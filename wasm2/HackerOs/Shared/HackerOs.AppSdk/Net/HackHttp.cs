using System.Net.Http;
using System.Text;
using HackerOs.Simulation.Abstractions.Network;

namespace HackerOs.AppSdk.Net;

/// <summary>A cookie of a <see cref="HackCookieContainer"/>.</summary>
/// <param name="Domain">Host the cookie belongs to, e.g. <c>hackmail.com</c>.</param>
/// <param name="Name">Cookie name.</param>
/// <param name="Value">Cookie value.</param>
public sealed record HackCookie(string Domain, string Name, string Value);

/// <summary>One HTTP request as the gateway receives it; apps build it through <see cref="HackHttpClient"/>.</summary>
/// <param name="Method">HTTP method, upper case (<c>GET</c>, <c>HEAD</c>, <c>POST</c>…).</param>
/// <param name="RequestUri">Absolute <c>http</c> or <c>https</c> URI.</param>
/// <param name="Headers">Request headers.</param>
/// <param name="Form">URL-encoded form fields of a <c>POST</c>, or <see langword="null"/>.</param>
/// <param name="Cookies">Every cookie of the caller's container; the OS sends those of the target host.</param>
public sealed record HackHttpRequest(
    string Method,
    Uri RequestUri,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    IReadOnlyList<KeyValuePair<string, string>>? Form,
    IReadOnlyList<HackCookie> Cookies);

/// <summary>
/// An HTTP response, the parts of <see cref="HttpResponseMessage"/> HackerOS apps use, plus the
/// structured page a simulated site returns.
/// </summary>
/// <param name="StatusCode">Status code, e.g. 200.</param>
/// <param name="ReasonPhrase">Reason phrase, e.g. <c>OK</c>.</param>
/// <param name="RequestUri">Final URI, after the redirects.</param>
/// <param name="Headers">Response headers.</param>
/// <param name="Content">Body bytes; empty for <c>HEAD</c>. A simulated page's body is its text rendering.</param>
/// <param name="Page">Structured page of a simulated site, which the browser renders; <see langword="null"/> otherwise.</param>
/// <param name="RedirectCount">Number of redirects followed.</param>
/// <param name="SetCookies">Cookies the exchange set, already stored in the client's container.</param>
public sealed record HackHttpResponse(
    int StatusCode,
    string ReasonPhrase,
    Uri RequestUri,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    byte[] Content,
    SimulatedPage? Page,
    int RedirectCount,
    IReadOnlyList<HackCookie> SetCookies)
{
    /// <summary>Gets whether <see cref="StatusCode"/> is in the 200–299 range.</summary>
    public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;

    /// <summary>Decodes <see cref="Content"/> as UTF-8.</summary>
    public string ReadAsString() => Encoding.UTF8.GetString(Content);

    /// <summary>Throws when <see cref="IsSuccessStatusCode"/> is <see langword="false"/>, like <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>.</summary>
    /// <exception cref="HttpRequestException">The status code is not a success.</exception>
    public HackHttpResponse EnsureSuccessStatusCode() =>
        IsSuccessStatusCode
            ? this
            : throw new HttpRequestException(
                $"Response status code does not indicate success: {StatusCode} ({ReasonPhrase}).",
                null,
                (System.Net.HttpStatusCode)StatusCode);

    /// <summary>Gets the standard reason phrase of a status code.</summary>
    public static string GetReasonPhrase(int statusCode) => statusCode switch
    {
        200 => "OK",
        201 => "Created",
        204 => "No Content",
        301 => "Moved Permanently",
        302 => "Found",
        303 => "See Other",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        _ => string.Empty
    };
}

/// <summary>
/// Thrown by <see cref="HackHttpClient"/> when no response came back: unknown host, unreachable
/// host, too many redirects. A response with an error status (404, 500) is not an exception.
/// </summary>
public sealed class HackHttpRequestException : HttpRequestException
{
    /// <summary>Initializes the exception.</summary>
    public HackHttpRequestException(HackNetworkError networkError, HttpRequestError httpRequestError, string message)
        : base(httpRequestError, message)
    {
        NetworkError = networkError;
    }

    /// <summary>Gets why the request failed.</summary>
    public HackNetworkError NetworkError { get; }

    /// <summary>Gets the code a browser prints for this failure, e.g. <c>ERR_NAME_NOT_RESOLVED</c>.</summary>
    public string ErrorCode => HackNetworkErrorCodes.ToCode(NetworkError);
}

/// <summary>
/// Cookie store of an HTTP client, modeled on <see cref="System.Net.CookieContainer"/>: the app
/// keeps it (a browser has one per tab) and the OS reads and fills it on each exchange.
/// </summary>
/// <remarks>Thread-safe. Cookies are kept by host and name, for the life of the container.</remarks>
public sealed class HackCookieContainer
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Domain, string Name), string> _cookies = [];

    /// <summary>Gets the number of cookies.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _cookies.Count;
            }
        }
    }

    /// <summary>Adds or replaces a cookie.</summary>
    public void Add(HackCookie cookie)
    {
        ArgumentNullException.ThrowIfNull(cookie);
        lock (_gate)
        {
            _cookies[(cookie.Domain.ToLowerInvariant(), cookie.Name)] = cookie.Value;
        }
    }

    /// <summary>Gets the cookies sent to <paramref name="uri"/>'s host.</summary>
    public IReadOnlyList<HackCookie> GetCookies(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        string host = uri.Host.ToLowerInvariant();
        return [.. GetAllCookies().Where(cookie => cookie.Domain == host)];
    }

    /// <summary>Gets every cookie, ordered by host then name.</summary>
    public IReadOnlyList<HackCookie> GetAllCookies()
    {
        lock (_gate)
        {
            return [.. _cookies
                .OrderBy(pair => pair.Key.Domain, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.Name, StringComparer.Ordinal)
                .Select(pair => new HackCookie(pair.Key.Domain, pair.Key.Name, pair.Value))];
        }
    }

    /// <summary>Removes every cookie.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _cookies.Clear();
        }
    }
}

