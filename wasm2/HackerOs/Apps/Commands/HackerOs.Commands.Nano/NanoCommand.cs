using HackerOs.App.Abstractions;
using HackerOs.AppSdk;

namespace HackerOs.Commands.Nano;

/// <summary>
/// Nano interactive terminal text editor command.
/// In the first slice (P4-W3-006) this command provides a minimal simulation
/// of nano's editing mode: it accepts a filename argument, prints an editing
/// header, and returns immediately with exit code 0.
/// Full interactive editing in the terminal is deferred until the terminal
/// full-screen interaction contract is approved.
/// </summary>
public sealed class NanoCommand : TerminalAppBase
{
    /// <summary>Static manifest used for test validation without a DI container.</summary>
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.nano",
        Name = "nano",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Terminal interactive text editor command",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Nano.dll", "HackerOs.Commands.Nano.NanoCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("system", AppLaunchVisibility.Hidden, []),
        Capabilities = ["filesystem.user-home.read", "filesystem.user-home.write"],
        Resources = AppResourceProfileManifest.None,
        SingleInstancePerUser = false
    };

    /// <summary>Initializes the nano command with its validated manifest.</summary>
    public NanoCommand(AppManifest manifest) : base(manifest) { }

    /// <inheritdoc/>
    public override ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        string targetFile = context.Arguments.Count > 0 ? context.Arguments[0] : "new_file.txt";
        context.StandardOutput.WriteLine($"  GNU nano 5.6.1  {targetFile}");
        context.StandardOutput.WriteLine();
        context.StandardOutput.WriteLine("[nano simulation: interactive editing deferred — file path captured]");
        context.StandardOutput.WriteLine();
        context.StandardOutput.WriteLine("^X Exit   ^O Save   ^W Where Is   ^K Cut   ^U Paste");
        return ValueTask.FromResult(0);
    }
}
