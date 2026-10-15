using System.Globalization;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Net;
using HackerOs.Commands.Common;

namespace HackerOs.Commands.Netstat;

/// <summary>
/// <c>netstat [-tulnap] [-i] [-r] [-s]</c> (ADR 0064), net-tools style (also answers as
/// <c>ss</c>). The listening sockets are found the way an attacker's scan would: by probing
/// this machine's own simulated stack (<c>127.0.0.1</c>, <see cref="HackTcpClient"/>), so what
/// <c>nmap localhost</c> sees open is what <c>netstat -l</c> lists. <c>-i</c> shows the
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

        IReadOnlyList<HackPortProbeResult> open;
        try
        {
            open = [.. (await HackTcpClient.ProbeAsync("127.0.0.1", 1, 10000, cancellationToken)).Where(port => port.State == HackPortState.Open)];
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Net.Sockets.SocketException or NotSupportedException)
        {
            open = [];
        }

        foreach (HackPortProbeResult port in open)
        {
            string local = numeric || port.ServiceName is null ? $"0.0.0.0:{port.Port}" : $"0.0.0.0:{port.ServiceName}";
            string program = programs ? $" {(port.Port * 37 % 900) + 100}/{(port.ServiceVersion ?? port.ServiceName ?? "-").Split(' ')[0].ToLowerInvariant()}" : string.Empty;
            await context.StandardOutput.WriteLineAsync($"tcp        0      0 {local,-23} {"0.0.0.0:*",-23} LISTEN     {program}");
        }
        return 0;
    }
}
