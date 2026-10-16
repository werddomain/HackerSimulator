using HackerOs.AppSdk.Administration;
using HackerOs.AppSdk.Apps;
using HackerOs.AppSdk.Settings;
using HackerOs.Simulation.Abstractions.Settings;
using HackerOs.Theming.Abstractions;

namespace HackerOs.Apps.Settings;

/// <summary>
/// The Settings app's window: appearance, file associations, granted capabilities, permissions,
/// installed apps, optional server connection and system (restart, factory reset).
/// </summary>
/// <remarks>
/// Uses only the public SDK (ADR 0055, ADR 0056 sections 8 to 11):
/// <list type="bullet">
/// <item><see cref="HackSettings.OpenSystemAsync(string, CancellationToken)"/> for
/// <c>appearance.config</c>; the shell applies a new theme by itself as soon as the file changes;</item>
/// <item><see cref="HackApps"/> for the file handlers of the installed apps;</item>
/// <item><see cref="HackServerConnection"/> (capability <c>server.connection.manage</c>) for the
/// optional server;</item>
/// <item><see cref="HackSystem"/> (capability <c>system.power</c>) for restart and factory reset;
/// the OS asks an administrator in its own window, this app never sees the password.</item>
/// </list>
/// </remarks>
public partial class SettingsWindow : IAsyncDisposable
{
    /// <summary>OS document holding the look of the shell (ADR 0044 section 8).</summary>
    internal const string AppearanceDocumentId = "appearance";

    /// <summary>Key of the desktop theme id in <c>appearance.config</c>.</summary>
    internal const string DesktopThemeKey = "desktopThemeId";

    /// <summary>Key of the mobile theme id in <c>appearance.config</c>.</summary>
    internal const string MobileThemeKey = "mobileThemeId";

    /// <summary>Key of the accent id in <c>appearance.config</c>.</summary>
    internal const string AccentKey = "accent";

    /// <summary>Key of the animations switch in <c>appearance.config</c>.</summary>
    internal const string AnimationsKey = "animationsEnabled";

    private const string ResetConfirmPhrase = "RESET";

    private HackSettingsDocument? _appearance;
    private ThemePreferences _appearanceSettings = ThemePreferences.Default;
    private SettingLayer _targetLayer = SettingLayer.User;
    private bool _isSavingAppearance;
    private string? _appearanceMessage;
    private List<(string Extension, string AppName)> _fileHandlers = [];

    private bool _isResetDialogOpen;
    private bool _isResetting;
    private bool _isRebooting;
    private string _confirmText = string.Empty;
    private string? _resetErrorMessage;
    private string? _systemMessage;

    private HackServerConnectionState _connectionState = HackServerConnectionState.NotConnected;
    private string _serverUrl = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _isConnecting;
    private string? _connectionErrorMessage;
    private bool _isSyncing;
    private string? _syncStatusMessage;
    private int _conflictCount;

    private bool IsConfirmed => string.Equals(_confirmText, ResetConfirmPhrase, StringComparison.Ordinal);

    private bool CurrentDesktopThemeSupportsAccent =>
        ThemeCatalog.TryGet(_appearanceSettings.DesktopThemeId, out ThemeDefinition? theme)
        && theme is { SupportsAccentColor: true };

    private bool CanSubmitConnection =>
        Uri.TryCreate(_serverUrl, UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(_username) &&
        !string.IsNullOrWhiteSpace(_password);

    /// <inheritdoc />
    protected override async Task OnAppInitializedAsync()
    {
        await OpenAppearanceAsync();

        IReadOnlyList<HackAppInfo> apps = await HackApps.GetInstalledAsync(AppContext.CancellationToken);
        _fileHandlers = [.. apps
            .SelectMany(app => app.FileHandlers.SelectMany(handler =>
                handler.Extensions.Select(extension => (Extension: extension, AppName: app.Name))))
            .OrderBy(entry => entry.Extension, StringComparer.OrdinalIgnoreCase)];

        await RefreshConnectionAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_appearance is not null)
        {
            _appearance.Changed -= OnAppearanceChanged;
            await _appearance.DisposeAsync();
        }
    }

    private async Task OpenAppearanceAsync()
    {
        try
        {
            _appearance = await HackSettings.OpenSystemAsync(AppContext, AppearanceDocumentId, AppContext.CancellationToken);
            _appearance.Changed += OnAppearanceChanged;
            _appearanceSettings = ReadAppearance(_appearance);
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or UnauthorizedAccessException)
        {
            _appearanceMessage = $"The appearance settings cannot be read: {exception.Message}";
        }
    }

    /// <summary>An external edit (<c>nano /etc/hackeros/appearance.config</c>) or another window changed the file.</summary>
    private void OnAppearanceChanged(object? sender, HackSettingsChangedEventArgs args) =>
        _ = InvokeAsync(() =>
        {
            if (_appearance is not null && !_isSavingAppearance)
            {
                _appearanceSettings = ReadAppearance(_appearance);
            }

            StateHasChanged();
        });

