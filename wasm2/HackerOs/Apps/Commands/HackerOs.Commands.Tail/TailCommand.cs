using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Tail;

/// <summary>
/// Implements the <c>tail</c> terminal command (`P4-W5-CMD-003`).
/// Outputs the last N lines of stdin or virtual files (default 10).
/// </summary>
public sealed class TailCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.tail",
        Name = "tail",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Output the last part of files",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Tail.dll", "HackerOs.Commands.Tail.TailCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.FileSystemUserHomeRead],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("tail", [], "tail [-n count] [file...]"),
        SingleInstancePerUser = false
    };

    public TailCommand(AppManifest manifest) : base(manifest) { }

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
            Queue<string> queue = new();
            string? line;
            while ((line = await context.StandardInput.ReadLineAsync(cancellationToken)) != null)
            {
                queue.Enqueue(line);
                if (queue.Count > maxLines) queue.Dequeue();
            }
            foreach (var l in queue) context.StandardOutput.WriteLine(l);
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
                    context.StandardError.WriteLine($"tail: {file}: No such file or directory");
                    continue;
                }

                if (files.Count > 1) context.StandardOutput.WriteLine($"==> {file} <==");
                using var reader = new StreamReader(res.Value.Content);
                Queue<string> queue = new();
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    queue.Enqueue(line);
                    if (queue.Count > maxLines) queue.Dequeue();
                }
                foreach (var l in queue) context.StandardOutput.WriteLine(l);
            }
        }

        return 0;
    }

    private static string ResolvePath(string cwd, string path) =>
        path.StartsWith('/') ? path : (cwd.TrimEnd('/') + "/" + path).Replace("//", "/");
}
