using System.Globalization;
using System.Net;
using System.Net.Sockets;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Net;

namespace HackerOs.Commands.Nmap;

/// <summary>
/// Simulated <c>nmap</c> port-scanner terminal command (P4-W4-006) on the SDK network API
/// (ADR 0056, section 6): <see cref="HackDns"/> for the address, <see cref="HackPing"/> for host
/// discovery, <see cref="HackTcpClient"/> for the scan and <c>-O</c>. For a host outside the
/// simulated network the OS relays a single-port probe (<c>-p &lt;port&gt;</c>) through the
/// optional server when it can (ADR 0035); a range against such a host is never relayed and the
/// host "seems down".
/// </summary>
public sealed class NmapCommand : TerminalAppBase
{
    /// <summary>Default port range when -p is not specified.</summary>
    private const int DefaultFirstPort = 1;
    private const int DefaultLastPort  = 1000;

    /// <summary>Initializes the command with its manifest.</summary>
    public NmapCommand(AppManifest manifest) : base(manifest)
    {
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        // Parse arguments: nmap [-p <range>] [-sV] [-O] <target>
        string? target = null;
        int firstPort = DefaultFirstPort;
        int lastPort  = DefaultLastPort;
        bool showVersion = false;
        bool showOs      = false;

        IReadOnlyList<string> args = context.Arguments;
        for (int i = 0; i < args.Count; i++)
        {
            string a = args[i];
            if (a is "-p" or "--port" && i + 1 < args.Count)
            {
                (firstPort, lastPort) = ParsePortRange(args[++i]);
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
                target = a;
            }
        }

        if (target is null)
        {
            context.StandardError.WriteLine("nmap: usage: nmap [-p <port-range>] [-sV] [-O] <target>");
            return 1;
        }

        context.StandardOutput.WriteLine($"\nStarting Nmap 7.94 ( https://nmap.org ) at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");

        IPAddress? address = await TryResolveAsync(target, cancellationToken).ConfigureAwait(false);
        string reportName = address is null || address.ToString() == target ? target : $"{target} ({address})";

        // Host discovery: a known host is pinged first, as nmap does without -Pn.
        HackPingReply? discovery = null;
        if (address is not null)
        {
            discovery = await HackPing.SendAsync(target, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (discovery.Status != HackIPStatus.Success)
            {
                context.StandardOutput.WriteLine($"Nmap scan report for {reportName}");
                context.StandardOutput.WriteLine("Host is unreachable.");
                context.StandardOutput.WriteLine("Nmap done: 1 IP address (0 hosts up) scanned");
                return 1;
            }
        }

        IReadOnlyList<HackPortProbeResult> ports;
        try
        {
            ports = await HackTcpClient.ProbeAsync(target, firstPort, lastPort, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException exception)
        {
            context.StandardOutput.WriteLine($"Nmap scan report for {reportName}");
            context.StandardOutput.WriteLine(exception.SocketErrorCode == SocketError.HostNotFound
                ? "Note: Host seems down. If it is really up, but blocking our ping probes, try -Pn"
                : $"Note: probe failed ({exception.SocketErrorCode}).");
            context.StandardOutput.WriteLine("Nmap done: 1 IP address (0 hosts up) scanned");
            return 1;
        }

        context.StandardOutput.WriteLine($"Nmap scan report for {reportName}");
        context.StandardOutput.WriteLine(discovery is null
            ? "Host is up."
            : string.Create(CultureInfo.InvariantCulture, $"Host is up ({discovery.RoundtripTime.TotalSeconds:0.0000}s latency)."));

        int hidden = (lastPort - firstPort + 1) - ports.Count;
        if (hidden > 0)
        {
            context.StandardOutput.WriteLine($"Not shown: {hidden} closed tcp ports (conn-refused)");
        }

        if (ports.Count > 0)
        {
            context.StandardOutput.WriteLine(showVersion
                ? "PORT      STATE     SERVICE          VERSION"
                : "PORT      STATE     SERVICE");

            foreach (HackPortProbeResult port in ports)
            {
                if (cancellationToken.IsCancellationRequested) break;

                string portCol    = $"{port.Port}/tcp".PadRight(9);
                string stateCol   = port.State.ToString().ToLowerInvariant().PadRight(9);
                string serviceCol = (port.ServiceName ?? "unknown").PadRight(16);

                context.StandardOutput.WriteLine(showVersion && port.ServiceVersion is not null
                    ? $"{portCol} {stateCol} {serviceCol} {port.ServiceVersion}"
                    : $"{portCol} {stateCol} {serviceCol}".TrimEnd());
            }
        }

        if (showOs && address is not null
            && await HackTcpClient.DetectOperatingSystemAsync(target, cancellationToken).ConfigureAwait(false) is { } os)
        {
            context.StandardOutput.WriteLine();
            context.StandardOutput.WriteLine("OS detection performed. Please report any incorrect results at https://nmap.org/submit/");
            context.StandardOutput.WriteLine($"OS details: {os.Name} {os.Version}  (Accuracy: {os.Accuracy}%)");
        }

        double elapsedSeconds = discovery is null ? 0.5 : discovery.RoundtripTime.TotalMilliseconds * 0.1;
        context.StandardOutput.WriteLine();
        context.StandardOutput.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Nmap done: 1 IP address (1 host up) scanned in {elapsedSeconds:F2} seconds"));
        return 0;
    }

    /// <summary>Resolves <paramref name="target"/>, or returns <see langword="null"/> when no resolver knows it.</summary>
    private static async Task<IPAddress?> TryResolveAsync(string target, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(target, out IPAddress? literal))
        {
            // An address literal is only "known" when the simulated network has a host there.
            try
            {
                await HackDns.GetHostEntryAsync(literal, cancellationToken).ConfigureAwait(false);
                return literal;
            }
            catch (SocketException)
            {
                return null;
            }
        }

        try
        {
            IPAddress[] addresses = await HackDns.GetHostAddressesAsync(target, cancellationToken).ConfigureAwait(false);
            return addresses.Length > 0 ? addresses[0] : null;
        }
        catch (SocketException)
        {
            return null;
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
