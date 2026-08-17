using HackerOs.App.Abstractions;
using HackerOs.AppSdk;

namespace HackerOs.Commands.Clear;

/// <summary>
/// Implements the <c>clear</c> terminal command (`P4-W5-CMD-006`).
/// Clears the active terminal buffer.
/// </summary>
public sealed class ClearCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.clear",
        Name = "clear",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Clear terminal screen",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Clear.dll", "HackerOs.Commands.Clear.ClearCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("clear", ["cls"], "clear"),
        SingleInstancePerUser = false
    };

    public ClearCommand(AppManifest manifest) : base(manifest) { }

    public override ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        // ANSI escape code for clear screen and reset cursor
        context.StandardOutput.Write("\x1b[2J\x1b[H");
        return ValueTask.FromResult(0);
    }
}
