using System.Net;
using HackerOs.Server.Contracts.Proxy;
using HackerOs.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace HackerOs.Server.Services;

/// <summary>
/// Server-side validated HTTP proxy with SSRF protection.
/// Implements P5-PROXY-001 through P5-PROXY-007.
///
/// Security model:
///   - All policy is validated SERVER-SIDE. Client claims are never trusted.
///   - Blocked address ranges: loopback, link-local, private (RFC-1918), cloud metadata.
///   - Blocked ports: 0–79, 81–442, 444–65535 unless explicitly allow-listed.
///   - Max redirects: 5 (server follows; client sees final URL).
///   - Simulated domain suffixes (.hackeros.local, .sim) → always 403.
///   - DNS rebinding: validate every DNS answer and pin the selected address for the socket connect.
/// </summary>
public interface IProxyService
{
    Task<ProxyHttpResponse> ExecuteHttpRequestAsync(
        Guid accountId, Guid deviceId, ProxyHttpRequest request, CancellationToken ct);

    /// <summary>
    /// Executes a single-port TCP reachability probe (ADR 0035) — one connect attempt against one
    /// host:port, no data exchanged. Reuses the same SSRF address-range and simulated-domain
    /// protections as the HTTP proxy, but does not restrict the target port to 80/443, since an
    /// arbitrary-port probe is the entire point.
    /// </summary>
    Task<ProxyTcpProbeResponse> ExecuteTcpProbeAsync(
        Guid accountId, Guid deviceId, ProxyTcpProbeRequest request, CancellationToken ct);

