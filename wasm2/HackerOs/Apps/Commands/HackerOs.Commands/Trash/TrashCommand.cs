using System.Globalization;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.IO;
using HackerOs.Commands.Rm;

namespace HackerOs.Commands.Trash;

/// <summary>
/// Implements the <c>trash</c> terminal command (ADR 0052 section 4), modelled on <c>trash-cli</c>:
/// <list type="bullet">
/// <item><c>trash put [-f] &lt;file...&gt;</c> — moves files or whole directories to the trash (no <c>-r</c> needed, like <c>trash-put</c>);</item>
/// <item><c>trash list</c> — one line per element, <c>DeletionDate  Path</c>, oldest first;</item>
/// <item><c>trash restore &lt;name|original path&gt; [destination]</c> — the most recent match when a path was trashed several times;</item>
/// <item><c>trash rm &lt;name...&gt;</c> — deletes elements permanently;</item>
/// <item><c>trash empty</c> — empties the trash.</item>
/// </list>
/// Every subcommand goes through <see cref="HackTrash"/>, so it acts on the effective user's trash
/// (<c>/root/.local/share/Trash</c> under <c>sudo</c>).
/// </summary>
public sealed class TrashCommand : TerminalAppBase
{
    private const string Usage =
        "usage: trash put [-f] <file...>\n" +
        "       trash list\n" +
        "       trash restore <name|original-path> [destination]\n" +
        "       trash rm <name...>\n" +
        "       trash empty";

    public TrashCommand(AppManifest manifest) : base(manifest) { }

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Arguments.Count == 0)
        {
            context.StandardError.WriteLine(Usage);
            return 1;
        }

        string[] rest = [.. context.Arguments.Skip(1)];
        try
        {
            return context.Arguments[0] switch
            {
                "put" => await PutAsync(context, rest, cancellationToken),
                "list" or "ls" => await ListAsync(context, rest, cancellationToken),
                "restore" => await RestoreAsync(context, rest, cancellationToken),
                "rm" => await RemoveAsync(context, rest, cancellationToken),
                "empty" => await EmptyAsync(context, rest, cancellationToken),
                "--help" or "-h" or "help" => PrintUsage(context),
                string other => Fail(context, $"unknown subcommand '{other}'\n{Usage}")
            };
        }
        catch (UnauthorizedAccessException)
        {
            return Fail(context, "Permission denied");
        }
        catch (IOException exception)
        {
            return Fail(context, exception.Message);
        }
    }

    private static async Task<int> PutAsync(TerminalExecutionContext context, string[] arguments, CancellationToken cancellationToken)
    {
        if (!RmOptions.TryParse(arguments, out RmOptions options, out string? error))
        {
            return Fail(context, $"put: {error}");
        }

        if (options.Permanent)
        {
            return Fail(context, "put: --permanent is not a trash operation; use 'rm --permanent'");
        }

        if (options.Targets.Count == 0)
        {
            return options.Force ? 0 : Fail(context, "put: missing operand");
        }

        // trash-put sends directories without asking for -r.
        RmOptions trashOptions = options with { Recursive = true };
        int status = 0;
        foreach (string target in options.Targets)
        {
            if (!await RmCommand.RemoveAsync(context, target, trashOptions, cancellationToken))
            {
                status = 1;
            }
        }

        return status;
    }

    private static async Task<int> ListAsync(TerminalExecutionContext context, string[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length > 0)
        {
            return Fail(context, "list: too many arguments");
        }

        foreach (HackTrashItem item in await HackTrash.GetItemsAsync(cancellationToken))
        {
            string date = item.DeletedAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "????-??-?? ??:??:??";
            string path = item.OriginalPath ?? "(unknown origin)";

            // Show the trash name when it is not obvious from the path, so `trash rm <name>` is usable.
            string suffix = item.OriginalPath is null || HackPath.GetFileName(item.OriginalPath) != item.Name ? $"  [{item.Name}]" : string.Empty;
            context.StandardOutput.WriteLine($"{date}  {path}{suffix}");
        }

        return 0;
    }

    private static async Task<int> RestoreAsync(TerminalExecutionContext context, string[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length is 0 or > 2)
        {
            return Fail(context, "restore: expected <name|original-path> [destination]");
        }

        IReadOnlyList<HackTrashItem> items = await HackTrash.GetItemsAsync(cancellationToken);
        HackTrashItem? item = Find(items, context, arguments[0]);
        if (item is null)
        {
            return Fail(context, $"restore: '{arguments[0]}' is not in the trash");
        }

        string? destination = arguments.Length == 2 ? Resolve(context, arguments[1]) : null;
        await HackTrash.RestoreAsync(item, destination, cancellationToken);
        return 0;
    }

    private static async Task<int> RemoveAsync(TerminalExecutionContext context, string[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
        {
            return Fail(context, "rm: missing trash element name");
        }

        IReadOnlyList<HackTrashItem> items = await HackTrash.GetItemsAsync(cancellationToken);
        int status = 0;
        foreach (string name in arguments)
        {
            HackTrashItem? item = items.FirstOrDefault(candidate => candidate.Name == name);
            if (item is null)
            {
                context.StandardError.WriteLine($"trash: rm: '{name}' is not in the trash");
                status = 1;
                continue;
            }

            await HackTrash.DeleteAsync(item, cancellationToken);
        }

        return status;
    }

    private static async Task<int> EmptyAsync(TerminalExecutionContext context, string[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length > 0)
        {
            return Fail(context, "empty: too many arguments");
        }

        await HackTrash.EmptyAsync(cancellationToken);
        return 0;
    }

    /// <summary>Finds an element by trash name first, then by original path (most recent deletion wins).</summary>
    private static HackTrashItem? Find(IReadOnlyList<HackTrashItem> items, TerminalExecutionContext context, string key)
    {
        HackTrashItem? byName = items.FirstOrDefault(item => item.Name == key);
        if (byName is not null)
        {
            return byName;
        }

        string? path = Resolve(context, key);
        return items
            .Where(item => item.OriginalPath == path)
            .OrderByDescending(item => item.DeletedAt)
            .FirstOrDefault();
    }

    /// <summary>Resolves a user-typed path against the shell's working directory; <see langword="null"/> when invalid.</summary>
    private static string? Resolve(TerminalExecutionContext context, string path)
    {
        try
        {
            return path.StartsWith('~') ? HackPath.GetFullPath(path) : HackPath.GetFullPath(path, context.WorkingDirectory);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static int PrintUsage(TerminalExecutionContext context)
    {
        context.StandardOutput.WriteLine(Usage);
        return 0;
    }

    private static int Fail(TerminalExecutionContext context, string message)
    {
        context.StandardError.WriteLine($"trash: {message}");
        return 1;
    }
}
