using System.Globalization;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Net;
using HackerOs.Commands.Common;

namespace HackerOs.Commands.Netstat;

/// <summary>
/// <c>netstat [-tulnap] [-i] [-r] [-s]</c> (ADR 0064), net-tools style (also answers as
/// <c>ss</c>). The listening sockets come from the OS table of listening ports (ADR 0074,
/// section 2, <see cref="HackIPGlobalProperties"/>, the same table as <c>/proc/net/tcp</c>): the
/// sockets apps opened with <c>HackHttpListener</c> and the system daemons. With <c>-p</c>, the
/// owner of a socket is shown only to its user and to root, as on Linux. <c>-i</c> shows the
/// <c>/proc/net/dev</c> counters, <c>-r</c> the routing table, <c>-s</c> totals.
/// </summary>
public sealed class NetstatCommand(AppManifest manifest) : TerminalAppBase(manifest)
{
    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        HashSet<char> flags = [];
        foreach (string arg in context.Arguments)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                flags.Add(arg switch { "--tcp" => 't', "--udp" => 'u', "--listening" => 'l', "--numeric" => 'n', "--all" => 'a', "--programs" => 'p', "--interfaces" => 'i', "--route" => 'r', "--statistics" => 's', _ => '?' });
            }
            else if (arg.StartsWith('-'))
            {
                foreach (char flag in arg[1..])
                {
                    flags.Add(flag);
                }
            }
        }

        if (flags.Contains('i'))
        {
            await context.StandardOutput.WriteLineAsync("Kernel Interface table");
            await context.StandardOutput.WriteLineAsync("Iface             MTU    RX-OK RX-ERR RX-DRP RX-OVR    TX-OK TX-ERR TX-DRP TX-OVR Flg");
            foreach (NetworkInterface nic in await NetworkInfo.GetInterfacesAsync(cancellationToken))
            {
                await context.StandardOutput.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
                    $"{nic.Name,-12} {nic.Mtu,8} {nic.RxPackets,8} {nic.RxErrors,6} {nic.RxDropped,6} {0,6} {nic.TxPackets,8} {nic.TxErrors,6} {nic.TxDropped,6} {0,6} {(nic.Name == "lo" ? "LRU" : "BMRU")}"));
            }

            return 0;
        }

        if (flags.Contains('r'))
        {
            await context.StandardOutput.WriteLineAsync("Kernel IP routing table");
            await context.StandardOutput.WriteLineAsync("Destination     Gateway         Genmask         Flags   MSS Window  irtt Iface");
            foreach (var route in NetworkInfo.Routes)
            {
                await context.StandardOutput.WriteLineAsync($"{route.Destination,-15} {route.Gateway,-15} {route.Mask,-15} {route.Flags,-5}     0 0          0 {route.Device}");
            }

            return 0;
        }

        if (flags.Contains('s'))
        {
            List<NetworkInterface> interfaces = await NetworkInfo.GetInterfacesAsync(cancellationToken);
            long received = interfaces.Sum(nic => nic.RxPackets);
            long sent = interfaces.Sum(nic => nic.TxPackets);
            await context.StandardOutput.WriteLineAsync("Ip:");
            await context.StandardOutput.WriteLineAsync($"    {received} total packets received");
            await context.StandardOutput.WriteLineAsync("    0 forwarded");
            await context.StandardOutput.WriteLineAsync($"    {received} incoming packets delivered");
            await context.StandardOutput.WriteLineAsync($"    {sent} requests sent out");
            await context.StandardOutput.WriteLineAsync("Tcp:");
            await context.StandardOutput.WriteLineAsync($"    {sent} segments sent out");
            await context.StandardOutput.WriteLineAsync($"    {received} segments received");
            return 0;
        }

        bool listening = flags.Contains('l') || flags.Contains('a');
        bool numeric = flags.Contains('n');
        bool programs = flags.Contains('p');
        bool udpOnly = flags.Contains('u') && !flags.Contains('t');
        await context.StandardOutput.WriteLineAsync($"Active Internet connections ({(flags.Contains('a') ? "servers and established" : listening ? "only servers" : "w/o servers")})");
        await context.StandardOutput.WriteLineAsync($"Proto Recv-Q Send-Q Local Address           Foreign Address         State      {(programs ? " PID/Program name" : string.Empty)}");
        if (!listening || udpOnly)
        {
            return 0;
        }

        IReadOnlyList<HackTcpListenerInfo> listeners = await HackIPGlobalProperties.GetActiveTcpListenersAsync(cancellationToken);
        if (programs && listeners.Any(static listener => listener.ProcessId is null))
        {
            // net-tools prints this before the table when some owners are hidden from the caller.
            await context.StandardError.WriteLineAsync("(Not all processes could be identified, non-owned process info");
            await context.StandardError.WriteLineAsync(" will not be shown, you would have to be root to see it all.)");
        }

        foreach (HackTcpListenerInfo listener in listeners)
        {
            string address = numeric || !listener.Address.Equals(System.Net.IPAddress.Loopback)
                ? listener.Address.ToString()
                : "localhost";
            string port = numeric ? listener.Port.ToString(CultureInfo.InvariantCulture) : ServiceName(listener.Port);
            string local = $"{address}:{port}";
            string program = programs
                ? " " + (listener.ProcessId is { } pid ? $"{pid}/{listener.ProgramName}" : "-")
                : string.Empty;
            await context.StandardOutput.WriteLineAsync($"tcp        0      0 {local,-23} {"0.0.0.0:*",-23} LISTEN     {program}");
        }

        return 0;
    }

    /// <summary>The name <c>/etc/services</c> gives a well-known port, or the number itself.</summary>
    private static string ServiceName(int port) => port switch
    {
        21 => "ftp",
        22 => "ssh",
        23 => "telnet",
        25 => "smtp",
        53 => "domain",
        80 => "http",
        443 => "https",
        3306 => "mysql",
        5432 => "postgresql",
        8080 => "http-alt",
        _ => port.ToString(CultureInfo.InvariantCulture)
    };
}