    private static ThemePreferences ReadAppearance(HackSettingsDocument document) => new(
        document.Get<string>(DesktopThemeKey),
        document.Get<string>(MobileThemeKey),
        document.Get<string>(AccentKey),
        document.Get<bool>(AnimationsKey));

    private Task ChangeDesktopTheme(string themeId) => PersistAppearanceAsync(
        _appearanceSettings with { DesktopThemeId = themeId }, DesktopThemeKey, themeId);

    private Task ChangeMobileTheme(string themeId) => PersistAppearanceAsync(
        _appearanceSettings with { MobileThemeId = themeId }, MobileThemeKey, themeId);

    private Task ChangeAccent(string accentId) => PersistAppearanceAsync(
        _appearanceSettings with { AccentId = accentId }, AccentKey, accentId);

    private Task ToggleAnimations(bool value) => PersistAppearanceAsync(
        _appearanceSettings with { AnimationsEnabled = value }, AnimationsKey, value);

    private static string ThemeOptionClass(string themeId, string selectedThemeId) =>
        string.Equals(themeId, selectedThemeId, StringComparison.Ordinal)
            ? "theme-option is-selected"
            : "theme-option";

    private string AccentOptionClass(string accentId) =>
        string.Equals(accentId, _appearanceSettings.AccentId, StringComparison.Ordinal)
            ? $"accent-btn accent-{accentId} is-selected"
            : $"accent-btn accent-{accentId}";

    private string LayerOptionClass(SettingLayer layer) =>
        layer == _targetLayer ? "layer-option is-selected" : "layer-option";

    private void SelectLayer(SettingLayer layer)
    {
        _targetLayer = layer;
        _appearanceMessage = null;
    }

    /// <summary>Gets which layer currently provides <paramref name="key"/> (ADR 0044 section 3).</summary>
    private SettingLayer SourceOf(string key) => _appearance?.GetSource(key) ?? SettingLayer.Default;

    private bool IsLocked(string key) => _appearance?.IsLocked(key) == true;

    /// <summary>A locked key cannot be changed for oneself; the system default stays editable.</summary>
    private bool IsBlocked(string key) => _targetLayer == SettingLayer.User && IsLocked(key);

    private Task ApplyCurrentAsSystemDefaultAsync() => PersistAppearanceAsync(_appearanceSettings, key: null, value: null);

    /// <summary>The settings write in flight, if any (ADR 0069).</summary>
    private Task? _appearanceWrite;

    /// <summary>
    /// A stop request (logout, <c>kill</c>, "End task") waits for the settings write in flight: the
    /// write is one atomic edit of the settings file and runs to its end, so the change the user
    /// just made is either fully saved or reported, never half applied.
    /// </summary>
    protected override Task OnAppStopRequestedAsync(HackerOs.AppSdk.Diagnostics.HackStopRequestedEventArgs e) =>
        _appearanceWrite ?? Task.CompletedTask;

    /// <summary>
    /// Writes one changed key to the user layer, or the whole look to the system layer in one edit
    /// (the system default is what every user starts from, and one edit means one administrator
    /// approval).
    /// </summary>
    private async Task PersistAppearanceAsync(ThemePreferences next, string? key, object? value)
    {
        if (_isSavingAppearance || _appearance is null)
        {
            return;
        }

        _appearanceSettings = next;
        _appearanceMessage = null;
        _isSavingAppearance = true;
        StateHasChanged();

        // Not the process token: a stop request must let this one edit finish (see OnAppStopRequestedAsync).
        TaskCompletionSource written = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _appearanceWrite = written.Task;
        try
        {
            if (_targetLayer == SettingLayer.System || key is null)
            {
                // /etc/hackeros belongs to root: the write uses this app's own rights, so a denial
                // raises the administrator approval of ADR 0042.
                await _appearance.SetManyAsync(
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        [DesktopThemeKey] = next.DesktopThemeId,
                        [MobileThemeKey] = next.MobileThemeId,
                        [AccentKey] = next.AccentId,
                        [AnimationsKey] = next.AnimationsEnabled
                    },
                    SettingLayer.System,
                    CancellationToken.None);
                _appearanceMessage = "Saved as the default for every user.";
            }
            else
            {
                await _appearance.SetAsync(key, value!, SettingLayer.User, CancellationToken.None);
            }
        }
        catch (HackSettingLockedException exception)
        {
            _appearanceMessage = exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            _appearanceMessage = "Administrator approval is required to change the system default. Approve the prompt, then apply again.";
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException)
        {
            _appearanceMessage = $"Could not save the settings file: {exception.Message}";
        }
        finally
        {
            _isSavingAppearance = false;
            _appearanceWrite = null;
            written.TrySetResult();
        }

