using System.Net.Http;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Net;

namespace HackerOs.Commands.Curl;

/// <summary>
/// Simulated <c>curl</c> terminal command (P4-W4-006) on <see cref="HackHttpClient"/>
/// (ADR 0056, section 6). Supports <c>-I</c> (HEAD, headers only), <c>-v</c> (verbose),
/// <c>-X</c> (method), <c>-d</c> (URL-encoded POST data) and <c>-L</c> (accepted; redirects are
/// always followed). A host outside the simulated network is reached by the OS through the
/// optional server when it can (GET and HEAD, ADR 0028/0034); otherwise curl reports
/// "Could not resolve host", exit code 6. A response status of 400 or more prints no body and
/// exits with 22, as <c>curl --fail</c> does.
/// </summary>
public sealed class CurlCommand : TerminalAppBase
{
    private readonly HackCookieContainer _cookies = new();

    /// <summary>Initializes the command with its manifest.</summary>
    public CurlCommand(AppManifest manifest) : base(manifest)
    {
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        // Parse arguments: curl [-I] [-v] [-L] [-X <method>] [-d <data>] <url>
        string? url = null;
        bool headersOnly = false;
        bool verbose = false;
        string? method = null;
        string? postData = null;

        IReadOnlyList<string> args = context.Arguments;
        for (int i = 0; i < args.Count; i++)
        {
            string a = args[i];
            switch (a)
            {
                case "-I" or "--head": headersOnly = true; break;
                case "-v" or "--verbose": verbose = true; break;
                case "-L" or "--location": /* redirects are always followed */ break;
                case "-X" or "--request":
                    if (i + 1 < args.Count) method = args[++i].ToUpperInvariant();
                    break;
                case "-d" or "--data":
                    if (i + 1 < args.Count) postData = args[++i];
                    break;
                default:
                    if (!a.StartsWith('-')) url = a;
                    break;
            }
        }

        if (url is null)
        {
            context.StandardError.WriteLine("curl: no URL specified!\ncurl: try 'curl --help' for more information");
            return 1;
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            context.StandardError.WriteLine($"curl: (3) URL using bad/illegal format or missing URL");
            return 3;
        }

        method ??= postData is not null ? "POST" : headersOnly ? "HEAD" : "GET";
        if (verbose)
        {
            context.StandardOutput.WriteLine($"*   Trying {uri.Host}:{uri.Port}...");
            context.StandardOutput.WriteLine($"> {method} {uri.PathAndQuery} HTTP/1.1");
            context.StandardOutput.WriteLine($"> Host: {uri.Host}");
            context.StandardOutput.WriteLine("> User-Agent: curl/7.88.1");
        }

        HackHttpResponse response;
        try
        {
            using HackHttpClient client = new(_cookies);
            client.DefaultRequestHeaders["User-Agent"] = "curl/7.88.1";
            response = await client.SendAsync(
                new HttpMethod(method), uri.AbsoluteUri, postData is null ? null : ParseForm(postData), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HackHttpRequestException exception)
        {
            (int code, string text) = exception.NetworkError switch
            {
                HackNetworkError.HostNotFound => (6, $"Could not resolve host: {uri.Host}"),
                HackNetworkError.TooManyRedirects => (47, "Maximum (10) redirects followed"),
                _ => (7, $"Failed to connect to {uri.Host} port {uri.Port}: {exception.Message}")
            };
            context.StandardError.WriteLine($"curl: ({code}) {text}");
            return code;
        }

        if (verbose)
        {
            if (response.RedirectCount > 0)
                context.StandardOutput.WriteLine($"* Followed {response.RedirectCount} redirect(s) to {response.RequestUri}");
            context.StandardOutput.WriteLine($"< HTTP/1.1 {response.StatusCode} {response.ReasonPhrase}");
        }

        if (headersOnly)
        {
            context.StandardOutput.WriteLine($"HTTP/1.1 {response.StatusCode} {response.ReasonPhrase}".TrimEnd());
            foreach (KeyValuePair<string, string> header in response.Headers)
            {
                context.StandardOutput.WriteLine($"{header.Key}: {header.Value}");
            }

            return 0;
        }

        if (!response.IsSuccessStatusCode && response.StatusCode >= 400)
        {
            context.StandardError.WriteLine($"curl: (22) The requested URL returned error: {response.StatusCode}");
            return 22;
        }

        string body = response.ReadAsString();
        context.StandardOutput.Write(body);
        if (body.Length > 0 && !body.EndsWith('\n'))
        {
            context.StandardOutput.WriteLine();
        }

        return 0;
    }

    /// <summary>Parses <c>key=value&amp;key2=value2</c> into form fields, unescaping both sides.</summary>
    private static List<KeyValuePair<string, string>> ParseForm(string data)
    {
        List<KeyValuePair<string, string>> fields = [];
        foreach (string pair in data.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = pair.IndexOf('=');
            if (separator > 0)
            {
                fields.Add(new(Uri.UnescapeDataString(pair[..separator]), Uri.UnescapeDataString(pair[(separator + 1)..])));
            }
        }

        return fields;
    }
}
