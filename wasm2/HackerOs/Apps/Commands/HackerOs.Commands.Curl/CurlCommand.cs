using System.Collections.Immutable;
using System.Net.Http;
using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Server.Contracts.Proxy;
using HackerOs.Simulation.Abstractions.Network;
using HackerOs.Simulation.Abstractions.ServerConnection;

namespace HackerOs.Commands.Curl;

/// <summary>
/// Simulated <c>curl</c> terminal command (P4-W4-006), with a real-network fallback for
/// <c>-I</c> (ADR 0028/ADR 0034 Pass N+1a): a host unknown to the simulated network gets a real
/// HTTP HEAD proxy round-trip (through the optional server, when connected) instead of only
/// "Could not resolve host." Full-body <c>curl</c> (no <c>-I</c>) against an unrecognized host
/// stays simulation-only — <see cref="IProxyClient"/> is metadata-only until the server-side
/// proxy body-transfer gap closes (see docs/server-implementation-pass.md). The simulated
/// network stays authoritative for any host it recognizes, real or not.
/// Supports -I (headers only), -v (verbose), -X (method), -d (POST data),
/// -L (follow redirects — always followed for navigation, this flag is a no-op).
/// </summary>
public sealed class CurlCommand : TerminalAppBase
{
    /// <summary>Static manifest for test validation without a DI container.</summary>
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.curl",
        Name = "curl",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Simulated URL data transfer command",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Curl.dll", "HackerOs.Commands.Curl.CurlCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("network", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.NetworkSimulatedRead, AppCapabilities.NetworkSimulatedWrite, AppCapabilities.NetworkRealAccess],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("curl", [], "curl [-I] [-v] [-L] [-X <method>] [-d <data>] <url>"),
        SingleInstancePerUser = false
    };

    private readonly ISimulatedNetworkService _network;
    private readonly IServerConnectionService _connection;
    private readonly IProxyClient _proxy;
    private readonly Dictionary<string, Dictionary<string, string>> _sessionCookies = [];

    /// <summary>Initializes the command with its manifest, the simulated network, and the optional real-network bridge.</summary>
    public CurlCommand(
        AppManifest manifest,
        ISimulatedNetworkService network,
        IServerConnectionService connection,
        IProxyClient proxy) : base(manifest)
    {
        _network = network;
        _connection = connection;
        _proxy = proxy;
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        // Parse arguments: curl [-I] [-v] [-L] [-X <method>] [-d <data>] <url>
        string? url      = null;
        bool headersOnly = false;
        bool verbose     = false;
        string method    = SimulatedHttpMethods.Get;
        string? postData = null;

        var args = context.Arguments;
        for (int i = 0; i < args.Count; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-I": headersOnly = true;  break;
                case "-v": verbose     = true;  break;
                case "-L": /* follow redirects — always done */  break;
                case "-X" or "--request":
                    if (i + 1 < args.Count) method = args[++i].ToUpperInvariant();
                    break;
                case "-d" or "--data":
                    if (i + 1 < args.Count) { postData = args[++i]; method = SimulatedHttpMethods.Post; }
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

        // Normalize URL
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;

        if (verbose)
        {
            context.StandardOutput.WriteLine($"* Trying simulated network...");
            context.StandardOutput.WriteLine($"> {method} {new Uri(url).PathAndQuery} HTTP/1.1");
            context.StandardOutput.WriteLine($"> Host: {new Uri(url).Host}");
            context.StandardOutput.WriteLine($"> User-Agent: curl/7.88.1");
        }

        if (headersOnly && _network.GetHost(new Uri(url).Host) is null)
        {
            return await CurlRealHostHeadAsync(context, url, cancellationToken).ConfigureAwait(false);
        }

        SimulatedNavigationResult result;

        if (method == SimulatedHttpMethods.Post && postData is not null)
        {
            // Parse form data (key=value&key2=value2)
            var formBuilder = ImmutableDictionary.CreateBuilder<string, string>();
            foreach (var pair in postData.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIdx = pair.IndexOf('=');
                if (eqIdx > 0)
                    formBuilder[Uri.UnescapeDataString(pair[..eqIdx])] =
                        Uri.UnescapeDataString(pair[(eqIdx + 1)..]);
            }

            var resp = _network.Post(url, formBuilder.ToImmutable(), _sessionCookies);

            if (verbose)
            {
                context.StandardOutput.WriteLine($"< HTTP/1.1 {resp.StatusCode}");
            }

            if (headersOnly)
            {
                context.StandardOutput.WriteLine($"HTTP/1.1 {resp.StatusCode}");
                context.StandardOutput.WriteLine($"Content-Type: text/simulated");
                return resp.StatusCode < 400 ? 0 : 1;
            }

            if (resp.Page is not null)
                PrintPage(context, resp.Page);
            else if (resp.RedirectUrl is not null)
                context.StandardOutput.WriteLine($"<Redirect: {resp.RedirectUrl}>");

            return resp.StatusCode < 400 ? 0 : 1;
        }

        result = _network.Navigate(url, _sessionCookies);

        if (verbose)
        {
            if (result.RedirectCount > 0)
                context.StandardOutput.WriteLine($"* Followed {result.RedirectCount} redirect(s)");
            context.StandardOutput.WriteLine($"< HTTP/1.1 {result.StatusCode}");
        }

        if (result.NetworkError is not null)
        {
            context.StandardError.WriteLine($"curl: ({result.NetworkError}) Could not resolve host: {url}");
            return 6;
        }

        if (headersOnly)
        {
            context.StandardOutput.WriteLine($"HTTP/1.1 {result.StatusCode}");
            context.StandardOutput.WriteLine($"Content-Type: text/simulated");
            context.StandardOutput.WriteLine($"X-Final-Url: {result.FinalUrl}");
            return 0;
        }

        if (result.Page is not null)
            PrintPage(context, result.Page);

        return result.StatusCode < 400 ? 0 : 1;
    }

    /// <summary>
    /// Real-network fallback (ADR 0028/ADR 0034 Pass N+1a) for <c>curl -I</c> against a host the
    /// simulated network doesn't recognize: a single HTTP HEAD proxy round-trip through the
    /// optional server, when connected. <see cref="IProxyClient"/> is metadata-only, so this path
    /// only ever serves <c>-I</c> — a normal body-fetching <c>curl</c> against an unknown host
    /// still reports "Could not resolve host" via the caller's existing <c>NetworkError</c> path.
    /// </summary>
    private async ValueTask<int> CurlRealHostHeadAsync(TerminalExecutionContext context, string url, CancellationToken cancellationToken)
    {
        ServerConnectionState? state = await _connection.GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            context.StandardError.WriteLine($"curl: (6) Could not resolve host: {url}");
            return 6;
        }

        string? accessToken = await _connection.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (accessToken is null)
        {
            context.StandardError.WriteLine($"curl: (6) Could not resolve host: {url}");
            return 6;
        }

        try
        {
            ProxyHttpResponse response = await _proxy.ExecuteHttpRequestAsync(
                new Uri(state.ServerBaseUrl),
                accessToken,
                new ProxyHttpRequest(Guid.NewGuid(), ProxyProtocol.Http, url, "HEAD", [], null, 0, 10, Manifest.Id),
                cancellationToken).ConfigureAwait(false);

            context.StandardOutput.WriteLine($"HTTP/1.1 {response.StatusCode} {response.ReasonPhrase}");
            foreach (ProxyHeader header in response.Headers)
            {
                context.StandardOutput.WriteLine($"{header.Name}: {header.Value}");
            }

            return 0;
        }
        catch (Exception exception) when (exception is ServerConnectionException or HttpRequestException)
        {
            context.StandardError.WriteLine($"curl: (7) Failed to connect to {url}: {exception.Message}");
            return 7;
        }
    }

    // ── Plain-text serialization of structured page content ──────────────

    private static void PrintPage(TerminalExecutionContext context, SimulatedPage page)
    {
        context.StandardOutput.WriteLine($"<!-- Title: {page.Title} -->");
        context.StandardOutput.WriteLine();

        foreach (var section in page.Sections)
        {
            switch (section)
            {
                case HeroSection h:
                    context.StandardOutput.WriteLine($"=== {h.Headline} ===");
                    if (h.Subtitle is not null) context.StandardOutput.WriteLine(h.Subtitle);
                    context.StandardOutput.WriteLine();
                    break;

                case ParagraphSection p:
                    context.StandardOutput.WriteLine(p.Text);
                    context.StandardOutput.WriteLine();
                    break;

                case ListSection l:
                    if (l.Title is not null) context.StandardOutput.WriteLine(l.Title + ":");
                    foreach (var item in l.Items) context.StandardOutput.WriteLine("  * " + item);
                    context.StandardOutput.WriteLine();
                    break;

                case AlertSection a:
                    context.StandardOutput.WriteLine($"[{a.Level.ToString().ToUpperInvariant()}] {a.Message}");
                    context.StandardOutput.WriteLine();
                    break;

                case NavigationSection n:
                    context.StandardOutput.WriteLine(string.Join("  |  ", n.Links.Select(lk => lk.Label)));
                    context.StandardOutput.WriteLine();
                    break;

                case LoginFormSection lf:
                    context.StandardOutput.WriteLine($"[FORM: {lf.Title}]");
                    context.StandardOutput.WriteLine($"  {lf.UsernameLabel}: _______");
                    context.StandardOutput.WriteLine($"  {lf.PasswordLabel}: _______");
                    context.StandardOutput.WriteLine($"  [{lf.SubmitLabel}] -> POST {lf.PostPath}");
                    context.StandardOutput.WriteLine();
                    break;

                case FormSection fs:
                    context.StandardOutput.WriteLine($"[FORM: {fs.Title}]");
                    foreach (var field in fs.Fields)
                        context.StandardOutput.WriteLine($"  {field.Label}: _______");
                    context.StandardOutput.WriteLine($"  [{fs.SubmitLabel}] -> POST {fs.PostPath}");
                    context.StandardOutput.WriteLine();
                    break;

                case ProductGridSection g:
                    foreach (var p in g.Products)
                    {
                        context.StandardOutput.WriteLine($"  [{p.Title}] - {p.Price}");
                        context.StandardOutput.WriteLine($"    {p.Description}");
                    }
                    context.StandardOutput.WriteLine();
                    break;

                case TableSection t:
                    if (t.Title is not null) context.StandardOutput.WriteLine(t.Title);
                    context.StandardOutput.WriteLine(string.Join(" | ", t.Headers));
                    foreach (var row in t.Rows)
                        context.StandardOutput.WriteLine(string.Join(" | ", row));
                    context.StandardOutput.WriteLine();
                    break;

                case ForumSection f:
                    context.StandardOutput.WriteLine($"--- {f.SectionTitle} ---");
                    foreach (var th in f.Threads)
                        context.StandardOutput.WriteLine($"  {(th.IsHot ? "[HOT] " : "")}{th.Title}  [{th.Author}, {th.TimestampDisplay}, {th.Views} views]");
                    context.StandardOutput.WriteLine();
                    break;

                case EmailListSection e:
                    foreach (var em in e.Emails)
                        context.StandardOutput.WriteLine($"  {(em.IsRead ? "" : "[UNREAD] ")}{em.Subject} — {em.Sender}");
                    context.StandardOutput.WriteLine();
                    break;
            }
        }
    }
}
