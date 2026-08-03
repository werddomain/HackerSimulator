using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Diff;

/// <summary>
/// Implements the <c>diff</c> terminal command (`P4-W5-CMD-003`).
/// Compares two virtual text files line by line. Supports -u (unified diff).
/// </summary>
public sealed class DiffCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.diff",
        Name = "diff",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Compare files line by line",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Diff.dll", "HackerOs.Commands.Diff.DiffCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.FileSystemUserHomeRead],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("diff", [], "diff [-u] <file1> <file2>"),
        SingleInstancePerUser = false
    };

    public DiffCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        bool unified = false;
        List<string> files = [];

        foreach (var a in context.Arguments)
        {
            if (a is "-u" or "--unified") unified = true;
            else if (!a.StartsWith('-')) files.Add(a);
        }

        if (files.Count < 2)
        {
            context.StandardError.WriteLine("diff: missing operand after '" + (files.FirstOrDefault() ?? "") + "'");
            return 2;
        }

        var path1 = ResolvePath(context.WorkingDirectory, files[0]);
        var path2 = ResolvePath(context.WorkingDirectory, files[1]);

        var res1 = await context.App.FileSystem.ReadAsync(new FileSystemReadRequest(VirtualPath.Parse(path1)), cancellationToken);
        var res2 = await context.App.FileSystem.ReadAsync(new FileSystemReadRequest(VirtualPath.Parse(path2)), cancellationToken);

        if (!res1.Succeeded || res1.Value is null)
        {
            context.StandardError.WriteLine($"diff: {files[0]}: No such file or directory");
            return 2;
        }
        if (!res2.Succeeded || res2.Value is null)
        {
            context.StandardError.WriteLine($"diff: {files[1]}: No such file or directory");
            return 2;
        }

        using var reader1 = new StreamReader(res1.Value.Content);
        using var reader2 = new StreamReader(res2.Value.Content);

        var lines1 = (await reader1.ReadToEndAsync(cancellationToken)).Split('\n');
        var lines2 = (await reader2.ReadToEndAsync(cancellationToken)).Split('\n');

        bool differed = false;
        int max = Math.Max(lines1.Length, lines2.Length);

        if (unified)
        {
            context.StandardOutput.WriteLine($"--- {files[0]}");
            context.StandardOutput.WriteLine($"+++ {files[1]}");
        }

        for (int i = 0; i < max; i++)
        {
            string l1 = i < lines1.Length ? lines1[i].TrimEnd('\r') : "";
            string l2 = i < lines2.Length ? lines2[i].TrimEnd('\r') : "";

            if (i >= lines1.Length)
            {
                differed = true;
                context.StandardOutput.WriteLine(unified ? $"+{l2}" : $"> {l2}");
            }
            else if (i >= lines2.Length)
            {
                differed = true;
                context.StandardOutput.WriteLine(unified ? $"-{l1}" : $"< {l1}");
            }
            else if (l1 != l2)
            {
                differed = true;
                if (unified)
                {
                    context.StandardOutput.WriteLine($"@@ -{i + 1} +{i + 1} @@");
                    context.StandardOutput.WriteLine($"-{l1}");
                    context.StandardOutput.WriteLine($"+{l2}");
                }
                else
                {
                    context.StandardOutput.WriteLine($"{i + 1}c{i + 1}");
                    context.StandardOutput.WriteLine($"< {l1}");
                    context.StandardOutput.WriteLine("---");
                    context.StandardOutput.WriteLine($"> {l2}");
                }
            }
        }

        return differed ? 1 : 0;
    }

    private static string ResolvePath(string cwd, string path) =>
        path.StartsWith('/') ? path : (cwd.TrimEnd('/') + "/" + path).Replace("//", "/");
}
