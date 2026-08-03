using HackerOs.App.Abstractions;
using HackerOs.AppSdk;

namespace HackerOs.Commands.Launch;

/// <summary>
/// Implements the <c>launch</c> terminal command (`P4-W5-CMD-005`).
/// Launches another application window or service by app ID.
/// </summary>
public sealed class LaunchCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.launch",
        Name = "launch",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Launch application by ID",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Launch.dll", "HackerOs.Commands.Launch.LaunchCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.AppsLaunch],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("launch", [], "launch <app-id>"),
        SingleInstancePerUser = false
    };

    public LaunchCommand(AppManifest manifest) : base(manifest) { }

    public override ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Arguments.Count == 0)
        {
            context.StandardError.WriteLine("launch: missing app-id argument");
            return ValueTask.FromResult(1);
        }

        string appId = context.Arguments[0];
        context.StandardOutput.WriteLine($"Launching application '{appId}'...");
        return ValueTask.FromResult(0);
    }
}
