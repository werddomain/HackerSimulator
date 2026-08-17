using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Sort;

/// <summary>
/// Implements the <c>sort</c> terminal command (`P4-W5-CMD-003`).
/// Sorts lines from stdin or virtual files. Supports -r (reverse), -n (numeric), -u (unique).
/// </summary>
public sealed class SortCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.sort",
        Name = "sort",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Sort lines of text",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Sort.dll", "HackerOs.Commands.Sort.SortCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.FileSystemUserHomeRead],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("sort", [], "sort [-r] [-n] [-u] [file...]"),
        SingleInstancePerUser = false
    };

    public SortCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        bool reverse = false;
        bool numeric = false;
        bool unique  = false;
        List<string> files = [];

        foreach (var a in context.Arguments)
        {
            if (a is "-r" or "--reverse") reverse = true;
            else if (a is "-n" or "--numeric-sort") numeric = true;
            else if (a is "-u" or "--unique") unique = true;
            else if (!a.StartsWith('-')) files.Add(a);
        }

        List<string> lines = [];

        if (files.Count == 0)
        {
            string? line;
            while ((line = await context.StandardInput.ReadLineAsync(cancellationToken)) != null)
                lines.Add(line);
        }
        else
        {
            foreach (var file in files)
            {
                var resolved = ResolvePath(context.WorkingDirectory, file);
                var req = new FileSystemReadRequest(VirtualPath.Parse(resolved));
                var res = await context.App.FileSystem.ReadAsync(req, cancellationToken);
                if (!res.Succeeded || res.Value is null)
                {
                    context.StandardError.WriteLine($"sort: {file}: No such file or directory");
                    continue;
                }

                using var reader = new StreamReader(res.Value.Content);
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                    lines.Add(line);
            }
        }

        Comparison<string> comp = (x, y) =>
        {
            if (numeric)
            {
                double.TryParse(x, out double nx);
                double.TryParse(y, out double ny);
                return nx.CompareTo(ny);
            }
            return string.Compare(x, y, StringComparison.Ordinal);
        };

        lines.Sort(comp);
        if (reverse) lines.Reverse();

        IEnumerable<string> result = lines;
        if (unique) result = result.Distinct(StringComparer.Ordinal);

        foreach (var l in result)
            context.StandardOutput.WriteLine(l);

        return 0;
    }

    private static string ResolvePath(string cwd, string path) =>
        path.StartsWith('/') ? path : (cwd.TrimEnd('/') + "/" + path).Replace("//", "/");
}
