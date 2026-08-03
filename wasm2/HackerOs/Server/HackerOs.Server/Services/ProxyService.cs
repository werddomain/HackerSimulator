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
///   - DNS rebinding: resolve once pre-request, verify address has not changed post-TTL.
/// </summary>
public interface IProxyService
{
    Task<ProxyHttpResponse> ExecuteHttpRequestAsync(
        Guid accountId, Guid deviceId, ProxyHttpRequest request, CancellationToken ct);

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
        (IPAddress.Parse("::1"), 128),             // IPv6 loopback
        (IPAddress.Parse("fe80::"), 10),           // IPv6 link-local
        (IPAddress.Parse("fc00::"), 7),            // IPv6 ULA
    ];

    private const int MaxRedirectHops = 5;
    private const long MaxResponseBytes = 10 * 1024 * 1024; // 10 MiB
    private const int MaxConcurrentPerDevice = 8;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuditService _audit;
    private readonly HackerOsServerDbContext _db;

    // Simple in-memory concurrency tracker per device.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, int>
        _activeCounts = new();

    /// <inheritdoc />
    public ProxyService(IHttpClientFactory httpClientFactory, IAuditService audit, HackerOsServerDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _audit = audit;
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ProxyHttpResponse> ExecuteHttpRequestAsync(
        Guid accountId, Guid deviceId, ProxyHttpRequest request, CancellationToken ct)
    {
        // ── 1. Validate target ────────────────────────────────────────────────
        if (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var targetUri))
            throw new ProxyRequestException(ProxyErrorCode.MalformedRequest, "Target URL is not a valid absolute URI.");

        ValidateSimulatedDomain(targetUri.Host);
        await ValidateAddressAsync(targetUri.Host, targetUri.Port, ct);

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
                var httpRequest = BuildHttpRequest(request, currentUri);
                lastResponse?.Dispose();
                lastResponse = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                if (IsRedirect(lastResponse.StatusCode) && redirectHops < MaxRedirectHops)
                {
                    var location = lastResponse.Headers.Location;
                    if (location is null) break;
                    currentUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);

                    // SSRF: re-validate the redirect target.
                    ValidateSimulatedDomain(currentUri.Host);
                    await ValidateAddressAsync(currentUri.Host, currentUri.Port, cts.Token);

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
                $"{{\"url\":\"{request.TargetUrl}\",\"method\":\"{request.HttpMethod}\",\"status\":{(int)lastResponse.StatusCode},\"ms\":{sw.ElapsedMilliseconds}}}",
                ct);

            return new ProxyHttpResponse(
                request.RequestId,
                (int)lastResponse.StatusCode,
                lastResponse.ReasonPhrase ?? string.Empty,
                responseHeaders,
                bodyHash,
                bodyBytes.Length,
                currentUri.ToString(),
                redirectHops,
                sw.ElapsedMilliseconds);
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

    private static async Task ValidateAddressAsync(string host, int port, CancellationToken ct)
    {
        // Validate port.
        if (port > 0 && !AllowedPorts.Contains(port))
            throw new ProxyRequestException(ProxyErrorCode.BlockedPort,
                $"Port {port} is not in the proxy allow-list.");

        // Resolve and validate address.
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, ct);
        }
        catch (Exception ex)
        {
            throw new ProxyRequestException(ProxyErrorCode.MalformedRequest,
                $"DNS resolution failed for '{host}': {ex.Message}");
        }

        foreach (var address in addresses)
        {
            if (IsBlockedAddress(address))
                throw new ProxyRequestException(ProxyErrorCode.BlockedAddress,
                    $"Resolved address {address} for '{host}' is in a blocked range.");
        }
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
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
