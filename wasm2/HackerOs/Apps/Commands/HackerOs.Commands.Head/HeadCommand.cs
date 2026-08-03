using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Head;

/// <summary>
/// Implements the <c>head</c> terminal command (`P4-W5-CMD-003`).
/// Outputs the first N lines of stdin or virtual files (default 10).
/// </summary>
public sealed class HeadCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.head",
        Name = "head",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Output the first part of files",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Head.dll", "HackerOs.Commands.Head.HeadCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.FileSystemUserHomeRead],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("head", [], "head [-n count] [file...]"),
        SingleInstancePerUser = false
    };

    public HeadCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        int maxLines = 10;
        List<string> files = [];

        var args = context.Arguments;
        for (int i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a == "-n" && i + 1 < args.Count && int.TryParse(args[++i], out var n)) maxLines = n;
            else if (!a.StartsWith('-')) files.Add(a);
        }

        if (files.Count == 0)
        {
            int count = 0;
            string? line;
            while (count < maxLines && (line = await context.StandardInput.ReadLineAsync(cancellationToken)) != null)
            {
                context.StandardOutput.WriteLine(line);
                count++;
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
                    context.StandardError.WriteLine($"head: {file}: No such file or directory");
                    continue;
                }

                if (files.Count > 1) context.StandardOutput.WriteLine($"==> {file} <==");
                using var reader = new StreamReader(res.Value.Content);
                int count = 0;
                string? line;
                while (count < maxLines && (line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    context.StandardOutput.WriteLine(line);
                    count++;
                }
            }
        }

        return 0;
    }

    private static string ResolvePath(string cwd, string path) =>
        path.StartsWith('/') ? path : (cwd.TrimEnd('/') + "/" + path).Replace("//", "/");
}
