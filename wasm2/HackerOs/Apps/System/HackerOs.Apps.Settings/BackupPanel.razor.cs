using System.Globalization;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Administration;
using HackerOs.AppSdk.IO;

namespace HackerOs.Apps.Settings;

/// <summary>
/// Settings &gt; Backup (ADR 0099, section 1): download a backup of the whole machine to the host
/// computer, or restore one picked there, merging or replacing.
/// </summary>
/// <remarks>
/// Uses only the public SDK (ADR 0055): <see cref="HackSystemBackup"/> (capability
/// <c>system.backup</c>). The OS asks an administrator in its own window, opens the host's file
/// picker and restarts after a restore; this panel never sees a password or the archive. The
/// restore call starts from the button's click so the browser accepts to open its file picker.
/// </remarks>
public partial class BackupPanel
{
    private enum Operation
    {
        None,
        Create,
        Restore
    }

    private Operation _operation;
    private HackBackupRestoreMode _mode = HackBackupRestoreMode.Merge;
    private HackBackupInfo? _lastBackup;
    private string? _error;

    private bool IsBusy => _operation != Operation.None;

    private IAppExecutionContext Context => AppContext
        ?? throw new InvalidOperationException("This panel must be rendered inside the Settings window.");

    private string ModeClass(HackBackupRestoreMode mode) =>
        mode == _mode ? "restore-mode is-selected" : "restore-mode";

    private async Task CreateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _operation = Operation.Create;
        _error = null;
        try
        {
            _lastBackup = await HackSystemBackup.CreateAsync(Context, Context.CancellationToken);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or NotSupportedException or IOException or InvalidOperationException)
        {
            _error = $"Backup failed: {exception.Message}";
        }
        finally
        {
            _operation = Operation.None;
        }
    }

    private async Task RestoreAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _operation = Operation.Restore;
        _error = null;
        try
        {
            // Returns null when the picker was closed; otherwise the machine restarts.
            await HackSystemBackup.RestoreAsync(Context, _mode, Context.CancellationToken);
        }
        catch (HackBackupInvalidException exception)
        {
            _error = $"This file cannot be restored: {exception.Message}";
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or NotSupportedException or IOException or InvalidOperationException)
        {
            _error = $"Restore failed: {exception.Message}";
        }
        catch (OperationCanceledException) when (Context.CancellationToken.IsCancellationRequested)
        {
            // The session is ending for the restart.
        }
        finally
        {
            _operation = Operation.None;
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KiB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.#} MiB")
    };
}
