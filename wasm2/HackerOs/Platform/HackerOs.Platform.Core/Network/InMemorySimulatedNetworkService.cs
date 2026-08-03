using System.Collections.Immutable;
using HackerOs.Simulation.Abstractions.Network;

namespace HackerOs.Platform.Core.Network;

/// <summary>
/// In-memory DNS resolver built from the registered <see cref="SimulatedHost"/> catalog.
/// Forward records are built from each host's Hostname field; additional alias
/// hostnames registered by website controllers are added at composition time.
/// </summary>
public sealed class InMemorySimulatedDns : ISimulatedDns
{
    // hostname → IP (case-insensitive)
    private readonly Dictionary<string, string> _forward  = new(StringComparer.OrdinalIgnoreCase);
    // IP → canonical hostname
    private readonly Dictionary<string, string> _reverse  = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Hostname, string Ip)> _allRecords = [];

    /// <summary>
    /// Initializes the DNS server from a list of hosts and optional alias mappings.
    /// </summary>
    public InMemorySimulatedDns(
        IEnumerable<SimulatedHost> hosts,
        IEnumerable<(string Alias, string Ip)>? aliases = null)
    {
        foreach (var host in hosts)
            AddRecord(host.Hostname, host.Ip);

        if (aliases is not null)
            foreach (var (alias, ip) in aliases)
                AddAlias(alias, ip);
    }

    private void AddRecord(string hostname, string ip)
    {
        if (_forward.TryAdd(hostname, ip))
        {
            _reverse.TryAdd(ip, hostname);
            _allRecords.Add((hostname, ip));
        }
    }

    private void AddAlias(string alias, string ip)
    {
        if (_forward.TryAdd(alias, ip))
            _allRecords.Add((alias, ip));
    }

    /// <inheritdoc/>
    public string? Resolve(string hostname) =>
        _forward.TryGetValue(hostname, out var ip) ? ip : null;

    /// <inheritdoc/>
    public string? ReverseLookup(string ip) =>
        _reverse.TryGetValue(ip, out var hostname) ? hostname : null;

    /// <inheritdoc/>
    public IReadOnlyList<(string Hostname, string Ip)> AllRecords => _allRecords;
}

/// <summary>
/// In-memory registry of <see cref="ISimulatedWebsiteController"/> implementations.
/// </summary>
public sealed class InMemorySimulatedWebsiteRegistry : ISimulatedWebsiteRegistry
{
    private readonly Dictionary<string, ISimulatedWebsiteController> _byHost =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ISimulatedWebsiteController> _all = [];

    /// <summary>
    /// Registers a controller under its primary hostname and all alias hostnames.
    /// Duplicate hostname registrations are silently ignored (first wins).
    /// </summary>
    public void Register(ISimulatedWebsiteController controller)
    {
        if (_byHost.TryAdd(controller.PrimaryHostname, controller))
            _all.Add(controller);

        foreach (var alias in controller.AliasHostnames)
            _byHost.TryAdd(alias, controller);
    }

    /// <inheritdoc/>
    public ISimulatedWebsiteController? FindController(string hostname) =>
        _byHost.TryGetValue(hostname, out var controller) ? controller : null;

    /// <inheritdoc/>
    public IReadOnlyList<ISimulatedWebsiteController> All => _all;
}

/// <summary>
/// Pure in-memory simulated network service.
/// Orchestrates DNS, host-reachability, cookie-jar management, redirect
/// following, and website-controller dispatch.
/// All methods are synchronous; no real I/O is ever performed.
/// </summary>
public sealed class InMemorySimulatedNetworkService : ISimulatedNetworkService
{
    private const int MaxRedirects = 10;

    private readonly Dictionary<string, SimulatedHost> _hostsByIp =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, SimulatedHost> _hostsByName =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ISimulatedWebsiteRegistry _registry;

