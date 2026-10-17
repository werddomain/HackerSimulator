using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Administration;
using HackerOs.AppSdk.Apps;
using HackerOs.AppSdk.IO;
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

    private const string ThemeEditorAppId = "org.hackeros.theme-editor";
    private HackSettingsDocument? _appearance;
    private UserThemeLibraryResult _userThemes = UserThemeLibraryResult.Empty;
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
    private HackSyncStatus _syncStatus = HackSyncStatus.Inactive;
    private IReadOnlyList<HackSyncConflict> _conflicts = [];

    /// <summary>Index of the "Application" tab, the last one (ADR 0096 §4).</summary>
    private const int AppPageTabIndex = 7;

    /// <summary>Prefix of the argument naming the app whose page to show (<c>--app=&lt;id&gt;</c>).</summary>
    internal const string AppArgumentPrefix = "--app=";

    private int _activeTab;
    private string? _appPageId;

    private bool IsConfirmed => string.Equals(_confirmText, ResetConfirmPhrase, StringComparison.Ordinal);

    // A user theme takes the accent behavior of its base theme (ADR 0103).
    private bool CurrentDesktopThemeSupportsAccent =>
        ThemeCatalog.TryGet(
            ResolvedTheme.Resolve(_appearanceSettings.DesktopThemeId, ThemePlatform.Desktop, _userThemes.Themes).BaseThemeId,
            out ThemeDefinition? theme)
        && theme is { SupportsAccentColor: true };

    private bool CanSubmitConnection =>
        Uri.TryCreate(_serverUrl, UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(_username) &&
        !string.IsNullOrWhiteSpace(_password);

    /// <inheritdoc />
    protected override async Task OnAppInitializedAsync()
    {
        ShowAppPage(LaunchArguments);
        await OpenAppearanceAsync();
        await RefreshUserThemesAsync();

        IReadOnlyList<HackAppInfo> apps = await HackApps.GetInstalledAsync(AppContext.CancellationToken);
        _fileHandlers = [.. apps
            .SelectMany(app => app.FileHandlers.SelectMany(handler =>
                handler.Extensions.Select(extension => (Extension: extension, AppName: app.Name))))
            .OrderBy(entry => entry.Extension, StringComparer.OrdinalIgnoreCase)];

        await RefreshConnectionAsync();
    }

    /// <summary>
    /// Settings is a single instance: another app's <c>ShowAppSettingsIntent</c> reaches the open
    /// window here with <c>--app=&lt;id&gt;</c> (ADR 0096, sections 4 and 5).
    /// </summary>
    protected override Task OnIntentReceivedAsync(AppIntentDelivery intent)
    {
        ShowAppPage(intent.Arguments);
        return Task.CompletedTask;
    }

    /// <summary>Opens the "Application" tab on the app named by <c>--app=&lt;id&gt;</c>, when an argument names one.</summary>
    private void ShowAppPage(IReadOnlyList<string> arguments)
    {
        if (ReadAppArgument(arguments) is { } appId)
        {
            _appPageId = appId;
            _activeTab = AppPageTabIndex;
        }
    }

    /// <summary>Reads <c>--app=&lt;id&gt;</c> from Settings' command line; the last one wins, an empty id is ignored.</summary>
    internal static string? ReadAppArgument(IReadOnlyList<string> arguments) =>
        arguments
            .Where(static argument => argument.StartsWith(AppArgumentPrefix, StringComparison.Ordinal))
            .Select(static argument => argument[AppArgumentPrefix.Length..].Trim())
            .LastOrDefault(static id => id.Length > 0);
    /// <summary>Re-reads the user themes when the window comes back to the front (after the Theme Editor saved one).</summary>
    protected override Task OnWindowActivatedAsync() => RefreshUserThemesAsync();

    /// <summary>
    /// Reads the user themes of <c>/etc/hackeros/themes</c> and <c>~/.config/hackeros/themes</c>
    /// (ADR 0103 §2.2) with the SDK's file API; a missing folder simply means no theme.
    /// </summary>
    private async Task RefreshUserThemesAsync()
    {
        try
        {
            _userThemes = await UserThemeLibrary.LoadAsync(
                async (directory, token) => await HackDirectory.GetFilesAsync(directory, "*" + ThemeFile.Extension, cancellationToken: token),
                (path, token) => HackFile.ReadAllTextAsync(path, token),
                HackUser.Describe(AppContext).HomeDirectory,
                AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is HackIOException or HackUnauthorizedAccessException)
        {
            _userThemes = UserThemeLibraryResult.Empty;
        }
    }

    /// <summary>User themes offered on a form factor, in catalog order.</summary>
    private IEnumerable<UserTheme> UserThemesFor(ThemePlatform platform) =>
        _userThemes.Themes.Where(theme => theme.Supports(platform));

    /// <summary>Starts the Theme Editor, on a theme file when one is given.</summary>
    private async Task OpenThemeEditorAsync(string? themePath)
    {
        try
        {
            await HackApps.LaunchAsync(ThemeEditorAppId, themePath is null ? [] : [themePath], AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is HackAppLaunchException or HackUnauthorizedAccessException)
        {
            _appearanceMessage = exception.Message;
        }
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
            await RefreshSyncAsync();
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
            // ADR 0101 section 4: one complete pass in the OS's order (FileSystem push then pull, so a
            // concurrent edit is caught as a conflict first; Grants pull; FileAssociations). User
            // settings are home files (ADR 0044), so they travel with the FileSystem domain.
            _syncStatus = await HackServerConnection.SyncNowAsync(AppContext, AppContext.CancellationToken);
            await RefreshSyncAsync();
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

    /// <summary>Reads the periodic-sync state and the pending conflicts (ADR 0101).</summary>
    private async Task RefreshSyncAsync()
    {
        _syncStatus = await HackServerConnection.GetSyncStatusAsync(AppContext, AppContext.CancellationToken);
        _conflicts = await HackServerConnection.GetConflictsAsync(AppContext, AppContext.CancellationToken);
        _conflictCount = _conflicts.Count;
    }

    private string SyncStateLabel =>
        !_connectionState.IsConnected ? "off (not connected to a server)"
        : !_syncStatus.IsActive ? "inactive"
        : _syncStatus.IsPaused ? "paused"
        : _syncStatus.IsOnline ? "running"
        : "waiting for the server";

    private static string FormatInterval(TimeSpan interval) =>
        interval.TotalMinutes is 1 ? "minute" : $"{interval.TotalMinutes:0} minutes";

    private async Task SetSyncPausedAsync(bool paused)
    {
        try
        {
            await HackServerConnection.SetSyncPausedAsync(AppContext, paused, AppContext.CancellationToken);
            await RefreshSyncAsync();
            _syncStatus = _syncStatus with { IsPaused = paused };
        }
        catch (Exception exception) when (exception is HackServerConnectionException or UnauthorizedAccessException or IOException)
        {
            _syncStatusMessage = $"Could not change the setting: {exception.Message}";
        }
    }

    private async Task ShowConflictsAsync()
    {
        await HackServerConnection.ShowConflictsAsync(AppContext, AppContext.CancellationToken);
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
