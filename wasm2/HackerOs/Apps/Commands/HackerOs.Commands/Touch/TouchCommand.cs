using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Touch;

/// <summary>
/// Implements the <c>touch</c> terminal command (`P4-W5-CMD-001`).
/// Creates an empty file if it doesn't exist.
/// </summary>
public sealed class TouchCommand : TerminalAppBase
{
    public TouchCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var paths = context.Arguments.Where(a => !a.StartsWith('-')).ToList();
        if (paths.Count == 0)
        {
            context.StandardError.WriteLine("touch: missing file operand");
            return 1;
        }

        int status = 0;
        foreach (var path in paths)
        {
            var resolved = ResolvePath(context.WorkingDirectory, path);
            var parentStat = await context.App.FileSystem.StatAsync(
                new FileSystemStatRequest(VirtualPath.Parse(GetParentPath(resolved))), cancellationToken);
            if (!parentStat.Succeeded || parentStat.Value is null)
            {
                context.StandardError.WriteLine($"touch: cannot touch '{path}': No such file or directory");
                status = 1;
                continue;
            }

            var req = new FileSystemCreateRequest(
                path: VirtualPath.Parse(resolved),
                kind: FileSystemEntryKind.File,
                permissions: FileSystemPermissions.FromMode(0b110_100_100),
                expectedParentRevision: parentStat.Value.Metadata.Revision);

            var result = await context.App.FileSystem.CreateAsync(req, cancellationToken);
            if (!result.Succeeded && result.Transaction.Error?.Code != FileSystemErrorCode.AlreadyExists)
            {
                context.StandardError.WriteLine($"touch: cannot touch '{path}': {result.Transaction.Error?.Code.ToString() ?? "Operation failed"}");
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
