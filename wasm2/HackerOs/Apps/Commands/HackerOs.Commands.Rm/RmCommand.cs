using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Rm;

/// <summary>
/// Implements the <c>rm</c> terminal command (`P4-W5-CMD-001`).
/// Removes files and directories. Supports -r / -R and -f.
/// </summary>
public sealed class RmCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.rm",
        Name = "rm",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Remove files or directories",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Rm.dll", "HackerOs.Commands.Rm.RmCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.FileSystemUserHomeRead, AppCapabilities.FileSystemUserHomeWrite],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("rm", [], "rm [-r|-R] [-f] <file...>"),
        SingleInstancePerUser = false
    };

    public RmCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        bool recursive = false;
        bool force     = false;
        List<string> targets = [];

        foreach (var arg in context.Arguments)
        {
            if (arg is "-r" or "-R" or "--recursive") recursive = true;
            else if (arg is "-f" or "--force") force = true;
            else if (!arg.StartsWith('-')) targets.Add(arg);
        }

        if (targets.Count == 0 && !force)
        {
            context.StandardError.WriteLine("rm: missing operand");
            return 1;
        }

        int status = 0;
        foreach (var target in targets)
        {
            var resolved = ResolvePath(context.WorkingDirectory, target);
            var entryStat = await context.App.FileSystem.StatAsync(
                new FileSystemStatRequest(VirtualPath.Parse(resolved)), cancellationToken);
            var parentStat = await context.App.FileSystem.StatAsync(
                new FileSystemStatRequest(VirtualPath.Parse(GetParentPath(resolved))), cancellationToken);

            if (!entryStat.Succeeded || entryStat.Value is null || !parentStat.Succeeded || parentStat.Value is null)
            {
                if (!force)
                {
                    context.StandardError.WriteLine($"rm: cannot remove '{target}': No such file or directory");
                    status = 1;
                }
                continue;
            }

            var req = new FileSystemDeleteRequest(
                path: VirtualPath.Parse(resolved),
                expectedEntryRevision: entryStat.Value.Metadata.Revision,
                expectedParentRevision: parentStat.Value.Metadata.Revision,
                recursive: recursive);

            var result = await context.App.FileSystem.DeleteAsync(req, cancellationToken);
            if (!result.Succeeded && !force)
            {
                context.StandardError.WriteLine($"rm: cannot remove '{target}': {result.Transaction.Error?.Code.ToString() ?? "No such file or directory or is a directory"}");
                status = 1;
            }
        }

        return status;
    }

    private static string ResolvePath(string cwd, string path) =>
        path.StartsWith('/') ? path : (cwd.TrimEnd('/') + "/" + path).Replace("//", "/");

    private static string GetParentPath(string canonicalPath)
    {
        int lastSlash = canonicalPath.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : canonicalPath[..lastSlash];
    }
}
