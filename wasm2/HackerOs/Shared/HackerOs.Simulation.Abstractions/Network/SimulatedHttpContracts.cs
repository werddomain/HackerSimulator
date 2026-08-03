using System.Collections.Immutable;

namespace HackerOs.Simulation.Abstractions.Network;

// ---------------------------------------------------------------------------
// Simulated HTTP request / response contracts.
// ---------------------------------------------------------------------------

/// <summary>
/// Immutable HTTP method constants used by the simulation.
/// </summary>
public static class SimulatedHttpMethods
{
    public const string Get    = "GET";
    public const string Post   = "POST";
    public const string Put    = "PUT";
    public const string Delete = "DELETE";
    public const string Head   = "HEAD";
}

/// <summary>
/// An immutable inbound HTTP-like request directed at a simulated website.
/// The simulation never makes real network calls; all fields are populated
/// by the in-memory network service before dispatch to the controller.
/// </summary>
public sealed record SimulatedHttpRequest(
    string Method,
    string Host,
    string Path,
    ImmutableDictionary<string, string> Query,
    ImmutableDictionary<string, string> Headers,
    ImmutableDictionary<string, string> Cookies,
    ImmutableDictionary<string, string> RouteParams,
    ImmutableDictionary<string, string>? FormBody = null,
    string? TextBody = null)
{
    /// <summary>Returns the canonical URL string for this request.</summary>
    public string Url =>
        $"https://{Host}{Path}" +
        (Query.Count > 0
            ? "?" + string.Join("&", Query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"))
            : string.Empty);

    /// <summary>Convenience factory for a simple GET request.</summary>
    public static SimulatedHttpRequest Get(string host, string path,
        ImmutableDictionary<string, string>? query = null,
        ImmutableDictionary<string, string>? cookies = null) =>
        new(
            Method: SimulatedHttpMethods.Get,
            Host: host,
            Path: path.Length > 0 ? path : "/",
            Query: query ?? ImmutableDictionary<string, string>.Empty,
            Headers: ImmutableDictionary<string, string>.Empty,
            Cookies: cookies ?? ImmutableDictionary<string, string>.Empty,
            RouteParams: ImmutableDictionary<string, string>.Empty);

    /// <summary>Convenience factory for a POST request with form body.</summary>
    public static SimulatedHttpRequest Post(string host, string path,
        ImmutableDictionary<string, string> form,
        ImmutableDictionary<string, string>? cookies = null) =>
        new(
            Method: SimulatedHttpMethods.Post,
            Host: host,
            Path: path.Length > 0 ? path : "/",
            Query: ImmutableDictionary<string, string>.Empty,
            Headers: ImmutableDictionary<string, string>.Empty,
            Cookies: cookies ?? ImmutableDictionary<string, string>.Empty,
            RouteParams: ImmutableDictionary<string, string>.Empty,
            FormBody: form);
}

/// <summary>
/// Stable HTTP status code constants used by the simulation.
/// </summary>
public static class SimulatedHttpStatus
{
    public const int Ok                = 200;
    public const int Created           = 201;
    public const int Found             = 302;
    public const int SeeOther          = 303;
    public const int PermanentRedirect = 301;
    public const int BadRequest        = 400;
    public const int Unauthorized      = 401;
    public const int Forbidden         = 403;
    public const int NotFound          = 404;
    public const int InternalError     = 500;
}

/// <summary>
/// Immutable response from a simulated website controller.
/// Content is a structured <see cref="SimulatedPage"/> rather than raw HTML.
/// A redirect response carries a <see cref="RedirectUrl"/> and no page.
/// </summary>
public sealed record SimulatedHttpResponse(
    int StatusCode,
    SimulatedPage? Page,
    string? RedirectUrl,
    ImmutableDictionary<string, string> SetCookies,
    ImmutableDictionary<string, string> Headers)
{
    /// <summary>True when this response asks the client to follow a redirect.</summary>
    public bool IsRedirect =>
        StatusCode is SimulatedHttpStatus.Found
            or SimulatedHttpStatus.SeeOther
            or SimulatedHttpStatus.PermanentRedirect
        && RedirectUrl is not null;

    /// <summary>Convenience factory for a plain-content 200 response.</summary>
    public static SimulatedHttpResponse Ok(SimulatedPage page,
        ImmutableDictionary<string, string>? setCookies = null) =>
        new(
            StatusCode: SimulatedHttpStatus.Ok,
            Page: page,
            RedirectUrl: null,
            SetCookies: setCookies ?? ImmutableDictionary<string, string>.Empty,
            Headers: ImmutableDictionary<string, string>.Empty);

    /// <summary>Convenience factory for a redirect response.</summary>
    public static SimulatedHttpResponse Redirect(string url, bool permanent = false,
        ImmutableDictionary<string, string>? setCookies = null) =>
        new(
            StatusCode: permanent ? SimulatedHttpStatus.PermanentRedirect : SimulatedHttpStatus.Found,
            Page: null,
            RedirectUrl: url,
            SetCookies: setCookies ?? ImmutableDictionary<string, string>.Empty,
            Headers: ImmutableDictionary<string, string>.Empty);

    /// <summary>Convenience factory for an error page response.</summary>
    public static SimulatedHttpResponse Error(int statusCode, string message) =>
        new(
            StatusCode: statusCode,
            Page: SimulatedPage.Error(statusCode, message),
            RedirectUrl: null,
            SetCookies: ImmutableDictionary<string, string>.Empty,
            Headers: ImmutableDictionary<string, string>.Empty);

    /// <summary>Convenience factory for a 404 Not Found response.</summary>
    public static SimulatedHttpResponse NotFound(string path) =>
        Error(SimulatedHttpStatus.NotFound, $"The page '{path}' was not found on this server.");
}
