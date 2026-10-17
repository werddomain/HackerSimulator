using System.Globalization;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Administration;
using HackerOs.AppSdk.IO;
using HackerOs.Commands.Common;

namespace HackerOs.Commands.Hackbackup;

/// <summary>
/// <c>hackbackup create | restore [--merge|--replace]</c> (ADR 0099, section 1): the terminal side
/// of Settings &gt; Backup, through <see cref="HackSystemBackup"/>.
/// </summary>
/// <remarks>
/// <para>
/// Like a system backup tool, it must run as root (<c>sudo hackbackup ...</c>): an archive holds
/// every user's files and the password hashes. Without <c>sudo</c> it stops at once instead of
/// opening the graphical administrator prompt, the way <c>apt</c> answers "are you root?".
/// </para>
/// <para>
/// <c>create</c> downloads the <c>.hosbak</c> archive to the host computer. <c>restore</c> opens
/// the browser's file picker (Enter counts as the user's gesture); the default mode is
/// <c>--merge</c>. If the browser refuses to open its picker, the command points to Settings.
/// </para>
/// </remarks>
public sealed class HackbackupCommand(AppManifest manifest) : TerminalAppBase(manifest)
{
    private const string Usage = "usage: hackbackup create | restore [--merge|--replace]";

    /// <inheritdoc />
    public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<string> args = context.Arguments;
        if (args.Count == 0 || args[0] is "-h" or "--help" or "help")
        {
            await (args.Count == 0 ? context.StandardError : context.StandardOutput).WriteLineAsync(Usage);
            return args.Count == 0 ? 2 : 0;
        }

        string verb = args[0];
        if (verb is not ("create" or "restore"))
        {
            await context.StandardError.WriteLineAsync($"hackbackup: unknown command '{verb}'");
            await context.StandardError.WriteLineAsync(Usage);
            return 2;
        }

        HackBackupRestoreMode mode = HackBackupRestoreMode.Merge;
        foreach (string option in args.Skip(1))
        {
            switch (option)
            {
                case "--merge" when verb == "restore":
                    mode = HackBackupRestoreMode.Merge;
                    break;
                case "--replace" when verb == "restore":
                    mode = HackBackupRestoreMode.Replace;
                    break;
                default:
                    await context.StandardError.WriteLineAsync($"hackbackup: invalid option '{option}'");
                    await context.StandardError.WriteLineAsync(Usage);
                    return 2;
            }
        }

        if (!context.App.IsRootElevated)
        {
            await context.StandardError.WriteLineAsync(
                $"hackbackup: must be run as root (try: sudo hackbackup {string.Join(' ', args)})");
            return 1;
        }

        if (!HackSystemBackup.IsSupportedFor(context.App))
        {
            await context.StandardError.WriteLineAsync("hackbackup: this HackerOS host cannot back up the machine");
            return 1;
        }

        try
        {
            return verb == "create"
                ? await CreateAsync(context, cancellationToken)
                : await RestoreAsync(context, mode, cancellationToken);
        }
        catch (HackBackupInvalidException exception)
        {
            await context.StandardError.WriteLineAsync($"hackbackup: not a usable backup: {exception.Message}");
            return 1;
        }
        catch (InvalidOperationException exception) when (verb == "restore")
        {
            // The browser refused to open its file picker without a fresh user gesture.
            await context.StandardError.WriteLineAsync($"hackbackup: {exception.Message}");
            await context.StandardError.WriteLineAsync("hackbackup: use Settings > Backup > Restore... instead");
            return 1;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or NotSupportedException or IOException)
        {
            await context.StandardError.WriteLineAsync($"hackbackup: {ShellErrorText.Describe(exception)}");
            return 1;
        }
    }

    private static async Task<int> CreateAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
    {
        await context.StandardOutput.WriteLineAsync("hackbackup: exporting the whole machine...");
        HackBackupInfo backup = await HackSystemBackup.CreateAsync(context.App, cancellationToken);
        await context.StandardOutput.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"hackbackup: {backup.FileName} ({CommandIO.HumanSize(backup.SizeBytes)}, {backup.RecordCount} records) downloaded to the host computer"));
        return 0;
    }

    private static async Task<int> RestoreAsync(TerminalExecutionContext context, HackBackupRestoreMode mode, CancellationToken cancellationToken)
    {
        string modeName = mode == HackBackupRestoreMode.Replace ? "replace" : "merge";
        await context.StandardOutput.WriteLineAsync($"hackbackup: choose the backup in the browser's file dialog ({modeName})...");
        HackBackupInfo? restored = await HackSystemBackup.RestoreAsync(context.App, mode, cancellationToken);
        if (restored is null)
        {
            await context.StandardError.WriteLineAsync("hackbackup: no file chosen; nothing was restored");
            return 1;
        }

        // Normally unreachable: the session ends and the machine restarts during the restore.
        await context.StandardOutput.WriteLineAsync($"hackbackup: {restored.FileName} restored ({modeName}); restarting");
        return 0;
    }
}
