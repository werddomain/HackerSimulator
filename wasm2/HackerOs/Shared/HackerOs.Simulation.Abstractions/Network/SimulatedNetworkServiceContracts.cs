namespace HackerOs.Simulation.Abstractions.Network;

// ---------------------------------------------------------------------------
// Website controller interface + registry contracts.
// ---------------------------------------------------------------------------

/// <summary>
/// Route pattern for a simulated website.
/// Supports exact paths ("/" or "/account") and single-segment parameterized
/// paths ("/account/{id}"). The extracted parameter values are added to
/// <see cref="SimulatedHttpRequest.RouteParams"/> before dispatch.
/// </summary>
public sealed record SimulatedRoute(string Method, string PathPattern);

/// <summary>
/// Contract for a simulated website controller.
/// Implementations register against one or more hostnames and handle
/// <see cref="SimulatedHttpRequest"/> values, returning structured
/// <see cref="SimulatedHttpResponse"/> values.
/// No I/O, real HTTP, or DOM interaction is permitted in implementations.
/// </summary>
public interface ISimulatedWebsiteController
{
    /// <summary>
    /// Canonical primary hostname (e.g. "hackersearch.net").
    /// </summary>
    string PrimaryHostname { get; }

    /// <summary>
    /// Additional hostnames this controller handles (e.g. "www.hackersearch.net").
    /// May be empty.
    /// </summary>
    IReadOnlyCollection<string> AliasHostnames { get; }

    /// <summary>
    /// The page theme token (e.g. "hacker", "bank", "darknet") used by the
    /// Blazor renderer to select CSS custom-property overrides.
    /// </summary>
    string Theme { get; }

    /// <summary>
    /// Human-readable site name shown in browser bookmarks/tabs.
    /// </summary>
    string SiteName { get; }

    /// <summary>
    /// Process a simulated request and return a structured response.
    /// Must never block the thread, call real network APIs, or modify
    /// shared mutable state concurrently.
    /// </summary>
    SimulatedHttpResponse ProcessRequest(SimulatedHttpRequest request);
}

/// <summary>
/// Registry of all simulated website controllers known to the platform.
/// Populated at boot from the host composition root.
/// </summary>
public interface ISimulatedWebsiteRegistry
{
    /// <summary>
    /// Attempts to find a controller that handles <paramref name="hostname"/>.
    /// Returns <c>null</c> when the hostname is not registered.
    /// </summary>
    ISimulatedWebsiteController? FindController(string hostname);

    /// <summary>
    /// Returns all registered controllers in insertion order.
    /// </summary>
    IReadOnlyList<ISimulatedWebsiteController> All { get; }
}

// ---------------------------------------------------------------------------
// Simulated DNS and network service contracts.
// ---------------------------------------------------------------------------

/// <summary>
/// Provides hostname-to-IP and reverse-IP-to-hostname resolution for the
/// simulated network. Never calls real DNS; uses an in-memory registry seeded
/// from the known <see cref="SimulatedHost"/> catalog.
/// </summary>
public interface ISimulatedDns
{
    /// <summary>
    /// Resolves a hostname to its simulated IPv4 address.
    /// Returns <c>null</c> when the hostname is not in the simulated zone.
    /// </summary>
    string? Resolve(string hostname);

    /// <summary>
    /// Reverse-resolves an IPv4 address to its canonical hostname.
    /// Returns <c>null</c> when no host matches.
    /// </summary>
    string? ReverseLookup(string ip);

    /// <summary>Returns all forward DNS records in the simulated zone.</summary>
    IReadOnlyList<(string Hostname, string Ip)> AllRecords { get; }
}

/// <summary>
/// Outcome of a simulated HTTP navigation through the network service.
/// Distinct from <see cref="SimulatedHttpResponse"/> because the service
/// follows redirects and applies cookie state before returning to callers.
/// </summary>
public sealed record SimulatedNavigationResult(
    /// <summary>Final URL after all redirects.</summary>
    string FinalUrl,
    /// <summary>Final HTTP status code.</summary>
    int StatusCode,
    /// <summary>Structured page content (null for non-content status codes).</summary>
    SimulatedPage? Page,
    /// <summary>Error description when the host is down or the DNS name is unknown.</summary>
    string? NetworkError,
    /// <summary>Number of redirects followed (max 10).</summary>
    int RedirectCount)
{
    /// <summary>True when navigation completed with a renderable page.</summary>
    public bool IsSuccess => NetworkError is null && Page is not null;
}

/// <summary>
/// Stable error codes returned by the simulated network service.
/// </summary>
public static class SimulatedNetworkErrors
{
    public const string UnknownHost      = "ERR_NAME_NOT_RESOLVED";
    public const string HostDown         = "ERR_HOST_UNREACHABLE";
    public const string ConnectionRefused = "ERR_CONNECTION_REFUSED";
    public const string TooManyRedirects = "ERR_TOO_MANY_REDIRECTS";
    public const string NoRoute          = "ERR_NO_ROUTE";
}

/// <summary>
/// High-level simulated network service.
/// Orchestrates DNS resolution, host reachability checks, cookie management,
/// redirect following, and controller dispatch. All operations are
/// synchronous and never touch real network APIs.
/// </summary>
public interface ISimulatedNetworkService
{
    /// <summary>
    /// Attempts to reach the host at the given URL as an HTTP navigation.
    /// Follows redirects up to 10 hops, applying and collecting cookies from
    /// the supplied <paramref name="sessionCookies"/> jar.
    /// </summary>
    /// <param name="url">The URL to navigate to (must start with http:// or https://).</param>
    /// <param name="sessionCookies">
    /// Current per-host cookie jar; updated in place with Set-Cookie directives
    /// from each response. Pass an empty mutable dictionary for a new session.
    /// </param>
    SimulatedNavigationResult Navigate(
        string url,
        Dictionary<string, Dictionary<string, string>> sessionCookies);

    /// <summary>
    /// Sends a simulated HTTP POST and returns the raw response.
    /// Redirects are NOT automatically followed; the caller must call
    /// <see cref="Navigate"/> if needed.
    /// </summary>
    SimulatedHttpResponse Post(
        string url,
        System.Collections.Immutable.ImmutableDictionary<string, string> formBody,
        Dictionary<string, Dictionary<string, string>> sessionCookies);

    /// <summary>
    /// Performs a simulated ping to the hostname or IP.
    /// Returns the synthetic latency in milliseconds, or null when the host
    /// is down or the address is unknown.
    /// </summary>
    double? Ping(string hostnameOrIp);

    /// <summary>
    /// Scans a host's port range and returns the visible ports.
    /// Returns an empty enumerable when the host is unknown or unreachable.
    /// </summary>
    IReadOnlyList<SimulatedPort> ScanPorts(string hostnameOrIp, int firstPort, int lastPort);

    /// <summary>
    /// Looks up host metadata for the given hostname or IP address.
    /// Returns null when the address is unknown.
    /// </summary>
    SimulatedHost? GetHost(string hostnameOrIp);

    /// <summary>
    /// Returns all hosts registered in the simulated network.
    /// </summary>
    IReadOnlyList<SimulatedHost> AllHosts { get; }

    /// <summary>
    /// The DNS service for direct hostname resolution.
    /// </summary>
    ISimulatedDns Dns { get; }
}