        _appearanceSettings = ReadAppearance(_appearance);
        StateHasChanged();
    }

    private async Task RefreshConnectionAsync()
    {
        try
        {
            _connectionState = await HackServerConnection.GetStateAsync(AppContext, AppContext.CancellationToken);
            _conflictCount = (await HackServerConnection.GetConflictsAsync(AppContext, AppContext.CancellationToken)).Count;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or NotSupportedException)
        {
            _connectionState = HackServerConnectionState.NotConnected;
            _connectionErrorMessage = $"The server connection is not available: {exception.Message}";
        }
    }

    private async Task ConnectAsync(bool createNewAccount)
    {
        if (!CanSubmitConnection || _isConnecting)
        {
            return;
        }

        _isConnecting = true;
        _connectionErrorMessage = null;
        StateHasChanged();

        try
        {
            Uri serverUrl = new(_serverUrl, UriKind.Absolute);
            _connectionState = createNewAccount
                ? await HackServerConnection.CreateAccountAsync(AppContext, serverUrl, _username, _password, AppContext.CancellationToken)
                : await HackServerConnection.ConnectAsync(AppContext, serverUrl, _username, _password, AppContext.CancellationToken);
            _password = string.Empty;
        }
        catch (Exception exception) when (exception is HackServerConnectionException or UnauthorizedAccessException)
        {
            _connectionErrorMessage = exception.Message;
        }
        finally
        {
            _isConnecting = false;
        }

        if (_connectionState.IsConnected)
        {
            // ADR 0029 Decision 7: sync once immediately after a successful connect.
            await SyncNowAsync();
        }
    }

    private async Task DisconnectAsync()
    {
        if (_isConnecting)
        {
            return;
        }

        await HackServerConnection.DisconnectAsync(AppContext, AppContext.CancellationToken);
        _connectionState = HackServerConnectionState.NotConnected;
        _serverUrl = string.Empty;
        _username = string.Empty;
        _password = string.Empty;
        _connectionErrorMessage = null;
        _syncStatusMessage = null;
        _conflictCount = 0;
    }

    private async Task SyncNowAsync()
    {
        if (_isSyncing || !_connectionState.IsConnected)
        {
            return;
        }

        _isSyncing = true;
        _syncStatusMessage = null;
        StateHasChanged();

        try
        {
            // ADR 0030: push before pull, so a concurrent edit is flagged as a conflict before the
            // pull runs. User settings are home files (ADR 0044), so they travel with this domain;
            // the appearance document's watch refreshes this window when the pull changed them.
            await HackServerConnection.SyncAsync(AppContext, HackSyncDomain.FileSystem, HackSyncDirection.Push, AppContext.CancellationToken);
            await HackServerConnection.SyncAsync(AppContext, HackSyncDomain.FileSystem, HackSyncDirection.Pull, AppContext.CancellationToken);

            // ADR 0031: grants are pull-only.
            await HackServerConnection.SyncAsync(AppContext, HackSyncDomain.Grants, HackSyncDirection.Pull, AppContext.CancellationToken);

            // ADR 0033: FileAssociations domain (the AppCatalog domain is retired by ADR 0048).
            await HackServerConnection.SyncAsync(AppContext, HackSyncDomain.FileAssociations, HackSyncDirection.Push, AppContext.CancellationToken);
            await HackServerConnection.SyncAsync(AppContext, HackSyncDomain.FileAssociations, HackSyncDirection.Pull, AppContext.CancellationToken);

            _conflictCount = (await HackServerConnection.GetConflictsAsync(AppContext, AppContext.CancellationToken)).Count;
            _syncStatusMessage = _conflictCount > 0
                ? $"Last synced {DateTimeOffset.Now:t} ({_conflictCount} file conflict(s))"
                : $"Last synced {DateTimeOffset.Now:t}";
        }
        catch (HackServerConnectionException exception)
        {
            _syncStatusMessage = exception.Message;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task RebootAsync()
    {
        if (_isRebooting)
        {
            return;
        }

        _isRebooting = true;
        _systemMessage = null;
        StateHasChanged();
        try
        {
            // The OS asks an administrator, flushes the logs, ends every process and reloads.
            await HackSystem.RebootAsync(AppContext, AppContext.CancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            _systemMessage = "No administrator confirmed the restart: nothing was done.";
        }
        catch (OperationCanceledException)
        {
            // The session ended under us: the machine is restarting.
        }
        finally
        {
            _isRebooting = false;
        }
    }

    private void OpenResetDialog()
    {
        _isResetDialogOpen = true;
        _confirmText = string.Empty;
        _resetErrorMessage = null;
    }

    private void CancelReset()
    {
        if (_isResetting)
        {
            return;
        }

        _isResetDialogOpen = false;
        _confirmText = string.Empty;
        _resetErrorMessage = null;
    }

    private async Task ConfirmResetAsync()
    {
        if (!IsConfirmed || _isResetting)
        {
            return;
        }

        _isResetting = true;
        _resetErrorMessage = null;

        try
        {
            // The OS asks an administrator, erases its database and restarts as freshly installed.
            await HackSystem.FactoryResetAsync(AppContext, AppContext.CancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            _resetErrorMessage = "No administrator confirmed the reset: nothing was erased.";
            _isResetting = false;
        }
        catch (OperationCanceledException)
        {
            // The session ended under us: the machine is restarting.
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or InvalidOperationException)
        {
            _resetErrorMessage = $"Reset failed: {exception.Message}";
            _isResetting = false;
        }
    }
}
