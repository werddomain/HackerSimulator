using System.Text.RegularExpressions;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Find;

/// <summary>
/// Implements the <c>find</c> terminal command (`P4-W5-CMD-003`).
/// Recursively searches for files/directories matching name pattern or type.
/// </summary>
public sealed class FindCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.find",
        Name = "find",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Search directory hierarchy",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Find.dll", "HackerOs.Commands.Find.FindCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.FileSystemUserHomeRead],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("find", [], "find [path] [-name pattern] [-type f|d]"),
        SingleInstancePerUser = false
    };

    public FindCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        string root = context.WorkingDirectory;
        string? namePattern = null;
        string? typeFilter = null;

        var args = context.Arguments;
        for (int i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a == "-name" && i + 1 < args.Count) namePattern = args[++i];
            else if (a == "-type" && i + 1 < args.Count) typeFilter = args[++i];
            else if (!a.StartsWith('-')) root = a;
        }

        var resolvedRoot = ResolvePath(context.WorkingDirectory, root);
        await TraverseAsync(context, resolvedRoot, namePattern, typeFilter, cancellationToken);
        return 0;
    }

    private async Task TraverseAsync(
        TerminalExecutionContext context,
        string currentPath,
        string? namePattern,
        string? typeFilter,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return;

        var statReq = new FileSystemStatRequest(VirtualPath.Parse(currentPath));
        var statRes = await context.App.FileSystem.StatAsync(statReq, cancellationToken);
        if (!statRes.Succeeded || statRes.Value is null) return;

        var snapshot = statRes.Value;
        string name = System.IO.Path.GetFileName(snapshot.Path.Value);
        if (string.IsNullOrEmpty(name)) name = snapshot.Path.Value;

        bool matchesName = namePattern is null || MatchGlob(name, namePattern);
        bool matchesType = typeFilter is null ||
            (typeFilter == "f" && snapshot.Metadata.Kind == FileSystemEntryKind.File) ||
            (typeFilter == "d" && snapshot.Metadata.Kind == FileSystemEntryKind.Directory);

        if (matchesName && matchesType)
            context.StandardOutput.WriteLine(currentPath);

        if (snapshot.Metadata.Kind == FileSystemEntryKind.Directory)
        {
            var enumReq = new FileSystemEnumerateRequest(VirtualPath.Parse(currentPath));
            var enumRes = await context.App.FileSystem.EnumerateAsync(enumReq, cancellationToken);
            if (enumRes.Succeeded && enumRes.Value is not null)
            {
                foreach (var child in enumRes.Value.Entries)
                {
                    string childPath = (currentPath.TrimEnd('/') + "/" + child.Name.Value).Replace("//", "/");
                    await TraverseAsync(context, childPath, namePattern, typeFilter, cancellationToken);
                }
            }
        }
    }

    private static bool MatchGlob(string text, string pattern)
    {
        string regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(text, regex, RegexOptions.IgnoreCase);
    }

    private static string ResolvePath(string cwd, string path) =>
        path.StartsWith('/') ? path : (cwd.TrimEnd('/') + "/" + path).Replace("//", "/");
}