    Task<ProxyPolicyResponse> GetPolicyAsync(Guid deviceId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class ProxyService : IProxyService
{
    // Simulated-domain suffixes that must never reach this proxy (P5-PROXY-006).
    private static readonly string[] SimulatedSuffixes = [".hackeros.local", ".sim", ".hackeros"];

    // Allowed outbound ports (80 for HTTP, 443 for HTTPS).
    private static readonly HashSet<int> AllowedPorts = [80, 443];

    // Blocked RFC-1918 and special-purpose private ranges.
    private static readonly (IPAddress Network, int PrefixLength)[] BlockedRanges =
    [
        (IPAddress.Parse("10.0.0.0"), 8),
        (IPAddress.Parse("172.16.0.0"), 12),
        (IPAddress.Parse("192.168.0.0"), 16),
        (IPAddress.Parse("127.0.0.0"), 8),
        (IPAddress.Parse("169.254.0.0"), 16),     // link-local / cloud metadata
        (IPAddress.Parse("100.64.0.0"), 10),       // CGNAT
        (IPAddress.Parse("0.0.0.0"), 8),           // unspecified/current network
        (IPAddress.Parse("192.0.2.0"), 24),        // documentation
        (IPAddress.Parse("198.18.0.0"), 15),       // benchmark testing
        (IPAddress.Parse("198.51.100.0"), 24),     // documentation
        (IPAddress.Parse("203.0.113.0"), 24),      // documentation
        (IPAddress.Parse("224.0.0.0"), 4),         // multicast/reserved
        (IPAddress.Parse("::"), 128),              // IPv6 unspecified
        (IPAddress.Parse("::1"), 128),             // IPv6 loopback
        (IPAddress.Parse("fe80::"), 10),           // IPv6 link-local
        (IPAddress.Parse("fc00::"), 7),            // IPv6 ULA
        (IPAddress.Parse("ff00::"), 8),             // IPv6 multicast
    ];

    private const int MaxRedirectHops = 5;
    private const long MaxResponseBytes = 10 * 1024 * 1024; // 10 MiB
    private const int MaxConcurrentPerDevice = 8;

    // TCP probe timeout is clamped much shorter than the HTTP proxy's — a scan-shaped operation
    // should fail fast rather than hold a connection slot for up to 30 seconds per port.
    private const int MaxTcpProbeTimeoutSeconds = 5;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuditService _audit;
    private readonly HackerOsServerDbContext _db;
    private readonly IProxyAddressResolver _addressResolver;
    private readonly IProxyConnectionPinAccessor _connectionPin;
    private readonly IProxyTcpConnector _tcpConnector;

    // Simple in-memory concurrency tracker per device.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, int>
        _activeCounts = new();

    /// <inheritdoc />
    public ProxyService(
        IHttpClientFactory httpClientFactory,
        IAuditService audit,
        HackerOsServerDbContext db,
        IProxyAddressResolver addressResolver,
        IProxyConnectionPinAccessor connectionPin,
        IProxyTcpConnector tcpConnector)
    {
        _httpClientFactory = httpClientFactory;
        _audit = audit;
        _db = db;
        _addressResolver = addressResolver;
        _connectionPin = connectionPin;
        _tcpConnector = tcpConnector;
    }

    /// <inheritdoc />
    public async Task<ProxyHttpResponse> ExecuteHttpRequestAsync(
        Guid accountId, Guid deviceId, ProxyHttpRequest request, CancellationToken ct)
    {
        // Authenticate against durable server state rather than trusting token
        // identifiers after a device is revoked or moved to another account.
        await ValidateDeviceOwnershipAsync(accountId, deviceId, ct);

        // ── 1. Validate target ────────────────────────────────────────────────
        if (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var targetUri))
            throw new ProxyRequestException(ProxyErrorCode.MalformedRequest, "Target URL is not a valid absolute URI.");

        ValidateRequest(request, targetUri);
        ValidateSimulatedDomain(targetUri.Host);

        // ── 2. Enforce concurrency quota ──────────────────────────────────────
        var current = _activeCounts.AddOrUpdate(deviceId, 1, (_, v) => v + 1);
        if (current > MaxConcurrentPerDevice)
        {
            _activeCounts.AddOrUpdate(deviceId, 0, (_, v) => Math.Max(0, v - 1));
            throw new ProxyRequestException(ProxyErrorCode.QuotaExceeded,
                $"Concurrent request limit of {MaxConcurrentPerDevice} exceeded.");
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            return await ExecuteCoreAsync(accountId, deviceId, request, targetUri, ct);
        }
        finally
        {
            sw.Stop();
            _activeCounts.AddOrUpdate(deviceId, 0, (_, v) => Math.Max(0, v - 1));
        }
    }

    /// <inheritdoc />
    public async Task<ProxyTcpProbeResponse> ExecuteTcpProbeAsync(
        Guid accountId, Guid deviceId, ProxyTcpProbeRequest request, CancellationToken ct)
    {
        await ValidateDeviceOwnershipAsync(accountId, deviceId, ct);

        if (string.IsNullOrWhiteSpace(request.Host))
            throw new ProxyRequestException(ProxyErrorCode.MalformedRequest, "A target host is required.");
        if (request.Port is < 1 or > 65535)
            throw new ProxyRequestException(ProxyErrorCode.MalformedRequest, "Port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(request.AppId) || request.AppId.Length > 256)
            throw new ProxyRequestException(ProxyErrorCode.CapabilityDenied,
                "A valid registered application identifier is required.");

        ValidateSimulatedDomain(request.Host);

        var current = _activeCounts.AddOrUpdate(deviceId, 1, (_, v) => v + 1);
        if (current > MaxConcurrentPerDevice)
        {
            _activeCounts.AddOrUpdate(deviceId, 0, (_, v) => Math.Max(0, v - 1));
            throw new ProxyRequestException(ProxyErrorCode.QuotaExceeded,
                $"Concurrent request limit of {MaxConcurrentPerDevice} exceeded.");
        }

        try
        {
            // No port allow-list here — probing an arbitrary port is the entire point (unlike the
            // HTTP proxy, which only ever needs 80/443). Address-range and simulated-domain
            // protection still fully apply.
            var pinnedAddress = await ResolveAndValidateAddressAsync(
                request.Host, request.Port, ct, enforceHttpPortAllowList: false);

            var timeout = TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 1, MaxTcpProbeTimeoutSeconds));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ProxyTcpProbeState state = await _tcpConnector.ProbeAsync(pinnedAddress, request.Port, timeout, ct);
            sw.Stop();

            await _audit.RecordAsync(accountId, deviceId, "PROXY_TCP_PROBE",
                $"{{\"host\":\"{request.Host}\",\"port\":{request.Port},\"state\":\"{state}\",\"ms\":{sw.ElapsedMilliseconds}}}",
                ct);

            return new ProxyTcpProbeResponse(request.RequestId, state, sw.ElapsedMilliseconds);
        }
        finally
        {
            _activeCounts.AddOrUpdate(deviceId, 0, (_, v) => Math.Max(0, v - 1));
        }
    }

    /// <inheritdoc />
    public async Task<ProxyPolicyResponse> GetPolicyAsync(Guid deviceId, CancellationToken ct)
    {
        var current = _activeCounts.GetValueOrDefault(deviceId, 0);
        return new ProxyPolicyResponse(
            deviceId,
            MaxConcurrentPerDevice,
            current,
            MaxResponseBytes,
            30, // MaxDurationSeconds
            MaxRedirectHops,
            ["http", "https"],
            30,  // AuditRetentionDays
            []);  // no operator weakenings in default config
    }

    // ── Core HTTP execution ───────────────────────────────────────────────────

