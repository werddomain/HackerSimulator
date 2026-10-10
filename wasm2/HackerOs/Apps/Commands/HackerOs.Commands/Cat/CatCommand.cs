using System.Net.Http;
using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Server.Contracts.Proxy;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Network;
using HackerOs.Simulation.Abstractions.ServerConnection;

namespace HackerOs.Commands.Cat;

/// <summary>
/// Implements the <c>cat</c> terminal command (`P2-CMD-004`). An argument starting with
/// <c>http://</c> or <c>https://</c> is read as a URL instead of a virtual-filesystem path —
/// ADR 0023 named `cat` as a network command alongside <c>ping</c>/<c>curl</c>, but this was never
/// actually built until now; every other argument keeps its original VFS-only behavior unchanged.
/// A URL known to the simulated network is fetched and rendered the same way <c>curl</c> renders a
/// page (<see cref="SimulatedPageTextFormatter"/>); an unrecognized host falls back to a real HTTP
/// GET through the optional server, when connected, printing the fetched body — mirroring
/// <c>CurlCommand</c>'s own real-network fallback exactly.
/// </summary>
public sealed class CatCommand : TerminalAppBase
{
    private readonly ISimulatedNetworkService _network;
    private readonly IServerConnectionService _connection;
    private readonly IProxyClient _proxy;
    private readonly Dictionary<string, Dictionary<string, string>> _sessionCookies = [];

    /// <summary>Initializes the command with its manifest, the simulated network, and the optional real-network bridge.</summary>
    public CatCommand(
        AppManifest manifest,
        ISimulatedNetworkService network,
        IServerConnectionService connection,
        IProxyClient proxy) : base(manifest)
    {
        _network = network;
        _connection = connection;
        _proxy = proxy;
    }

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Arguments.Count == 0)
        {
            context.StandardError.WriteLine("cat: missing file argument");
            return 1;
        }

        int exitCode = 0;
        foreach (string fileArg in context.Arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsUrl(fileArg))
            {
                int urlExitCode = await CatUrlAsync(context, fileArg, cancellationToken).ConfigureAwait(false);
                if (urlExitCode != 0)
                {
                    exitCode = urlExitCode;
                }
                continue;
            }

            string resolvedPath = ResolvePath(context.WorkingDirectory, fileArg);
            FileSystemResult<FileSystemContentReadHandle> readResult = await context.App.FileSystem.ReadAsync(
                new FileSystemReadRequest(VirtualPath.Parse(resolvedPath)), cancellationToken);

            if (!readResult.Succeeded || readResult.Value is null)
            {
                if (readResult.Error?.Code == FileSystemErrorCode.PermissionDenied || readResult.Error?.Code == FileSystemErrorCode.CapabilityDenied)
                {
                    context.StandardError.WriteLine($"cat: {fileArg}: Permission denied");
                }
                else
                {
                    context.StandardError.WriteLine($"cat: {fileArg}: No such file or directory");
                }
                exitCode = 1;
            }
            else
            {
                await using FileSystemContentReadHandle handle = readResult.Value;
                using StreamReader reader = new(handle.Content, Encoding.UTF8, leaveOpen: true);
                string text = await reader.ReadToEndAsync(cancellationToken);
                context.StandardOutput.Write(text);
                if (!text.EndsWith('\n'))
                {
                    context.StandardOutput.WriteLine();
                }
            }
        }

        return exitCode;
    }

    private static bool IsUrl(string arg) =>
        arg.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads <paramref name="url"/> as content: the simulated network if it recognizes the host,
    /// otherwise a real-network fallback (see <see cref="CatRealUrlAsync"/>).
    /// </summary>
    private async ValueTask<int> CatUrlAsync(TerminalExecutionContext context, string url, CancellationToken cancellationToken)
    {
        Uri uri = new(url);
        if (_network.GetHost(uri.Host) is null)
        {
            return await CatRealUrlAsync(context, url, cancellationToken).ConfigureAwait(false);
        }

        SimulatedNavigationResult result = _network.Navigate(url, _sessionCookies);

        if (result.NetworkError is not null)
        {
            context.StandardError.WriteLine($"cat: {url}: {result.NetworkError}");
            return 1;
        }

        if (result.Page is not null)
        {
            SimulatedPageTextFormatter.WriteTo(context.StandardOutput, result.Page);
        }

        return result.StatusCode < 400 ? 0 : 1;
    }

    /// <summary>
    /// Real-network fallback for a URL unknown to the simulated network: a real HTTP GET proxy
    /// round-trip through the optional server, when connected, with the response body fetched and
    /// printed — the same <see cref="IProxyClient"/> <c>IncludeBody</c> path <c>CurlCommand</c> uses.
    /// </summary>
    private async ValueTask<int> CatRealUrlAsync(TerminalExecutionContext context, string url, CancellationToken cancellationToken)
    {
        ServerConnectionState? state = await _connection.GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            context.StandardError.WriteLine($"cat: {url}: Could not resolve host");
            return 1;
        }

        string? accessToken = await _connection.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (accessToken is null)
        {
            context.StandardError.WriteLine($"cat: {url}: Could not resolve host");
            return 1;
        }

        try
        {
            ProxyHttpResponse response = await _proxy.ExecuteHttpRequestAsync(
                new Uri(state.ServerBaseUrl),
                accessToken,
                new ProxyHttpRequest(Guid.NewGuid(), ProxyProtocol.Http, url, "GET", [], null, 0, 10, Manifest.Id, IncludeBody: true),
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode >= 400)
            {
                context.StandardError.WriteLine($"cat: {url}: {response.StatusCode} {response.ReasonPhrase}");
                return 1;
            }

            if (response.BodyBase64 is not null)
            {
                byte[] bodyBytes = Convert.FromBase64String(response.BodyBase64);
                string text = Encoding.UTF8.GetString(bodyBytes);
                context.StandardOutput.Write(text);
                if (!text.EndsWith('\n'))
                {
                    context.StandardOutput.WriteLine();
                }
            }

            return 0;
        }
        catch (Exception exception) when (exception is ServerConnectionException or HttpRequestException)
        {
            context.StandardError.WriteLine($"cat: {url}: {exception.Message}");
            return 1;
        }
    }

    private static string ResolvePath(string cwd, string target)
    {
        if (target.StartsWith('/'))
        {
            return NormalizePath(target);
        }
        return NormalizePath($"{cwd.TrimEnd('/')}/{target}");
    }

    private static string NormalizePath(string rawPath)
    {
        string[] segments = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Stack<string> stack = new();

        foreach (string segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }
            if (segment == "..")
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
            }
            else
            {
                stack.Push(segment);
            }
        }

        return "/" + string.Join('/', stack.ToArray().Reverse());
    }
}
