using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Chmod;

/// <summary>
/// Implements the <c>chmod</c> terminal command (`P4-W5-CMD-002`).
/// Changes permissions mode bits on virtual filesystem entries.
/// </summary>
public sealed class ChmodCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.chmod",
        Name = "chmod",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Change file permissions mode bits",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Chmod.dll", "HackerOs.Commands.Chmod.ChmodCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.FileSystemUserHomeWrite],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("chmod", [], "chmod <mode> <file...>"),
        SingleInstancePerUser = false
    };

    public ChmodCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var pos = context.Arguments.Where(a => !a.StartsWith('-')).ToList();
        if (pos.Count < 2)
        {
            context.StandardError.WriteLine("chmod: missing operand");
            return 1;
        }

        string modeStr = pos[0];
        var targets = pos.Skip(1).ToList();

        ushort modeBits;
        try
        {
            modeBits = Convert.ToUInt16(modeStr, 8);
        }
        catch
        {
            context.StandardError.WriteLine($"chmod: invalid mode: '{modeStr}'");
            return 1;
        }

        int status = 0;
        foreach (var target in targets)
        {
            var resolved = ResolvePath(context.WorkingDirectory, target);
            var entryStat = await context.App.FileSystem.StatAsync(
                new FileSystemStatRequest(VirtualPath.Parse(resolved)), cancellationToken);
            if (!entryStat.Succeeded || entryStat.Value is null)
            {
                context.StandardError.WriteLine($"chmod: cannot access '{target}': No such file or directory");
                status = 1;
                continue;
            }

            var req = new FileSystemSetPermissionsRequest(
                path: VirtualPath.Parse(resolved),
                permissions: FileSystemPermissions.FromMode(modeBits),
                expectedRevision: entryStat.Value.Metadata.Revision);

            var result = await context.App.FileSystem.SetPermissionsAsync(req, cancellationToken);
            if (!result.Succeeded)
            {
                context.StandardError.WriteLine($"chmod: changing permissions of '{target}': {result.Transaction.Error?.Code.ToString() ?? "Operation failed"}");
                status = 1;
            }
        }

        return status;
    }

    private static string ResolvePath(string cwd, string path) =>
        path.StartsWith('/') ? path : (cwd.TrimEnd('/') + "/" + path).Replace("//", "/");
}