    /// <inheritdoc/>
    public ISimulatedDns Dns { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SimulatedHost> AllHosts => [.._hostsByIp.Values];

    public InMemorySimulatedNetworkService(
        IEnumerable<SimulatedHost> hosts,
        ISimulatedWebsiteRegistry registry,
        IEnumerable<(string Alias, string Ip)>? dnsAliases = null)
    {
        _registry = registry;

        var hostList = hosts.ToList();
        foreach (var host in hostList)
        {
            _hostsByIp[host.Ip]           = host;
            _hostsByName[host.Hostname]   = host;
        }

        // Build DNS aliases from all registered controller alias hostnames too
        var aliases = dnsAliases?.ToList() ?? [];
        foreach (var ctrl in registry.All)
        {
            if (_hostsByName.TryGetValue(ctrl.PrimaryHostname, out var h))
                foreach (var alias in ctrl.AliasHostnames)
                    aliases.Add((alias, h.Ip));
        }

        Dns = new InMemorySimulatedDns(hostList, aliases);
    }

    /// <inheritdoc/>
    public SimulatedNavigationResult Navigate(
        string url,
        Dictionary<string, Dictionary<string, string>> sessionCookies)
    {
        int redirectCount = 0;
        string currentUrl = NormalizeUrl(url);

        while (true)
        {
            if (!TryParseUrl(currentUrl, out string host, out string path, out var query))
                return Fail(currentUrl, SimulatedNetworkErrors.UnknownHost, redirectCount);

            var cookies = GetCookies(sessionCookies, host);
            var request = SimulatedHttpRequest.Get(host, path, query, cookies);
            var (response, networkError) = Dispatch(request);

            if (networkError is not null)
                return Fail(currentUrl, networkError, redirectCount);

            // Apply Set-Cookie headers to the jar
            ApplyCookies(sessionCookies, host, response!.SetCookies);

            if (response.IsRedirect && redirectCount < MaxRedirects)
            {
                redirectCount++;
                string target = response.RedirectUrl!;
                if (target.StartsWith('/'))
                    target = $"https://{host}{target}";
                currentUrl = NormalizeUrl(target);
                continue;
            }

            if (response.IsRedirect)
                return Fail(currentUrl, SimulatedNetworkErrors.TooManyRedirects, redirectCount);

            return new SimulatedNavigationResult(
                FinalUrl: currentUrl,
                StatusCode: response.StatusCode,
                Page: response.Page,
                NetworkError: null,
                RedirectCount: redirectCount);
        }
    }

    /// <inheritdoc/>
    public SimulatedHttpResponse Post(
        string url,
        ImmutableDictionary<string, string> formBody,
        Dictionary<string, Dictionary<string, string>> sessionCookies)
    {
        if (!TryParseUrl(url, out string host, out string path, out _))
            return SimulatedHttpResponse.Error(SimulatedHttpStatus.NotFound, SimulatedNetworkErrors.UnknownHost);

        var cookies   = GetCookies(sessionCookies, host);
        var request   = SimulatedHttpRequest.Post(host, path, formBody, cookies);
        var (response, _) = Dispatch(request);

        if (response is null)
            return SimulatedHttpResponse.Error(SimulatedHttpStatus.InternalError, SimulatedNetworkErrors.HostDown);

        ApplyCookies(sessionCookies, host, response.SetCookies);
        return response;
    }

    /// <inheritdoc/>
    public double? Ping(string hostnameOrIp)
    {
        var host = LookupHost(hostnameOrIp);
        return host is { IsUp: true } ? host.LatencyMs : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SimulatedPort> ScanPorts(string hostnameOrIp, int firstPort, int lastPort)
    {
        var host = LookupHost(hostnameOrIp);
        return host is null || !host.IsUp
            ? []
            : [..host.ScanPorts(firstPort, lastPort)];
    }

    /// <inheritdoc/>
    public SimulatedHost? GetHost(string hostnameOrIp) => LookupHost(hostnameOrIp);

    // ── Private helpers ────────────────────────────────────────────────────

    private SimulatedHost? LookupHost(string hostnameOrIp)
    {
        if (_hostsByIp.TryGetValue(hostnameOrIp, out var byIp))   return byIp;
        if (_hostsByName.TryGetValue(hostnameOrIp, out var byName)) return byName;

        // Try DNS resolution then lookup
        var ip = Dns.Resolve(hostnameOrIp);
        return ip is not null && _hostsByIp.TryGetValue(ip, out var resolved) ? resolved : null;
    }

    private (SimulatedHttpResponse? Response, string? NetworkError) Dispatch(SimulatedHttpRequest request)
    {
        // 1. Check DNS
        var ip = Dns.Resolve(request.Host) ?? request.Host;
        if (!_hostsByIp.ContainsKey(ip) && !_hostsByName.ContainsKey(request.Host))
            return (null, SimulatedNetworkErrors.UnknownHost);

        // 2. Check host reachability
        var host = LookupHost(request.Host);
        if (host is null || !host.IsUp)
            return (null, SimulatedNetworkErrors.HostDown);

        // 3. Find controller
        var controller = _registry.FindController(request.Host);
        if (controller is null)
            return (SimulatedHttpResponse.NotFound(request.Path), null);

        // 4. Dispatch to controller
        try
        {
            return (controller.ProcessRequest(request), null);
        }
        catch (Exception)
        {
            return (SimulatedHttpResponse.Error(SimulatedHttpStatus.InternalError, "Controller error"), null);
        }
    }

    private static string NormalizeUrl(string url)
    {
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return "https://" + url;
        return url;
    }

    private static bool TryParseUrl(
        string url,
        out string host,
        out string path,
        out ImmutableDictionary<string, string> query)
    {
        host  = "";
        path  = "/";
        query = ImmutableDictionary<string, string>.Empty;

        try
        {
            var uri = new Uri(NormalizeUrl(url));
            host = uri.Host;
            path = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath;

            if (!string.IsNullOrEmpty(uri.Query))
            {
                var builder = ImmutableDictionary.CreateBuilder<string, string>();
                foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var idx = part.IndexOf('=');
                    if (idx > 0)
                        builder[Uri.UnescapeDataString(part[..idx])] =
                            Uri.UnescapeDataString(part[(idx + 1)..]);
                }
                query = builder.ToImmutable();
            }

            return true;
        }
        catch (UriFormatException) { return false; }
    }

    private static ImmutableDictionary<string, string> GetCookies(
        Dictionary<string, Dictionary<string, string>> jar, string host)
    {
        if (jar.TryGetValue(host, out var hostCookies))
            return hostCookies.ToImmutableDictionary();
        return ImmutableDictionary<string, string>.Empty;
    }

    private static void ApplyCookies(
        Dictionary<string, Dictionary<string, string>> jar,
        string host,
        ImmutableDictionary<string, string> setCookies)
    {
        if (setCookies.IsEmpty) return;
        if (!jar.TryGetValue(host, out var hostCookies))
        {
            hostCookies = new Dictionary<string, string>(StringComparer.Ordinal);
            jar[host] = hostCookies;
        }
        foreach (var (k, v) in setCookies)
            hostCookies[k] = v;
    }

    private static SimulatedNavigationResult Fail(string url, string error, int redirectCount) =>
        new(FinalUrl: url, StatusCode: 0, Page: null, NetworkError: error,
            RedirectCount: redirectCount);
}
