using HackerOs.Platform.Core.Settings;
using HackerOs.Simulation.Abstractions.Settings;
using HackerOs.Theming.Abstractions;

namespace HackerOs.Platform.Core.Appearance;

/// <summary>
/// Maintains the shell's live view of the layered appearance settings
/// (<see cref="AppearanceSettingsDocuments.Document"/>): the session user's
/// <c>~/.config/hackeros/appearance.config</c> over the machine's <c>/etc/hackeros/appearance.config</c>.
/// Writes go through <see cref="SetAsync"/>, so the file's permissions decide who may change what.
/// </summary>
public sealed class ThemePreferenceService : IDisposable
{
    private readonly IOsSettingsService _settings;

    /// <summary>Creates the service over the OS settings files.</summary>
    /// <param name="settings">OS settings service.</param>
    public ThemePreferenceService(IOsSettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.DocumentChanged += OnDocumentChanged;
    }

    /// <summary>Gets the current preferences, defaulting safely until initialization completes.</summary>
    public ThemePreferences Current { get; private set; } = ThemePreferences.Default;

    /// <summary>Gets the last merged snapshot, exposing each key's source layer and lock state.</summary>
    public LayeredConfigSnapshot? Snapshot { get; private set; }

    /// <summary>Raised after a refresh changes at least one effective preference.</summary>
    public event Action? Changed;

    /// <summary>Loads the initial appearance and starts watching its files, without emitting a change notification.</summary>
    /// <param name="cancellationToken">Signals cancellation of the settings read.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshCoreAsync(raiseEvent: false, cancellationToken).ConfigureAwait(false);
        await _settings.WatchAsync(AppearanceSettingsDocuments.Document, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reloads the appearance files and notifies subscribers when the effective value changed.</summary>
    /// <param name="cancellationToken">Signals cancellation of the settings read.</param>
    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(raiseEvent: true, cancellationToken);

    /// <summary>Writes every preference that differs from the current effective value to one layer.</summary>
    /// <param name="preferences">Desired preferences.</param>
    /// <param name="target">Layer to write; the user layer by default.</param>
    /// <param name="writer">Optional caller gateway whose rights decide the write (see <see cref="IOsSettingsService.SetAsync"/>).</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    public async Task SetAsync(
        ThemePreferences preferences,
        SettingLayer target = SettingLayer.User,
        Simulation.Abstractions.Gateways.IAppFileSystemGateway? writer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        OsSettingsDocument document = AppearanceSettingsDocuments.Document;
        (string Key, string Value)[] desired =
        [
            (AppearanceSettingsDocuments.DesktopThemeKey, preferences.DesktopThemeId),
            (AppearanceSettingsDocuments.MobileThemeKey, preferences.MobileThemeId),
            (AppearanceSettingsDocuments.AccentKey, preferences.AccentId),
            (AppearanceSettingsDocuments.AnimationsKey, preferences.AnimationsEnabled ? "true" : "false")
        ];

        LayeredConfigSnapshot current = Snapshot ?? await _settings.ReadAsync(document, cancellationToken).ConfigureAwait(false);
        foreach ((string key, string value) in desired)
        {
            if (current.Values[key] != value || (target == SettingLayer.System && current.Sources[key] == SettingLayer.Default))
            {
                await _settings.SetAsync(document, key, value, target, writer, cancellationToken).ConfigureAwait(false);
            }
        }

        await RefreshCoreAsync(raiseEvent: true, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => _settings.DocumentChanged -= OnDocumentChanged;

    private void OnDocumentChanged(object? sender, OsSettingsChangedEventArgs args)
    {
        if (args.Concerns(AppearanceSettingsDocuments.Document.DocumentId))
        {
            _ = RefreshInBackgroundAsync();
        }
    }

    private async Task RefreshInBackgroundAsync()
    {
        try
        {
            await RefreshCoreAsync(raiseEvent: true, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Keep the last good preferences; the read problem is already logged by the settings service.
        }
    }

    private async Task RefreshCoreAsync(bool raiseEvent, CancellationToken cancellationToken)
    {
        LayeredConfigSnapshot snapshot = await _settings
            .ReadAsync(AppearanceSettingsDocuments.Document, cancellationToken)
            .ConfigureAwait(false);
        ThemePreferences preferences = AppearanceSettingsDocuments.ToPreferences(snapshot);
        bool changed = preferences != Current;
        Snapshot = snapshot;
        Current = preferences;
        if (raiseEvent && changed)
        {
            Changed?.Invoke();
        }
    }
}
