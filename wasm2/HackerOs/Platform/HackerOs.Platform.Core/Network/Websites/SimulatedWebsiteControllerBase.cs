using System.Collections.Immutable;
using HackerOs.Simulation.Abstractions.Network;

namespace HackerOs.Platform.Core.Network.Websites;

/// <summary>
/// Shared base for all in-memory simulated website controllers.
/// Provides simple route registration (exact path or single-segment
/// parameterized paths like "/account/{id}") and request dispatch.
/// </summary>
public abstract class SimulatedWebsiteControllerBase : ISimulatedWebsiteController
{
    private readonly List<RouteEntry> _routes = [];

    /// <inheritdoc/>
    public abstract string PrimaryHostname { get; }

    /// <inheritdoc/>
    public virtual IReadOnlyCollection<string> AliasHostnames => [];

    /// <inheritdoc/>
    public abstract string Theme { get; }

    /// <inheritdoc/>
    public abstract string SiteName { get; }

    /// <inheritdoc/>
    public SimulatedHttpResponse ProcessRequest(SimulatedHttpRequest request)
    {
        foreach (var entry in _routes)
        {
            if (!string.Equals(entry.Method, request.Method, StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryMatchRoute(entry.PathPattern, request.Path, out var routeParams))
            {
                var enriched = request with
                {
                    RouteParams = routeParams
                };
                return entry.Handler(enriched);
            }
        }

        return SimulatedHttpResponse.NotFound(request.Path);
    }

    // ── Route registration helpers ─────────────────────────────────────────

    /// <summary>Registers a GET handler for <paramref name="pathPattern"/>.</summary>
    protected void Get(string pathPattern, Func<SimulatedHttpRequest, SimulatedHttpResponse> handler) =>
        _routes.Add(new RouteEntry(SimulatedHttpMethods.Get, pathPattern, handler));

    /// <summary>Registers a POST handler for <paramref name="pathPattern"/>.</summary>
    protected void Post(string pathPattern, Func<SimulatedHttpRequest, SimulatedHttpResponse> handler) =>
        _routes.Add(new RouteEntry(SimulatedHttpMethods.Post, pathPattern, handler));

    // ── Path matching ──────────────────────────────────────────────────────

    private static bool TryMatchRoute(
        string pattern,
        string requestPath,
        out ImmutableDictionary<string, string> routeParams)
    {
        routeParams = ImmutableDictionary<string, string>.Empty;

        // Normalize trailing slashes
        var p = pattern.TrimEnd('/');
        var r = requestPath.TrimEnd('/');
        if (p.Length == 0) p = "/";
        if (r.Length == 0) r = "/";

        // Fast path: exact match
        if (string.Equals(p, r, StringComparison.OrdinalIgnoreCase))
            return true;

        // Parameterized match: /segment/{param}
        var patternParts = p.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var requestParts = r.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (patternParts.Length != requestParts.Length)
            return false;

        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < patternParts.Length; i++)
        {
            var seg = patternParts[i];
            if (seg.StartsWith('{') && seg.EndsWith('}'))
            {
                var paramName = seg[1..^1];
                builder[paramName] = Uri.UnescapeDataString(requestParts[i]);
            }
            else if (!string.Equals(seg, requestParts[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        routeParams = builder.ToImmutable();
        return true;
    }

    private sealed record RouteEntry(
        string Method,
        string PathPattern,
        Func<SimulatedHttpRequest, SimulatedHttpResponse> Handler);
}