/// <summary>
/// HTTP client, modeled on <see cref="HttpClient"/> (ADR 0056, section 6). Needs
/// <c>network.simulated.read</c> for <c>GET</c> and <c>HEAD</c>, <c>network.simulated.write</c>
/// for any other method.
/// </summary>
/// <remarks>
/// <para>
/// Redirects are followed (at most 10); a <c>POST</c> answered by 301, 302 or 303 continues as a
/// <c>GET</c>, as browsers and <see cref="HttpClient"/> do. Cookies set along the way land in
/// <see cref="Cookies"/>.
/// </para>
/// <para>
/// A host outside the simulated network is reached through the optional server when the app holds
/// <c>network.real.access</c> (<c>GET</c> and <c>HEAD</c> only); otherwise it is "not found".
/// The app never learns whether a server exists.
/// </para>
/// </remarks>
public sealed class HackHttpClient : IDisposable
{
    private bool _disposed;

    /// <summary>Initializes a client with its own empty cookie container.</summary>
    public HackHttpClient()
        : this(new HackCookieContainer())
    {
    }

    /// <summary>Initializes a client sharing <paramref name="cookies"/>.</summary>
    public HackHttpClient(HackCookieContainer cookies)
    {
        Cookies = cookies ?? throw new ArgumentNullException(nameof(cookies));
    }

    /// <summary>Gets the cookie container read and filled by every exchange.</summary>
    public HackCookieContainer Cookies { get; }

    /// <summary>Gets the headers added to every request.</summary>
    public IDictionary<string, string> DefaultRequestHeaders { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["User-Agent"] = "HackerOS/1.0" };

    /// <summary>Sends a <c>GET</c> request.</summary>
    /// <exception cref="HackHttpRequestException">No response came back.</exception>
    /// <exception cref="UnauthorizedAccessException">The app lacks <c>network.simulated.read</c>.</exception>
    public Task<HackHttpResponse> GetAsync(string requestUri, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, requestUri, form: null, cancellationToken);

    /// <summary>Sends a <c>GET</c> request and returns the body as text, throwing on an error status.</summary>
    /// <exception cref="HttpRequestException">No response came back, or its status is not a success.</exception>
    public async Task<string> GetStringAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        HackHttpResponse response = await GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        return response.EnsureSuccessStatusCode().ReadAsString();
    }

    /// <summary>Sends a <c>POST</c> request with URL-encoded form fields (<see cref="FormUrlEncodedContent"/>).</summary>
    /// <exception cref="HackHttpRequestException">No response came back.</exception>
    /// <exception cref="UnauthorizedAccessException">The app lacks <c>network.simulated.write</c>.</exception>
    public Task<HackHttpResponse> PostAsync(
        string requestUri, IEnumerable<KeyValuePair<string, string>> form, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(form);
        return SendAsync(HttpMethod.Post, requestUri, [.. form], cancellationToken);
    }

    /// <summary>Sends a request with any method.</summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="requestUri">Absolute <c>http</c> or <c>https</c> URI.</param>
    /// <param name="form">URL-encoded form fields, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <exception cref="ArgumentException"><paramref name="requestUri"/> is not an absolute http(s) URI.</exception>
    /// <exception cref="HackHttpRequestException">No response came back.</exception>
    /// <exception cref="UnauthorizedAccessException">The app lacks the capability for <paramref name="method"/>.</exception>
    public async Task<HackHttpResponse> SendAsync(
        HttpMethod method,
        string requestUri,
        IReadOnlyList<KeyValuePair<string, string>>? form = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(method);
        Uri uri = ParseUri(requestUri);

        HackHttpRequest request = new(
            method.Method.ToUpperInvariant(),
            uri,
            [.. DefaultRequestHeaders],
            form,
            Cookies.GetAllCookies());

        HackNetworkResult<HackHttpResponse> result = await HackNetworkErrorTranslator
            .CallAsync(gateway => gateway.SendAsync(request, cancellationToken)).ConfigureAwait(false);
        HackHttpResponse response = HackNetworkErrorTranslator.UnwrapHttp(result, uri);
        foreach (HackCookie cookie in response.SetCookies)
        {
            Cookies.Add(cookie);
        }

        return response;
    }

    /// <summary>Marks the client disposed; the cookie container stays usable by its other owners.</summary>
    public void Dispose() => _disposed = true;

    private static Uri ParseUri(string requestUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestUri);
        if (!Uri.TryCreate(requestUri, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"'{requestUri}' is not an absolute http or https URI.", nameof(requestUri));
        }

        return uri;
    }
}
