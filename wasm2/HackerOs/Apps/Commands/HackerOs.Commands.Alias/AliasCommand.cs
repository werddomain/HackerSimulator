using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Commands.Alias;

/// <summary>
/// Implements the <c>alias</c> / <c>addalias</c> / <c>rmalias</c> / <c>unalias</c> terminal
/// command (`P4-W5-CMD-008`). Manages terminal command aliases, persisted per-user at
/// <c>~/.hackeros/aliases</c> so they survive across terminal sessions.
/// </summary>
/// <remarks>
/// <c>rmalias</c>/<c>unalias</c> are dispatched here as <c>alias -r &lt;name...&gt;</c> by the
/// terminal's alias-expansion step (see <c>TerminalWindow.razor</c>), since a command instance
/// has no way to observe which of its manifest-declared aliases it was invoked as.
/// </remarks>
public sealed class AliasCommand : TerminalAppBase
{
    public static AppManifest StaticManifest { get; } = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.commands.alias",
        Name = "alias",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Manage command aliases",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Commands.Alias.dll", "HackerOs.Commands.Alias.AliasCommand"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
        Capabilities = [AppCapabilities.FileSystemUserHomeRead, AppCapabilities.FileSystemUserHomeWrite],
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("alias", ["addalias", "rmalias", "unalias"], "alias [-r] [name[=value]...]"),
        SingleInstancePerUser = false
    };

    public AliasCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        bool remove = false;
        List<string> positional = [];
        foreach (string arg in context.Arguments)
        {
            if (arg is "-r" or "--remove")
            {
                remove = true;
            }
            else
            {
                positional.Add(arg);
            }
        }

        string home = context.Environment.TryGetValue("HOME", out string? envHome) ? envHome : "/home/user";
        VirtualPath aliasFilePath = VirtualPath.Parse($"{home.TrimEnd('/')}/.hackeros/aliases");

        (Dictionary<string, string> aliases, long? fileRevision) = await LoadAliasesAsync(
            context, aliasFilePath, cancellationToken);

        if (remove)
        {
            if (positional.Count == 0)
            {
                context.StandardError.WriteLine("unalias: usage: unalias <name...>");
                return 1;
            }

            int status = 0;
            bool changed = false;
            foreach (string name in positional)
            {
                if (aliases.Remove(name))
                {
                    changed = true;
                }
                else
                {
                    context.StandardError.WriteLine($"unalias: {name}: not found");
                    status = 1;
                }
            }

            if (changed && !await SaveAliasesAsync(context, aliasFilePath, home, aliases, fileRevision, cancellationToken))
            {
                context.StandardError.WriteLine("unalias: failed to save aliases");
                status = 1;
            }

            return status;
        }

        if (positional.Count == 0)
        {
            foreach ((string name, string value) in aliases.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                context.StandardOutput.WriteLine($"alias {name}='{value}'");
            }
            return 0;
        }

        int exitCode = 0;
        bool wrote = false;
        foreach (string arg in positional)
        {
            int eqIdx = arg.IndexOf('=');
            if (eqIdx > 0)
            {
                string name = arg[..eqIdx];
                string val = arg[(eqIdx + 1)..].Trim('\'', '"');
                aliases[name] = val;
                wrote = true;
            }
            else if (aliases.TryGetValue(arg, out string? val))
            {
                context.StandardOutput.WriteLine($"alias {arg}='{val}'");
            }
            else
            {
                context.StandardError.WriteLine($"alias: {arg}: not found");
                exitCode = 1;
            }
        }

        if (wrote && !await SaveAliasesAsync(context, aliasFilePath, home, aliases, fileRevision, cancellationToken))
        {
            context.StandardError.WriteLine("alias: failed to save aliases");
            exitCode = 1;
        }

        return exitCode;
    }

    private static async Task<(Dictionary<string, string> Aliases, long? FileRevision)> LoadAliasesAsync(
        TerminalExecutionContext context, VirtualPath filePath, CancellationToken cancellationToken)
    {
        Dictionary<string, string> aliases = new(StringComparer.Ordinal);

        var stat = await context.App.FileSystem.StatAsync(new FileSystemStatRequest(filePath), cancellationToken);
        if (!stat.Succeeded || stat.Value is null)
        {
            return (aliases, null);
        }

        var read = await context.App.FileSystem.ReadAsync(new FileSystemReadRequest(filePath), cancellationToken);
        if (!read.Succeeded || read.Value is null)
        {
            return (aliases, stat.Value.Metadata.Revision);
        }

        using StreamReader reader = new(read.Value.Content, Encoding.UTF8);
        string content = await reader.ReadToEndAsync(cancellationToken);
        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();
            int eqIdx = line.IndexOf('=');
            if (line.Length == 0 || eqIdx <= 0)
            {
                continue;
            }
            aliases[line[..eqIdx]] = line[(eqIdx + 1)..];
        }

        return (aliases, stat.Value.Metadata.Revision);
    }

    private static async Task<bool> SaveAliasesAsync(
        TerminalExecutionContext context,
        VirtualPath filePath,
        string home,
        Dictionary<string, string> aliases,
        long? fileRevision,
        CancellationToken cancellationToken)
    {
        string content = string.Join(
            '\n', aliases.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));

        if (fileRevision is { } revision)
        {
            var writeResult = await context.App.FileSystem.WriteAsync(
                new FileSystemWriteRequest(filePath, revision), new Utf8TextSource(content), cancellationToken);
            return writeResult.Succeeded;
        }

        string dirPath = GetParentPath(filePath.Value);
        var dirStat = await context.App.FileSystem.StatAsync(new FileSystemStatRequest(VirtualPath.Parse(dirPath)), cancellationToken);
        if (!dirStat.Succeeded || dirStat.Value is null)
        {
            var homeStat = await context.App.FileSystem.StatAsync(new FileSystemStatRequest(VirtualPath.Parse(home)), cancellationToken);
            if (!homeStat.Succeeded || homeStat.Value is null)
            {
                return false;
            }

            var createDirResult = await context.App.FileSystem.CreateAsync(
                new FileSystemCreateRequest(
                    VirtualPath.Parse(dirPath), FileSystemEntryKind.Directory,
                    FileSystemPermissions.FromMode(0b111_101_101), homeStat.Value.Metadata.Revision),
                cancellationToken);
            if (!createDirResult.Succeeded || createDirResult.Entry is null)
            {
                return false;
            }
            dirStat = FileSystemResult<FileSystemEntrySnapshot>.Success(createDirResult.Entry);
        }

        var createFileResult = await context.App.FileSystem.CreateAsync(
            new FileSystemCreateRequest(
                filePath, FileSystemEntryKind.File,
                FileSystemPermissions.FromMode(0b110_100_100), dirStat.Value!.Metadata.Revision),
            cancellationToken);
        if (!createFileResult.Succeeded || createFileResult.Entry is null)
        {
            return false;
        }

        var writeAfterCreate = await context.App.FileSystem.WriteAsync(
            new FileSystemWriteRequest(filePath, createFileResult.Entry.Metadata.Revision),
            new Utf8TextSource(content), cancellationToken);
        return writeAfterCreate.Succeeded;
    }

    private static string GetParentPath(string canonicalPath)
    {
        int lastSlash = canonicalPath.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : canonicalPath[..lastSlash];
    }

    private sealed class Utf8TextSource(string content) : IFileSystemContentSource
    {
        private readonly byte[] _bytes = Encoding.UTF8.GetBytes(content);
        public FileSystemContentDescriptor Descriptor { get; } = FileSystemContentDescriptor.Text();
        public long? Length => _bytes.LongLength;
        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Stream>(new MemoryStream(_bytes, writable: false));
        }
    }
}
