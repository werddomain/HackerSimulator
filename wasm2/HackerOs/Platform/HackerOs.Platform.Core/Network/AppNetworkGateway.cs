using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Net;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Server.Contracts.Proxy;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Metrics;
using HackerOs.Simulation.Abstractions.Network;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.ServerConnection;

namespace HackerOs.Platform.Core.Network;

/// <summary>
/// Trusted <see cref="IAppNetworkGateway"/> of one app instance (ADR 0056, section 6): the OS
/// network stack an app reaches through <c>HackDns</c>, <c>HackPing</c>, <c>HackHttpClient</c> and
/// <c>HackTcpClient</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every call checks the app's capability first (<c>network.simulated.read</c>, or
/// <c>network.simulated.write</c> for an HTTP method other than GET and HEAD), then asks the
/// simulated network. Only when the simulated network does not know the target does the OS decide,
/// on its own, whether to relay the call to the real Internet: the app must hold
/// <c>network.real.access</c> and the optional server must be connected and hand out an access
/// token (ADR 0023, 0028, 0035). The token, the server URL and even whether a server exists never
/// reach the app: a missing route is just "host not found", like a machine without a default
/// gateway.
/// </para>
/// <para>
/// What the proxy can relay is unchanged from the commands that used it directly before: GET (with
/// body) and HEAD, a single-port TCP connect probe, and a timed HEAD standing in for an ICMP echo,
/// which a browser cannot send. DNS, OS detection, port ranges and POST stay simulation-only.
/// </para>
/// <para>
/// This is also the one place network traffic of an app passes through, where ADR 0057 counts the
/// bytes shown in <c>/proc/net/dev</c> and charges them to the app's process
/// (<c>/proc/&lt;pid&gt;/io</c>); see <see cref="NetworkTrafficMeter"/> for how a packet is sized.
/// A call refused for a missing capability never reaches the wire and counts nothing.
/// </para>
/// </remarks>
public sealed class AppNetworkGateway : IAppNetworkGateway
{
    private const int ProxyTimeoutSeconds = 10;
    private const int ProbeTimeoutSeconds = 5;

    private readonly ISimulatedNetworkService _network;
    private readonly ICapabilityChecker _capabilities;
    private readonly string _appId;
    private readonly IServerConnectionService? _serverConnection;
    private readonly IProxyClient? _proxy;
    private readonly NetworkTrafficMeter _meter;
    private int _echoSequence;

