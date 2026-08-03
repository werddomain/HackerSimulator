using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Mkdir;

/// <summary>
/// Implements the <c>mkdir</c> terminal command (`P4-W5-CMD-001`).
/// Creates one or more virtual directories.
/// </summary>
public sealed class MkdirCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.mkdir",
        Name = "mkdir",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Create directories",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Mkdir.dll", "HackerOs.Commands.Mkdir.MkdirCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.FileSystemUserHomeWrite],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("mkdir", [], "mkdir [-p] <directory...>"),
        SingleInstancePerUser = false
    };

    public MkdirCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        List<string> paths = [];
        foreach (var arg in context.Arguments)
        {
            if (!arg.StartsWith('-')) paths.Add(arg);
        }

        if (paths.Count == 0)
        {
            context.StandardError.WriteLine("mkdir: missing operand");
            return 1;
        }

        int status = 0;
        foreach (var path in paths)
        {
            var resolved = ResolvePath(context.WorkingDirectory, path);
            var req = new FileSystemCreateRequest(
                path: VirtualPath.Parse(resolved),
                kind: FileSystemEntryKind.Directory,
                permissions: FileSystemPermissions.FromMode(0b111_101_101),
                expectedParentRevision: 0);

            var result = await context.App.FileSystem.CreateAsync(req, cancellationToken);
            if (!result.Succeeded)
            {
                context.StandardError.WriteLine($"mkdir: cannot create directory '{path}': {result.Transaction.Error?.Code.ToString() ?? "Operation failed"}");
                status = 1;
            }
        }

        return status;
    }

    private static string ResolvePath(string cwd, string path) =>
        path.StartsWith('/') ? path : (cwd.TrimEnd('/') + "/" + path).Replace("//", "/");
}
