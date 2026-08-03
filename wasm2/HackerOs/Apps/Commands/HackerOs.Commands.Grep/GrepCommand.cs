using System.Text.RegularExpressions;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Grep;

/// <summary>
/// Implements the <c>grep</c> terminal command (`P4-W5-CMD-003`).
/// Searches stdin or virtual files for matching lines.
/// Supports -i (case-insensitive), -n (line numbers), -v (invert match).
/// </summary>
public sealed class GrepCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.grep",
        Name = "grep",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Print lines matching a pattern",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Grep.dll", "HackerOs.Commands.Grep.GrepCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.FileSystemUserHomeRead],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("grep", [], "grep [-i] [-n] [-v] <pattern> [file...]"),
        SingleInstancePerUser = false
    };

    public GrepCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        bool ignoreCase = false;
        bool lineNumbers = false;
        bool invertMatch = false;

        string? pattern = null;
        List<string> files = [];

        var args = context.Arguments;
        for (int i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a is "-i" or "--ignore-case") ignoreCase = true;
            else if (a is "-n" or "--line-number") lineNumbers = true;
            else if (a is "-v" or "--invert-match") invertMatch = true;
            else if (pattern is null && !a.StartsWith('-')) pattern = a;
            else if (!a.StartsWith('-')) files.Add(a);
        }

        if (pattern is null)
        {
            context.StandardError.WriteLine("grep: missing pattern");
            return 2;
        }

        var regexOpts = RegexOptions.Compiled | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        Regex regex;
        try { regex = new Regex(pattern, regexOpts); }
        catch
        {
            context.StandardError.WriteLine($"grep: invalid regular expression '{pattern}'");
            return 2;
        }

        int matchesCount = 0;

        if (files.Count == 0)
        {
            int lineNo = 0;
            string? line;
            while ((line = await context.StandardInput.ReadLineAsync(cancellationToken)) != null)
            {
                lineNo++;
                bool matches = regex.IsMatch(line);
                if (invertMatch ? !matches : matches)
                {
                    matchesCount++;
                    if (lineNumbers) context.StandardOutput.WriteLine($"{lineNo}:{line}");
                    else context.StandardOutput.WriteLine(line);
                }
            }
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
                    context.StandardError.WriteLine($"grep: {file}: No such file or directory");
                    continue;
                }

                using var reader = new StreamReader(res.Value.Content);
                int lineNo = 0;
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    lineNo++;
                    bool matches = regex.IsMatch(line);
                    if (invertMatch ? !matches : matches)
                    {
                        matchesCount++;
                        string prefix = files.Count > 1 ? $"{file}:" : "";
                        string lineNoStr = lineNumbers ? $"{lineNo}:" : "";
                        context.StandardOutput.WriteLine($"{prefix}{lineNoStr}{line}");
                    }
                }
            }
        }

        return matchesCount > 0 ? 0 : 1;
    }

    private static string ResolvePath(string cwd, string path) =>
        path.StartsWith('/') ? path : (cwd.TrimEnd('/') + "/" + path).Replace("//", "/");
}
