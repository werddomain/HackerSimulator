using HackerOs.App.Abstractions;
using HackerOs.AppSdk;

namespace HackerOs.Commands.Help;

/// <summary>
/// Implements the <c>help</c> / <c>man</c> terminal command (`P4-W5-CMD-007`).
/// Displays manual pages and command usage listings.
/// </summary>
public sealed class HelpCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.help",
        Name = "help",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Display help manual pages",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Help.dll", "HackerOs.Commands.Help.HelpCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("help", ["man"], "help [command]"),
        SingleInstancePerUser = false
    };

    public HelpCommand(AppManifest manifest) : base(manifest) { }

    public override ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Arguments.Count == 0)
        {
            context.StandardOutput.WriteLine("HackerOS Shell Commands Reference:");
            context.StandardOutput.WriteLine("  Filesystem:  ls, cd, pwd, cat, mkdir, touch, rm, cp, mv, chmod, find, grep, head, tail, sort, wc, diff");
            context.StandardOutput.WriteLine("  Network:     ping, nmap, curl");
            context.StandardOutput.WriteLine("  Process:     ps, kill, launch");
            context.StandardOutput.WriteLine("  Utilities:   echo, clear, help, man, alias, addalias, rmalias, nano");
            context.StandardOutput.WriteLine();
            context.StandardOutput.WriteLine("Type 'help <command>' or 'man <command>' for specific command details.");
            return ValueTask.FromResult(0);
        }

        string cmd = context.Arguments[0].ToLowerInvariant();
        switch (cmd)
        {
            case "ping":
                context.StandardOutput.WriteLine("PING(1)                     HackerOS Manual                    PING(1)");
                context.StandardOutput.WriteLine("NAME: ping - send ICMP ECHO_REQUEST to simulated network hosts");
                context.StandardOutput.WriteLine("USAGE: ping <hostname|ip>");
                break;
            case "nmap":
                context.StandardOutput.WriteLine("NMAP(1)                     HackerOS Manual                    NMAP(1)");
                context.StandardOutput.WriteLine("NAME: nmap - simulated Network Mapper and port scanner");
                context.StandardOutput.WriteLine("USAGE: nmap [-p <range>] [-sV] [-O] <target>");
                break;
            case "curl":
                context.StandardOutput.WriteLine("CURL(1)                     HackerOS Manual                    CURL(1)");
                context.StandardOutput.WriteLine("NAME: curl - transfer data from simulated website URLs");
                context.StandardOutput.WriteLine("USAGE: curl [-I] [-v] [-L] [-X <method>] [-d <data>] <url>");
                break;
            default:
                context.StandardOutput.WriteLine($"Manual entry for '{cmd}': usage: {cmd} [options]");
                break;
        }

        return ValueTask.FromResult(0);
    }
}
