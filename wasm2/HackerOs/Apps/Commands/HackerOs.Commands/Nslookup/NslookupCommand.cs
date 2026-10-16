using System.Net;
using System.Net.Sockets;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Net;

namespace HackerOs.Commands.Nslookup;

/// <summary>
/// <c>nslookup &lt;name|address&gt;</c> (ADR 0074, section 4): asks <c>hackdns</c>, the OS
/// resolver, through <see cref="HackDns"/>, and prints the answer the way BIND's
/// <c>nslookup</c> does against the local stub resolver (<c>127.0.0.53</c>, as on a
/// <c>systemd-resolved</c> machine). Unlike the real tool, it also says which source answered
/// (<c>/etc/hosts</c> and its line, <c>localhost</c>, a web app's URLs, a hosted site, or the
/// simulated network), so a player can see why a name lands where it does.
/// </summary>
public sealed class NslookupCommand(AppManifest manifest) : TerminalAppBase(manifest)
{
    /// <summary>The stub resolver's address, printed as the server.</summary>
    private const string StubResolver = "127.0.0.53";

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        string? target = context.Arguments.FirstOrDefault(static argument => !argument.StartsWith('-'));
        if (target is null)
        {
            await context.StandardError.WriteLineAsync("Usage: nslookup <name|address>");
            return 1;
        }

        await context.StandardOutput.WriteLineAsync($"Server:\t\t{StubResolver}");
        await context.StandardOutput.WriteLineAsync($"Address:\t{StubResolver}#53");
        await context.StandardOutput.WriteLineAsync();

        try
        {
            if (IPAddress.TryParse(target, out IPAddress? address))
            {
                HackHostEntry reverse = await HackDns.GetHostEntryAsync(address, cancellationToken);
                await context.StandardOutput.WriteLineAsync($"{ReverseName(address)}\tname = {reverse.HostName}.");
                return 0;
            }

            HackHostEntry entry = await HackDns.GetHostEntryAsync(target, cancellationToken);
            await context.StandardOutput.WriteLineAsync(SourceLine(entry));
            if (!string.Equals(entry.HostName, target.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
            {
                await context.StandardOutput.WriteLineAsync($"{target}\tcanonical name = {entry.HostName}.");
            }

            foreach (IPAddress resolved in entry.AddressList)
            {
                await context.StandardOutput.WriteLineAsync($"Name:\t{entry.HostName}");
                await context.StandardOutput.WriteLineAsync($"Address: {resolved}");
            }

            return 0;
        }
        catch (SocketException)
        {
            string name = IPAddress.TryParse(target, out IPAddress? literal) ? ReverseName(literal) : target;
            await context.StandardOutput.WriteLineAsync($"** server can't find {name}: NXDOMAIN");
            await context.StandardOutput.WriteLineAsync();
            return 1;
        }
    }

    /// <summary>The line that says where the answer came from, in the comment style of <c>dig</c>.</summary>
    private static string SourceLine(HackHostEntry entry) => entry.Source switch
    {
        HackDnsSource.HostsFile => $";; source: {entry.SourceDetail ?? "/etc/hosts"} (static table)",
        HackDnsSource.Loopback => ";; source: loopback (this machine)",
        HackDnsSource.WebApp => $";; source: web app {entry.SourceDetail} (web.urls, this machine)",
        HackDnsSource.HostedSite => $";; source: hosted site {entry.SourceDetail} (this machine)",
        HackDnsSource.SimulatedNetwork => "Non-authoritative answer:",
        _ => ";; source: unknown"
    };

    /// <summary>The PTR name of an IPv4 address: its octets reversed under <c>in-addr.arpa</c>.</summary>
    private static string ReverseName(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetwork
            ? string.Join('.', address.GetAddressBytes().Reverse()) + ".in-addr.arpa"
            : address.ToString();
}