    /// <summary>Initializes the gateway of one app instance.</summary>
    /// <param name="network">The simulated network, always tried first.</param>
    /// <param name="capabilities">Capability checker bound to the app and its user.</param>
    /// <param name="appId">Id of the app, reported to the server proxy for its audit log.</param>
    /// <param name="serverConnection">Optional server connection; <see langword="null"/> disables the real-network fallback.</param>
    /// <param name="proxy">Optional server proxy client; <see langword="null"/> disables the real-network fallback.</param>
    /// <param name="statistics">Optional interface counters behind <c>/proc/net/dev</c> (ADR 0057).</param>
    /// <param name="processIo">Optional per-process I/O accounting, charged with this app's traffic.</param>
    /// <param name="pid">Process of this app instance, the one <paramref name="processIo"/> charges.</param>
    public AppNetworkGateway(
        ISimulatedNetworkService network,
        ICapabilityChecker capabilities,
        string appId,
        IServerConnectionService? serverConnection = null,
        IProxyClient? proxy = null,
        INetworkInterfaceStatistics? statistics = null,
        IProcessIoAccounting? processIo = null,
        ProcessId? pid = null)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        _appId = appId;
        _serverConnection = serverConnection;
        _proxy = proxy;
        _meter = new NetworkTrafficMeter(statistics, processIo, pid);
    }

    /// <inheritdoc />
    public async ValueTask<HackNetworkResult<HackHostEntry>> ResolveAsync(string hostName, CancellationToken cancellationToken = default)
    {
        HackNetworkResult<HackHostEntry> result = await ResolveCoreAsync(hostName, cancellationToken).ConfigureAwait(false);
        _meter.Dns(hostName, result.Value?.AddressList.Count ?? 0, result.Succeeded);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<HackNetworkResult<HackHostEntry>> ReverseLookupAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        HackNetworkResult<HackHostEntry> result = await ReverseLookupCoreAsync(address, cancellationToken).ConfigureAwait(false);
        // A PTR query asks for "<octets>.in-addr.arpa".
        _meter.Dns($"{address}.in-addr.arpa", result.Succeeded ? 1 : 0, result.Succeeded);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<HackNetworkResult<HackPingReply>> PingAsync(
        string hostNameOrAddress, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        HackNetworkResult<HackPingReply> result = await PingCoreAsync(hostNameOrAddress, timeout, cancellationToken).ConfigureAwait(false);
        _meter.Echo(result.Succeeded && result.Value!.Status == HackIPStatus.Success);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<HackNetworkResult<HackHttpResponse>> SendAsync(
        HackHttpRequest request, CancellationToken cancellationToken = default)
    {
        HackNetworkResult<HackHttpResponse> result = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
        _meter.Http(request, result);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<HackNetworkResult<IReadOnlyList<HackPortProbeResult>>> ProbeTcpAsync(
        string hostNameOrAddress, int firstPort, int lastPort, CancellationToken cancellationToken = default)
    {
        HackNetworkResult<IReadOnlyList<HackPortProbeResult>> result =
            await ProbeTcpCoreAsync(hostNameOrAddress, firstPort, lastPort, cancellationToken).ConfigureAwait(false);
        _meter.Probe(result, Math.Max(1, lastPort - firstPort + 1));
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<HackNetworkResult<HackOsFingerprint?>> DetectOperatingSystemAsync(
        string hostNameOrAddress, CancellationToken cancellationToken = default)
    {
        HackNetworkResult<HackOsFingerprint?> result =
            await DetectOperatingSystemCoreAsync(hostNameOrAddress, cancellationToken).ConfigureAwait(false);
        _meter.Fingerprint(result.Succeeded);
        return result;
    }

    // ── Simulated network ─────────────────────────────────────────────────

    private ValueTask<HackNetworkResult<HackHostEntry>> ResolveCoreAsync(string hostName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
        cancellationToken.ThrowIfCancellationRequested();
        _capabilities.Require(AppCapabilities.NetworkSimulatedRead);

        string? ip = _network.Dns.Resolve(hostName);
        return ValueTask.FromResult(ip is null
            ? HackNetworkResult<HackHostEntry>.Failure(HackNetworkError.HostNotFound)
            : HackNetworkResult<HackHostEntry>.Success(BuildEntry(_network.Dns.ReverseLookup(ip) ?? hostName, ip)));
    }

    private ValueTask<HackNetworkResult<HackHostEntry>> ReverseLookupCoreAsync(IPAddress address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        cancellationToken.ThrowIfCancellationRequested();
        _capabilities.Require(AppCapabilities.NetworkSimulatedRead);

        string ip = address.ToString();
        string? hostName = _network.Dns.ReverseLookup(ip);
        return ValueTask.FromResult(hostName is null
            ? HackNetworkResult<HackHostEntry>.Failure(HackNetworkError.HostNotFound)
            : HackNetworkResult<HackHostEntry>.Success(BuildEntry(hostName, ip)));
    }

    private async ValueTask<HackNetworkResult<HackPingReply>> PingCoreAsync(
        string hostNameOrAddress, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostNameOrAddress);
        cancellationToken.ThrowIfCancellationRequested();
        _capabilities.Require(AppCapabilities.NetworkSimulatedRead);

        SimulatedHost? host = _network.GetHost(hostNameOrAddress);
        if (host is null)
        {
            return await PingRealHostAsync(hostNameOrAddress, cancellationToken).ConfigureAwait(false);
        }

        IPAddress address = IPAddress.Parse(host.Ip);
        if (!host.IsUp)
        {
            return HackNetworkResult<HackPingReply>.Success(
                new HackPingReply(address, HackIPStatus.DestinationHostUnreachable, TimeSpan.Zero));
        }

        // Deterministic jitter of ±5% around the host's latency, cycling with each echo of this
        // instance, so consecutive `ping` lines differ the way a real link does.
        int sequence = Interlocked.Increment(ref _echoSequence);
        double milliseconds = Math.Round(host.LatencyMs + (host.LatencyMs * 0.05 * ((sequence % 3) - 1)), 3);
        TimeSpan roundTrip = TimeSpan.FromMilliseconds(milliseconds);
        return HackNetworkResult<HackPingReply>.Success(roundTrip > timeout
            ? new HackPingReply(address, HackIPStatus.TimedOut, TimeSpan.Zero)
            : new HackPingReply(address, HackIPStatus.Success, roundTrip));
    }

    private async ValueTask<HackNetworkResult<HackHttpResponse>> SendCoreAsync(
        HackHttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string method = request.Method.ToUpperInvariant();
        bool isRead = method is SimulatedHttpMethods.Get or SimulatedHttpMethods.Head;
        _capabilities.Require(isRead ? AppCapabilities.NetworkSimulatedRead : AppCapabilities.NetworkSimulatedWrite);

        SimulatedHost? host = _network.GetHost(request.RequestUri.Host);
        if (host is null)
        {
            return isRead
                ? await SendRealAsync(request, method, cancellationToken).ConfigureAwait(false)
                : HackNetworkResult<HackHttpResponse>.Failure(HackNetworkError.HostNotFound);
        }

        if (!host.IsUp)
        {
            return HackNetworkResult<HackHttpResponse>.Failure(HackNetworkError.HostUnreachable);
        }

        Dictionary<string, Dictionary<string, string>> jar = ToJar(request.Cookies);
        HashSet<HackCookie> before = [.. FromJar(jar)];
        string url = request.RequestUri.AbsoluteUri;

        SimulatedNavigationResult navigation;
        if (method == SimulatedHttpMethods.Post)
        {
            ImmutableDictionary<string, string> form = (request.Form ?? []).ToImmutableDictionary(StringComparer.Ordinal);
            SimulatedHttpResponse posted = _network.Post(url, form, jar);
            if (!posted.IsRedirect)
            {
                return HackNetworkResult<HackHttpResponse>.Success(
                    BuildResponse(posted.StatusCode, request.RequestUri, posted.Page, 0, includeBody: true, jar, before));
            }

            // 301/302/303 after a POST continue as a GET, as browsers and HttpClient do.
            navigation = _network.Navigate(ResolveLocation(request.RequestUri, posted.RedirectUrl!), jar);
            navigation = navigation with { RedirectCount = navigation.RedirectCount + 1 };
        }
        else if (isRead)
        {
            navigation = _network.Navigate(url, jar);
        }
        else
        {
            // Simulated sites only implement GET and POST.
            return HackNetworkResult<HackHttpResponse>.Success(
                BuildResponse(405, request.RequestUri, page: null, 0, includeBody: false, jar, before));
        }

        if (navigation.NetworkError is not null)
        {
            return HackNetworkResult<HackHttpResponse>.Failure(MapSimulatedError(navigation.NetworkError));
        }

        return HackNetworkResult<HackHttpResponse>.Success(BuildResponse(
            navigation.StatusCode,
            new Uri(navigation.FinalUrl),
            navigation.Page,
            navigation.RedirectCount,
            includeBody: method != SimulatedHttpMethods.Head,
            jar,
            before));
    }

    private async ValueTask<HackNetworkResult<IReadOnlyList<HackPortProbeResult>>> ProbeTcpCoreAsync(
        string hostNameOrAddress, int firstPort, int lastPort, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostNameOrAddress);
        cancellationToken.ThrowIfCancellationRequested();
        _capabilities.Require(AppCapabilities.NetworkSimulatedRead);

        SimulatedHost? host = _network.GetHost(hostNameOrAddress);
        if (host is null)
        {
            // ADR 0035: only one explicit port is ever relayed to a real host, never a range.
            return firstPort == lastPort
                ? await ProbeRealPortAsync(hostNameOrAddress, firstPort, cancellationToken).ConfigureAwait(false)
                : HackNetworkResult<IReadOnlyList<HackPortProbeResult>>.Failure(HackNetworkError.HostNotFound);
        }

        if (!host.IsUp)
        {
            return HackNetworkResult<IReadOnlyList<HackPortProbeResult>>.Failure(HackNetworkError.HostUnreachable);
        }

        IReadOnlyList<HackPortProbeResult> ports =
        [
            .. host.ScanPorts(firstPort, lastPort).Select(port => new HackPortProbeResult(
                port.PortNumber, ToPortState(port.State), port.Service?.Name, port.Service?.Version))
        ];
        return HackNetworkResult<IReadOnlyList<HackPortProbeResult>>.Success(ports);
    }

    private ValueTask<HackNetworkResult<HackOsFingerprint?>> DetectOperatingSystemCoreAsync(
        string hostNameOrAddress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostNameOrAddress);
        cancellationToken.ThrowIfCancellationRequested();
        _capabilities.Require(AppCapabilities.NetworkSimulatedRead);

        SimulatedHost? host = _network.GetHost(hostNameOrAddress);
        HackNetworkResult<HackOsFingerprint?> result = host switch
        {
            null => HackNetworkResult<HackOsFingerprint?>.Failure(HackNetworkError.HostNotFound),
            { IsUp: false } => HackNetworkResult<HackOsFingerprint?>.Failure(HackNetworkError.HostUnreachable),
            { OsFingerprint: { } os } => HackNetworkResult<HackOsFingerprint?>.Success(new HackOsFingerprint(os.Name, os.Version, os.Accuracy)),
            _ => HackNetworkResult<HackOsFingerprint?>.Success(null)
        };
        return ValueTask.FromResult(result);
    }

    // ── Real-network fallback ─────────────────────────────────────────────

    /// <summary>
    /// Opens the route to the real Internet for one call: <see langword="null"/> unless the app
    /// holds <c>network.real.access</c> and the optional server is connected with a valid token.
    /// </summary>
    private async ValueTask<(Uri ServerBaseUrl, string AccessToken)?> TryOpenRealRouteAsync(CancellationToken cancellationToken)
    {
        if (_serverConnection is null || _proxy is null
            || !_capabilities.Evaluate(AppCapabilities.NetworkRealAccess).Granted)
        {
            return null;
        }

        ServerConnectionState? state = await _serverConnection.GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return null;
        }

        string? accessToken = await _serverConnection.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        return accessToken is null ? null : (new Uri(state.ServerBaseUrl), accessToken);
    }

    private async ValueTask<HackNetworkResult<HackPingReply>> PingRealHostAsync(string target, CancellationToken cancellationToken)
    {
        if (await TryOpenRealRouteAsync(cancellationToken).ConfigureAwait(false) is not { } route)
        {
            return HackNetworkResult<HackPingReply>.Failure(HackNetworkError.HostNotFound);
        }

        string url = target.Contains("://", StringComparison.Ordinal) ? target : $"https://{target}";
        try
        {
            ProxyHttpResponse response = await _proxy!.ExecuteHttpRequestAsync(
                route.ServerBaseUrl,
                route.AccessToken,
                new ProxyHttpRequest(Guid.NewGuid(), ProxyProtocol.Http, url, SimulatedHttpMethods.Head, [], null, 0, ProxyTimeoutSeconds, _appId),
                cancellationToken).ConfigureAwait(false);
            return HackNetworkResult<HackPingReply>.Success(
                new HackPingReply(null, HackIPStatus.Success, TimeSpan.FromMilliseconds(response.DurationMs)));
        }
        catch (Exception exception) when (exception is ServerConnectionException or HttpRequestException)
        {
            return HackNetworkResult<HackPingReply>.Success(new HackPingReply(null, HackIPStatus.TimedOut, TimeSpan.Zero));
        }
    }

    private async ValueTask<HackNetworkResult<HackHttpResponse>> SendRealAsync(
        HackHttpRequest request, string method, CancellationToken cancellationToken)
    {
        if (await TryOpenRealRouteAsync(cancellationToken).ConfigureAwait(false) is not { } route)
        {
            return HackNetworkResult<HackHttpResponse>.Failure(HackNetworkError.HostNotFound);
        }

        bool includeBody = method == SimulatedHttpMethods.Get;
        try
        {
            ProxyHttpResponse response = await _proxy!.ExecuteHttpRequestAsync(
                route.ServerBaseUrl,
                route.AccessToken,
                new ProxyHttpRequest(
                    Guid.NewGuid(),
                    ProxyProtocol.Http,
                    request.RequestUri.AbsoluteUri,
                    method,
                    [.. request.Headers.Select(header => new ProxyHeader(header.Key, header.Value))],
                    null,
                    0,
                    ProxyTimeoutSeconds,
                    _appId,
                    IncludeBody: includeBody),
                cancellationToken).ConfigureAwait(false);

            byte[] content = includeBody && response.BodyBase64 is not null ? Convert.FromBase64String(response.BodyBase64) : [];
            Uri finalUri = Uri.TryCreate(response.FinalUrl, UriKind.Absolute, out Uri? parsed) ? parsed : request.RequestUri;
            return HackNetworkResult<HackHttpResponse>.Success(new HackHttpResponse(
                response.StatusCode,
                string.IsNullOrEmpty(response.ReasonPhrase) ? HackHttpResponse.GetReasonPhrase(response.StatusCode) : response.ReasonPhrase,
                finalUri,
                [.. response.Headers.Select(header => new KeyValuePair<string, string>(header.Name, header.Value))],
                content,
                Page: null,
                response.RedirectHops,
                SetCookies: []));
        }
        catch (Exception exception) when (exception is ServerConnectionException or HttpRequestException)
        {
            return HackNetworkResult<HackHttpResponse>.Failure(HackNetworkError.ConnectionFailed, exception.Message);
        }
    }

    private async ValueTask<HackNetworkResult<IReadOnlyList<HackPortProbeResult>>> ProbeRealPortAsync(
        string target, int port, CancellationToken cancellationToken)
    {
        if (await TryOpenRealRouteAsync(cancellationToken).ConfigureAwait(false) is not { } route)
        {
            return HackNetworkResult<IReadOnlyList<HackPortProbeResult>>.Failure(HackNetworkError.HostNotFound);
        }

        try
        {
            ProxyTcpProbeResponse response = await _proxy!.ExecuteTcpProbeAsync(
                route.ServerBaseUrl,
                route.AccessToken,
                new ProxyTcpProbeRequest(Guid.NewGuid(), target, port, ProbeTimeoutSeconds, _appId),
                cancellationToken).ConfigureAwait(false);
            HackPortState state = response.State switch
            {
                ProxyTcpProbeState.Open => HackPortState.Open,
                ProxyTcpProbeState.Closed => HackPortState.Closed,
                _ => HackPortState.Filtered
            };
            return HackNetworkResult<IReadOnlyList<HackPortProbeResult>>.Success([new HackPortProbeResult(port, state)]);
        }
        catch (Exception exception) when (exception is ServerConnectionException or HttpRequestException)
        {
            return HackNetworkResult<IReadOnlyList<HackPortProbeResult>>.Failure(HackNetworkError.ConnectionFailed, exception.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private HackHostEntry BuildEntry(string hostName, string ip)
    {
        string[] aliases =
        [
            .. _network.Dns.AllRecords
                .Where(record => record.Ip == ip && !string.Equals(record.Hostname, hostName, StringComparison.OrdinalIgnoreCase))
                .Select(record => record.Hostname)
        ];
        return new HackHostEntry(hostName, [IPAddress.Parse(ip)], aliases);
    }

    private static HackHttpResponse BuildResponse(
        int statusCode,
        Uri finalUri,
        SimulatedPage? page,
        int redirectCount,
        bool includeBody,
        Dictionary<string, Dictionary<string, string>> jar,
        HashSet<HackCookie> before)
    {
        // A HEAD answer announces the length of the body a GET would have returned, without it.
        byte[] body = [];
        if (page is not null)
        {
            using StringWriter writer = new() { NewLine = "\n" };
            SimulatedPageTextFormatter.WriteTo(writer, page);
            body = Encoding.UTF8.GetBytes(writer.ToString());
        }

        HackCookie[] setCookies = [.. FromJar(jar).Where(cookie => !before.Contains(cookie))];
        List<KeyValuePair<string, string>> headers =
        [
            new("Content-Type", "text/plain; charset=utf-8"),
            new("Content-Length", body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            .. setCookies.Select(cookie => new KeyValuePair<string, string>("Set-Cookie", $"{cookie.Name}={cookie.Value}; Path=/"))
        ];

        return new HackHttpResponse(
            statusCode,
            HackHttpResponse.GetReasonPhrase(statusCode),
            finalUri,
            headers,
            includeBody ? body : [],
            page,
            redirectCount,
            setCookies);
    }

    private static Dictionary<string, Dictionary<string, string>> ToJar(IReadOnlyList<HackCookie> cookies)
    {
        Dictionary<string, Dictionary<string, string>> jar = new(StringComparer.OrdinalIgnoreCase);
        foreach (HackCookie cookie in cookies)
        {
            if (!jar.TryGetValue(cookie.Domain, out Dictionary<string, string>? hostCookies))
            {
                hostCookies = new Dictionary<string, string>(StringComparer.Ordinal);
                jar[cookie.Domain] = hostCookies;
            }

            hostCookies[cookie.Name] = cookie.Value;
        }

        return jar;
    }

    private static IEnumerable<HackCookie> FromJar(Dictionary<string, Dictionary<string, string>> jar) =>
        jar.SelectMany(host => host.Value.Select(cookie => new HackCookie(host.Key.ToLowerInvariant(), cookie.Key, cookie.Value)));

    private static string ResolveLocation(Uri requestUri, string location) =>
        Uri.TryCreate(requestUri, location, out Uri? resolved) ? resolved.AbsoluteUri : location;

    private static HackNetworkError MapSimulatedError(string error) => error switch
    {
        SimulatedNetworkErrors.UnknownHost => HackNetworkError.HostNotFound,
        SimulatedNetworkErrors.ConnectionRefused => HackNetworkError.ConnectionRefused,
        SimulatedNetworkErrors.TooManyRedirects => HackNetworkError.TooManyRedirects,
        _ => HackNetworkError.HostUnreachable
    };

    private static HackPortState ToPortState(SimulatedPortState state) => state switch
    {
        SimulatedPortState.Open => HackPortState.Open,
        SimulatedPortState.Closed => HackPortState.Closed,
        _ => HackPortState.Filtered
    };
}
