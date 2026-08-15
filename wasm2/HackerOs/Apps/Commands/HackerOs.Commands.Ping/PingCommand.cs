using System.Net.Http;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Server.Contracts.Proxy;
using HackerOs.Simulation.Abstractions.Network;
using HackerOs.Simulation.Abstractions.ServerConnection;

namespace HackerOs.Commands.Ping;

/// <summary>
/// Simulated <c>ping</c> terminal command (P4-W4-006), with a real-network fallback (ADR 0028): a
/// target unknown to the simulated network falls back to an HTTP HEAD proxy round-trip (through the
/// optional server, when connected) to approximate reachability/latency, rather than only reporting
/// "cannot resolve." The simulated network stays authoritative for any target it recognizes.
/// </summary>
public sealed class PingCommand : TerminalAppBase
{
    /// <summary>Static manifest for test validation without a DI container.</summary>
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.ping",
        Name = "ping",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Simulated network ping command",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Ping.dll", "HackerOs.Commands.Ping.PingCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("network", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.NetworkSimulatedRead, AppCapabilities.NetworkRealAccess],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("ping", [], "ping <hostname|ip>"),
        SingleInstancePerUser = false
    };

    private readonly ISimulatedNetworkService _network;
    private readonly IServerConnectionService _connection;
    private readonly IProxyClient _proxy;

    /// <summary>Initializes the command with its manifest, the simulated network, and the optional real-network bridge.</summary>
    public PingCommand(
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

        if (context.Arguments.Count == 0)
        {
            context.StandardError.WriteLine("ping: usage: ping <hostname|ip>");
            return 1;
        }

        var target = context.Arguments[0];

        // Resolve DNS first so we can report the IP
        var ip = _network.Dns.Resolve(target) ?? target;
        var host = _network.GetHost(target);

        if (host is null)
        {
            return await PingRealHostAsync(context, target, cancellationToken).ConfigureAwait(false);
        }

        context.StandardOutput.WriteLine($"PING {target} ({ip}) 56(84) bytes of data.");

        if (!host.IsUp)
        {
            context.StandardOutput.WriteLine($"From {ip} icmp_seq=1 Destination Host Unreachable");
            context.StandardOutput.WriteLine();
            context.StandardOutput.WriteLine($"--- {target} ping statistics ---");
            context.StandardOutput.WriteLine("4 packets transmitted, 0 received, 100% packet loss");
            return 1;
        }

        // Simulate 4 ping replies with slight jitter (deterministic + offset)
        double baseMs = host.LatencyMs;
        for (int i = 1; i <= 4; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Deterministic jitter: ±5% of latency, cycling through i
            double jitter = baseMs * 0.05 * ((i % 3) - 1);
            double ms = Math.Round(baseMs + jitter, 3);

            context.StandardOutput.WriteLine(
                $"64 bytes from {ip}: icmp_seq={i} ttl=64 time={ms} ms");
        }

        context.StandardOutput.WriteLine();
        context.StandardOutput.WriteLine($"--- {target} ping statistics ---");
        context.StandardOutput.WriteLine(
            $"4 packets transmitted, 4 received, 0% packet loss, time 3003ms");
        context.StandardOutput.WriteLine(
            $"rtt min/avg/max/mdev = {Math.Round(baseMs * 0.95, 3)}/{Math.Round(baseMs, 3)}/{Math.Round(baseMs * 1.05, 3)}/0.125 ms");

        return 0;
    }

    /// <summary>
    /// Real-network fallback (ADR 0028) for a target the simulated network doesn't recognize: an
    /// HTTP HEAD proxy round-trip through the optional server, when connected, approximating ICMP
    /// reachability/latency. The proxy contract is HTTP-shaped, not ICMP — this is a best-effort
    /// approximation, not a real ping; see docs/server-implementation-pass.md for the open question
    /// on whether ping/nmap eventually need a non-HTTP proxy call shape.
    /// </summary>
    private async ValueTask<int> PingRealHostAsync(TerminalExecutionContext context, string target, CancellationToken cancellationToken)
    {
        ServerConnectionState? state = await _connection.GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            context.StandardOutput.WriteLine($"ping: cannot resolve '{target}': Simulated name or service not known");
            return 2;
        }

        string? accessToken = await _connection.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (accessToken is null)
        {
            context.StandardOutput.WriteLine($"ping: cannot resolve '{target}': Simulated name or service not known");
            return 2;
        }

        string url = target.Contains("://", StringComparison.Ordinal) ? target : $"https://{target}";
        context.StandardOutput.WriteLine($"PING {target} (via optional server proxy) 0(0) bytes of data.");

        for (int i = 1; i <= 4; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                ProxyHttpResponse response = await _proxy.ExecuteHttpRequestAsync(
                    new Uri(state.ServerBaseUrl),
                    accessToken,
                    new ProxyHttpRequest(Guid.NewGuid(), ProxyProtocol.Http, url, "HEAD", [], null, 0, 10, Manifest.Id),
                    cancellationToken).ConfigureAwait(false);
                context.StandardOutput.WriteLine(
                    $"Reply from {url}: status={response.StatusCode} time={response.DurationMs}ms");
            }
            catch (Exception exception) when (exception is ServerConnectionException or HttpRequestException)
            {
                context.StandardOutput.WriteLine($"Request timeout for icmp_seq {i} ({exception.Message})");
            }
        }

        context.StandardOutput.WriteLine();
        context.StandardOutput.WriteLine($"--- {target} ping statistics (via optional server proxy) ---");
        return 0;
    }
}
