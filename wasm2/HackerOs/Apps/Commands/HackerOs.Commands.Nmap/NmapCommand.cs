using System.Net.Http;
using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Server.Contracts.Proxy;
using HackerOs.Simulation.Abstractions.Network;
using HackerOs.Simulation.Abstractions.ServerConnection;

namespace HackerOs.Commands.Nmap;

/// <summary>
/// Simulated <c>nmap</c> port-scanner terminal command (P4-W4-006), with a narrow real-network
/// fallback (ADR 0035): when the target is unknown to the simulated network AND the user asked
/// about exactly one port with <c>-p &lt;port&gt;</c>, this issues a single real TCP-connect probe
/// through the optional server instead of only reporting "Host seems down." This is deliberately
/// not a real port-range scanner — no range or multi-port <c>-p</c> ever triggers a real probe,
/// and the simulated network stays authoritative for any host it recognizes.
/// </summary>
public sealed class NmapCommand : TerminalAppBase
{
    /// <summary>Default port range when -p is not specified.</summary>
    private const int DefaultFirstPort = 1;
    private const int DefaultLastPort  = 1000;

    /// <summary>Static manifest for test validation without a DI container.</summary>
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.nmap",
        Name = "nmap",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Simulated network mapper",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Nmap.dll", "HackerOs.Commands.Nmap.NmapCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("network", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.NetworkSimulatedRead, AppCapabilities.NetworkRealAccess],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("nmap", [], "nmap [-p <range>] [-sV] [-O] <target>"),
        SingleInstancePerUser = false
    };

    private readonly ISimulatedNetworkService _network;
    private readonly IServerConnectionService _connection;
    private readonly IProxyClient _proxy;

    /// <summary>Initializes the command with its manifest, the simulated network, and the optional real-network bridge.</summary>
    public NmapCommand(
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

        // Parse arguments: nmap [-p <range>] [-sV] [-O] <target>
        string? targetArg = null;
        int firstPort = DefaultFirstPort;
        int lastPort  = DefaultLastPort;
        bool explicitSinglePort = false;
        bool showVersion = false;
        bool showOs      = false;

        var args = context.Arguments;
        for (int i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a is "-p" or "--port" && i + 1 < args.Count)
            {
                string portArg = args[++i];
                (firstPort, lastPort) = ParsePortRange(portArg);
                explicitSinglePort = firstPort == lastPort && !portArg.Contains('-');
            }
            else if (a is "-sV")
            {
                showVersion = true;
            }
            else if (a is "-O")
            {
                showOs = true;
            }
            else if (!a.StartsWith('-'))
            {
                targetArg = a;
            }
        }

        if (targetArg is null)
        {
            context.StandardError.WriteLine("nmap: usage: nmap [-p <port-range>] [-sV] [-O] <target>");
            return 1;
        }

        var host = _network.GetHost(targetArg);
        var ip   = _network.Dns.Resolve(targetArg) ?? targetArg;

        context.StandardOutput.WriteLine($"\nStarting Nmap 7.94 ( https://nmap.org ) at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");

        if (host is null)
        {
            if (explicitSinglePort)
            {
                return await NmapRealHostPortAsync(context, targetArg, firstPort, cancellationToken).ConfigureAwait(false);
            }

            context.StandardOutput.WriteLine($"Nmap scan report for {targetArg}");
            context.StandardOutput.WriteLine("Note: Host seems down. If it is really up, but blocking our ping probes, try -Pn");
            context.StandardOutput.WriteLine($"Nmap done: 1 IP address (0 hosts up) scanned");
            return 1;
        }

        context.StandardOutput.WriteLine($"Nmap scan report for {targetArg} ({ip})");
        context.StandardOutput.WriteLine($"Host is {'u' + (host.IsUp ? "p" : "nreachable")}.");

        if (!host.IsUp)
        {
            context.StandardOutput.WriteLine($"Nmap done: 1 IP address (0 hosts up) scanned");
            return 1;
        }

        context.StandardOutput.WriteLine($"Not shown: {lastPort - firstPort + 1} closed tcp ports (conn-refused)");

        var ports = _network.ScanPorts(targetArg, firstPort, lastPort);
        if (ports.Count > 0)
        {
            // Print column headers
            context.StandardOutput.WriteLine(showVersion
                ? "PORT      STATE     SERVICE          VERSION"
                : "PORT      STATE     SERVICE");

            foreach (var port in ports)
            {
                if (cancellationToken.IsCancellationRequested) break;

                string portCol    = $"{port.PortNumber}/tcp".PadRight(9);
                string stateCol   = port.State.ToString().ToLowerInvariant().PadRight(9);
                string serviceCol = (port.Service?.Name ?? "unknown").PadRight(16);

                if (showVersion && port.Service?.Version is not null)
                    context.StandardOutput.WriteLine($"{portCol} {stateCol} {serviceCol} {port.Service.Version}");
                else
                    context.StandardOutput.WriteLine($"{portCol} {stateCol} {serviceCol}");
            }
        }

        if (showOs && host.OsFingerprint is not null)
        {
            context.StandardOutput.WriteLine();
            context.StandardOutput.WriteLine("OS detection performed. Please report any incorrect results at https://nmap.org/submit/");
            context.StandardOutput.WriteLine($"OS details: {host.OsFingerprint.Name} {host.OsFingerprint.Version}  (Accuracy: {host.OsFingerprint.Accuracy}%)");
        }

        context.StandardOutput.WriteLine();
        context.StandardOutput.WriteLine($"Nmap done: 1 IP address (1 host up) scanned in {host.LatencyMs * 0.1:F2} seconds");

        return 0;
    }

    /// <summary>
    /// Real-network fallback (ADR 0035) for a target unknown to the simulated network, when the
    /// user asked about exactly one port: a single TCP-connect probe through the optional server,
    /// when connected. Never scans a range — that stays simulation-only.
    /// </summary>
    private async ValueTask<int> NmapRealHostPortAsync(
        TerminalExecutionContext context, string target, int port, CancellationToken cancellationToken)
    {
        ServerConnectionState? state = await _connection.GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            context.StandardOutput.WriteLine($"Nmap scan report for {target}");
            context.StandardOutput.WriteLine("Note: Host seems down. If it is really up, but blocking our ping probes, try -Pn");
            context.StandardOutput.WriteLine($"Nmap done: 1 IP address (0 hosts up) scanned");
            return 1;
        }

        string? accessToken = await _connection.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (accessToken is null)
        {
            context.StandardOutput.WriteLine($"Nmap scan report for {target}");
            context.StandardOutput.WriteLine("Note: Host seems down. If it is really up, but blocking our ping probes, try -Pn");
            context.StandardOutput.WriteLine($"Nmap done: 1 IP address (0 hosts up) scanned");
            return 1;
        }

        try
        {
            ProxyTcpProbeResponse response = await _proxy.ExecuteTcpProbeAsync(
                new Uri(state.ServerBaseUrl),
                accessToken,
                new ProxyTcpProbeRequest(Guid.NewGuid(), target, port, 5, Manifest.Id),
                cancellationToken).ConfigureAwait(false);

            context.StandardOutput.WriteLine($"Nmap scan report for {target} (via optional server proxy)");
            context.StandardOutput.WriteLine("Host is up.");
            context.StandardOutput.WriteLine("PORT      STATE     SERVICE");
            string stateLabel = response.State.ToString().ToLowerInvariant();
            context.StandardOutput.WriteLine($"{$"{port}/tcp".PadRight(9)} {stateLabel.PadRight(9)} unknown");
            context.StandardOutput.WriteLine();
            context.StandardOutput.WriteLine(
                $"Nmap done: 1 IP address (1 host up) scanned in {response.DurationMs / 1000.0:F2} seconds");
            return 0;
        }
        catch (Exception exception) when (exception is ServerConnectionException or HttpRequestException)
        {
            context.StandardOutput.WriteLine($"Nmap scan report for {target}");
            context.StandardOutput.WriteLine($"Note: real-network probe failed ({exception.Message}).");
            context.StandardOutput.WriteLine($"Nmap done: 1 IP address (0 hosts up) scanned");
            return 1;
        }
    }

    // ── Port range parsing ────────────────────────────────────────────────

    /// <summary>
    /// Parses a port range string ("80", "1-1000", "22,80,443") into first/last bounds.
    /// Returns the defaults if the string cannot be parsed.
    /// </summary>
    private static (int First, int Last) ParsePortRange(string range)
    {
        if (range.Contains('-'))
        {
            var parts = range.Split('-');
            if (parts.Length == 2
                && int.TryParse(parts[0], out var lo)
                && int.TryParse(parts[1], out var hi)
                && lo >= 1 && hi <= 65535 && lo <= hi)
                return (lo, hi);
        }
        else if (int.TryParse(range, out var single) && single >= 1 && single <= 65535)
        {
            return (single, single);
        }

        return (DefaultFirstPort, DefaultLastPort);
    }
}