    private async Task<ProxyHttpResponse> ExecuteCoreAsync(
        Guid accountId, Guid deviceId, ProxyHttpRequest request,
        Uri targetUri, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("proxy");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var currentUri = targetUri;
        int redirectHops = 0;
        HttpResponseMessage? lastResponse = null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 1, 30)));

        try
        {
            while (true)
            {
                var pinnedAddress = await ResolveAndValidateAddressAsync(
                    currentUri.Host, currentUri.Port, cts.Token);
                var httpRequest = BuildHttpRequest(request, currentUri);
                lastResponse?.Dispose();
                using (_connectionPin.Push(pinnedAddress))
                {
                    lastResponse = await client.SendAsync(
                        httpRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                }

                if (IsRedirect(lastResponse.StatusCode) && redirectHops < MaxRedirectHops)
                {
                    var location = lastResponse.Headers.Location;
                    if (location is null) break;
                    currentUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);

                    // SSRF: re-validate the redirect target.
                    ValidateSimulatedDomain(currentUri.Host);
                    ValidateRequest(request, currentUri);

                    redirectHops++;
                    continue;
                }

                if (IsRedirect(lastResponse.StatusCode) && redirectHops >= MaxRedirectHops)
                    throw new ProxyRequestException(ProxyErrorCode.TooManyRedirects,
                        $"Maximum of {MaxRedirectHops} redirect hops exceeded.");

                break;
            }

            // Read body with size limit.
            var bodyStream = await lastResponse!.Content.ReadAsStreamAsync(cts.Token);
            var bodyBytes = await ReadWithLimitAsync(bodyStream, MaxResponseBytes, cts.Token);
            var bodyHash = bodyBytes.Length > 0
                ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bodyBytes)).ToLowerInvariant()
                : null;

            var responseHeaders = lastResponse.Headers
                .Concat(lastResponse.Content.Headers)
                .Where(h => !IsHopByHopHeader(h.Key))
                .SelectMany(h => h.Value.Select(v => new ProxyHeader(h.Key, v)))
                .ToList();

            sw.Stop();
            await _audit.RecordAsync(accountId, deviceId, "PROXY_REQUEST",
                $"{{\"url\":\"{request.TargetUrl}\",\"method\":\"{request.HttpMethod}\",\"status\":{(int)lastResponse.StatusCode},\"ms\":{sw.ElapsedMilliseconds},\"includeBody\":{(request.IncludeBody ? "true" : "false")}}}",
                ct);

            // Body bytes were already fetched (and size-capped) above regardless of IncludeBody, to
            // compute the hash every response carries; only base64-encode and return them when asked.
            string? bodyBase64 = request.IncludeBody && bodyBytes.Length > 0
                ? Convert.ToBase64String(bodyBytes)
                : null;

            return new ProxyHttpResponse(
                request.RequestId,
                (int)lastResponse.StatusCode,
                lastResponse.ReasonPhrase ?? string.Empty,
                responseHeaders,
                bodyHash,
                bodyBytes.Length,
                currentUri.ToString(),
                redirectHops,
                sw.ElapsedMilliseconds,
                bodyBase64);
        }
        finally
        {
            lastResponse?.Dispose();
        }
    }

    private static HttpRequestMessage BuildHttpRequest(ProxyHttpRequest request, Uri targetUri)
    {
        var httpRequest = new HttpRequestMessage(
            new HttpMethod(request.HttpMethod ?? "GET"),
            targetUri);

        // Forward safe headers only; strip hop-by-hop and dangerous headers.
        foreach (var header in request.Headers)
        {
            if (!IsBlockedRequestHeader(header.Name))
                httpRequest.Headers.TryAddWithoutValidation(header.Name, header.Value);
        }

        return httpRequest;
    }

    // ── SSRF validation ───────────────────────────────────────────────────────

    private static void ValidateSimulatedDomain(string host)
    {
        foreach (var suffix in SimulatedSuffixes)
        {
            if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                throw new ProxyRequestException(ProxyErrorCode.SimulatedDomainBlocked,
                    $"Simulated domain '{host}' cannot be reached through the real proxy.");
        }
    }

    private async Task<IPAddress> ResolveAndValidateAddressAsync(
        string host, int port, CancellationToken ct, bool enforceHttpPortAllowList = true)
    {
        // Validate port. The HTTP-only allow-list (80/443) doesn't apply to the TCP probe path,
        // where an arbitrary target port is the entire point — but the port must still be a valid
        // TCP port number.
        if (enforceHttpPortAllowList)
        {
            if (port > 0 && !AllowedPorts.Contains(port))
                throw new ProxyRequestException(ProxyErrorCode.BlockedPort,
                    $"Port {port} is not in the proxy allow-list.");
        }
        else if (port is < 1 or > 65535)
        {
            throw new ProxyRequestException(ProxyErrorCode.BlockedPort, $"Port {port} is not a valid TCP port.");
        }

        // Resolve and validate address.
        IPAddress[] addresses;
        try
        {
            addresses = [.. await _addressResolver.ResolveAsync(host, ct)];
        }
        catch (Exception ex)
        {
            throw new ProxyRequestException(ProxyErrorCode.MalformedRequest,
                $"DNS resolution failed for '{host}': {ex.Message}");
        }

        if (addresses.Length == 0)
            throw new ProxyRequestException(ProxyErrorCode.MalformedRequest,
                $"DNS resolution returned no addresses for '{host}'.");

        foreach (var address in addresses)
        {
            if (IsBlockedAddress(address))
                throw new ProxyRequestException(ProxyErrorCode.BlockedAddress,
                    $"Resolved address {address} for '{host}' is in a blocked range.");
        }

        // Sorting avoids resolver-order-dependent behavior while preserving both
        // IPv4 and IPv6 validation. The chosen address is pinned for ConnectCallback.
        return addresses.OrderBy(static address => address.ToString(), StringComparer.Ordinal).First();
    }

    private async Task ValidateDeviceOwnershipAsync(Guid accountId, Guid deviceId, CancellationToken ct)
    {
        var authorized = await _db.Devices.AsNoTracking().AnyAsync(
            device => device.DeviceId == deviceId
                && device.AccountId == accountId
                && !device.IsRevoked,
            ct);

        if (!authorized)
            throw new ProxyRequestException(ProxyErrorCode.CapabilityDenied,
                "The authenticated device is not registered to this account or has been revoked.");
    }

    private static void ValidateRequest(ProxyHttpRequest request, Uri targetUri)
    {
        if (request.Protocol is not ProxyProtocol.Http)
            throw new ProxyRequestException(ProxyErrorCode.MalformedRequest,
                "This endpoint accepts only the HTTP proxy protocol.");

        if (targetUri.Scheme is not ("http" or "https"))
            throw new ProxyRequestException(ProxyErrorCode.MalformedRequest,
                "Only HTTP and HTTPS target URLs are supported.");

        if (string.IsNullOrWhiteSpace(request.AppId) || request.AppId.Length > 256)
            throw new ProxyRequestException(ProxyErrorCode.CapabilityDenied,
                "A valid registered application identifier is required.");

        if (request.BodyBytes < 0 || request.BodyBytes > MaxResponseBytes)
            throw new ProxyRequestException(ProxyErrorCode.PayloadTooLarge,
                $"Request body metadata exceeds the {MaxResponseBytes / 1024 / 1024} MiB limit.");
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        foreach (var (network, prefixLength) in BlockedRanges)
        {
            if (address.AddressFamily == network.AddressFamily && IsInSubnet(address, network, prefixLength))
                return true;
        }
        return false;
    }

    private static bool IsInSubnet(IPAddress address, IPAddress network, int prefixLength)
    {
        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        if (addressBytes.Length != networkBytes.Length) return false;

        int fullBytes = prefixLength / 8;
        int remainingBits = prefixLength % 8;

        for (int i = 0; i < fullBytes; i++)
            if (addressBytes[i] != networkBytes[i]) return false;

        if (remainingBits > 0)
        {
            int mask = 0xFF << (8 - remainingBits) & 0xFF;
            if ((addressBytes[fullBytes] & mask) != (networkBytes[fullBytes] & mask)) return false;
        }

        return true;
    }

    // ── Header filtering ──────────────────────────────────────────────────────

    private static readonly HashSet<string> HopByHopHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        { "connection", "keep-alive", "proxy-authenticate", "proxy-authorization",
          "te", "trailers", "transfer-encoding", "upgrade" };

    private static readonly HashSet<string> BlockedRequestHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        { "host", "authorization", "cookie", "x-forwarded-for", "x-real-ip" };

    private static bool IsHopByHopHeader(string name) => HopByHopHeaders.Contains(name);
    private static bool IsBlockedRequestHeader(string name) => BlockedRequestHeaders.Contains(name);

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently
                   or HttpStatusCode.Found
                   or HttpStatusCode.SeeOther
                   or HttpStatusCode.TemporaryRedirect
                   or HttpStatusCode.PermanentRedirect;

    private static async Task<byte[]> ReadWithLimitAsync(
        Stream stream, long maxBytes, CancellationToken ct)
    {
        var ms = new System.IO.MemoryStream();
        var buffer = new byte[8192];
        long totalRead = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            totalRead += read;
            if (totalRead > maxBytes)
                throw new ProxyRequestException(ProxyErrorCode.PayloadTooLarge,
                    $"Response exceeds the {maxBytes / 1024 / 1024} MiB limit.");
            ms.Write(buffer, 0, read);
        }
        return ms.ToArray();
    }
}

/// <summary>
/// Exception thrown by the proxy service when a request violates a security rule.
/// The endpoint maps this to a ProxyErrorResponse with the appropriate HTTP status.
/// </summary>
public sealed class ProxyRequestException : Exception
{
    public string ErrorCode { get; }
    public ProxyRequestException(string errorCode, string message)
        : base(message) => ErrorCode = errorCode;
}
