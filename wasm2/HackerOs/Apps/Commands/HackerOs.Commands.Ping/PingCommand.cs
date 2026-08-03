using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.Network;

namespace HackerOs.Commands.Ping;

/// <summary>
/// Simulated <c>ping</c> terminal command (P4-W4-006).
/// Resolves a hostname or IP through the simulated DNS and reports
/// the synthetic latency from the host record. Makes zero real network calls.
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
        Capabilities = [AppCapabilities.NetworkSimulatedRead],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("ping", [], "ping <hostname|ip>"),
        SingleInstancePerUser = false
    };

    private readonly ISimulatedNetworkService _network;

    /// <summary>Initializes the command with its manifest and the simulated network service.</summary>
    public PingCommand(AppManifest manifest, ISimulatedNetworkService network) : base(manifest)
    {
        _network = network;
    }

    /// <inheritdoc/>
    public override ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Arguments.Count == 0)
        {
            context.StandardError.WriteLine("ping: usage: ping <hostname|ip>");
            return ValueTask.FromResult(1);
        }

        var target = context.Arguments[0];

        // Resolve DNS first so we can report the IP
        var ip = _network.Dns.Resolve(target) ?? target;
        var host = _network.GetHost(target);

        if (host is null)
        {
            context.StandardOutput.WriteLine($"ping: cannot resolve '{target}': Simulated name or service not known");
            return ValueTask.FromResult(2);
        }

        context.StandardOutput.WriteLine($"PING {target} ({ip}) 56(84) bytes of data.");

        if (!host.IsUp)
        {
            context.StandardOutput.WriteLine($"From {ip} icmp_seq=1 Destination Host Unreachable");
            context.StandardOutput.WriteLine();
            context.StandardOutput.WriteLine($"--- {target} ping statistics ---");
            context.StandardOutput.WriteLine("4 packets transmitted, 0 received, 100% packet loss");
            return ValueTask.FromResult(1);
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

        return ValueTask.FromResult(0);
    }
}
