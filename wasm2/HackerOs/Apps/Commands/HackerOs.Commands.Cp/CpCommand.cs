using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Cp;

/// <summary>
/// Implements the <c>cp</c> terminal command (`P4-W5-CMD-001`).
/// Copies files and directories. Supports -r / -R.
/// </summary>
public sealed class CpCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.cp",
        Name = "cp",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Copy files and directories",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Cp.dll", "HackerOs.Commands.Cp.CpCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.FileSystemUserHomeRead, AppCapabilities.FileSystemUserHomeWrite],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("cp", [], "cp [-r|-R] <source...> <destination>"),
        SingleInstancePerUser = false
    };

    public CpCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        List<string> pos = [];

        foreach (var arg in context.Arguments)
        {
            if (!arg.StartsWith('-')) pos.Add(arg);
        }

        if (pos.Count < 2)
        {
            context.StandardError.WriteLine("cp: missing destination file operand after '" + (pos.FirstOrDefault() ?? "") + "'");
            return 1;
        }

        string dest = pos[^1];
        var sources = pos.Take(pos.Count - 1).ToList();

        int status = 0;
        foreach (var src in sources)
        {
            var srcPath = ResolvePath(context.WorkingDirectory, src);
            var destPath = ResolvePath(context.WorkingDirectory, dest);

            var srcStat = await context.App.FileSystem.StatAsync(
                new FileSystemStatRequest(VirtualPath.Parse(srcPath)), cancellationToken);
            if (!srcStat.Succeeded || srcStat.Value is null)
            {
                context.StandardError.WriteLine($"cp: cannot stat '{src}': No such file or directory");
                status = 1;
                continue;
            }

            var destParentStat = await context.App.FileSystem.StatAsync(
                new FileSystemStatRequest(VirtualPath.Parse(GetParentPath(destPath))), cancellationToken);
            if (!destParentStat.Succeeded || destParentStat.Value is null)
            {
                context.StandardError.WriteLine($"cp: cannot create '{dest}': No such file or directory");
                status = 1;
                continue;
            }

            var req = new FileSystemCopyRequest(
                sourcePath: VirtualPath.Parse(srcPath),
                destinationPath: VirtualPath.Parse(destPath),
                expectedEntryRevision: srcStat.Value.Metadata.Revision,
                expectedDestinationParentRevision: destParentStat.Value.Metadata.Revision);

            var result = await context.App.FileSystem.CopyAsync(req, cancellationToken);
            if (!result.Succeeded)
            {
                context.StandardError.WriteLine($"cp: cannot copy '{src}' to '{dest}': {result.Transaction.Error?.Code.ToString() ?? "Operation failed"}");
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
