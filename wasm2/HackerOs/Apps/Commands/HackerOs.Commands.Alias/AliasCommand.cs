using HackerOs.App.Abstractions;
using HackerOs.AppSdk;

namespace HackerOs.Commands.Alias;

/// <summary>
/// Implements the <c>alias</c> / <c>addalias</c> / <c>rmalias</c> terminal command (`P4-W5-CMD-008`).
/// Manages terminal command aliases.
/// </summary>
public sealed class AliasCommand : TerminalAppBase
{
    private static readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ll"]  = "ls -la",
        ["la"]  = "ls -a",
        ["cls"] = "clear",
    };

    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.alias",
        Name = "alias",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Manage command aliases",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Alias.dll", "HackerOs.Commands.Alias.AliasCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("alias", ["addalias", "rmalias", "unalias"], "alias [name[=value]]"),
        SingleInstancePerUser = false
    };

    public AliasCommand(AppManifest manifest) : base(manifest) { }

    public override ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Arguments.Count == 0)
        {
            foreach (var (k, v) in _aliases)
                context.StandardOutput.WriteLine($"alias {k}='{v}'");
            return ValueTask.FromResult(0);
        }

        foreach (var arg in context.Arguments)
        {
            int eqIdx = arg.IndexOf('=');
            if (eqIdx > 0)
            {
                string name = arg[..eqIdx];
                string val = arg[(eqIdx + 1)..].Trim('\'', '"');
                _aliases[name] = val;
            }
            else if (_aliases.TryGetValue(arg, out var val))
            {
                context.StandardOutput.WriteLine($"alias {arg}='{val}'");
            }
            else
            {
                context.StandardError.WriteLine($"alias: {arg}: not found");
            }
        }

        return ValueTask.FromResult(0);
    }
}
