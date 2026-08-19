using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;
using HackerOs.Theming.Abstractions;

namespace HackerOs.Platform.Core.Appearance;

/// <summary>
/// Maintains the shell's read-only projection of the canonical appearance settings document.
/// Writes remain the responsibility of an authorized settings app, keeping shell rendering free
/// from hidden preference mutations.
/// </summary>
public sealed class ThemePreferenceService
{
    private static readonly AppOperationContext SystemReadContext = new()
    {
        AppId = "org.hackeros.shell",
        UserId = "system",
        UserAuthority = AppAuthority.System,
        GrantedCapabilities = new HashSet<string>([AppCapabilities.SettingsSystemRead], StringComparer.Ordinal),
        IsSystemOperation = true
    };

    private readonly ISettingsDocumentService _settings;

    /// <summary>Creates a scoped theme preference projection over the canonical settings service.</summary>
    /// <param name="settings">Settings service used only to read the appearance document.</param>
    public ThemePreferenceService(ISettingsDocumentService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>Gets the current preferences, defaulting safely until initialization completes.</summary>
    public ThemePreferences Current { get; private set; } = ThemePreferences.Default;

    /// <summary>Raised after a refresh changes at least one effective preference.</summary>
    public event Action? Changed;

    /// <summary>Loads the initial appearance projection without emitting a change notification.</summary>
    /// <param name="cancellationToken">Signals cancellation of the settings read.</param>
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(raiseEvent: false, cancellationToken);

    /// <summary>Reloads the appearance document and notifies subscribers when its effective value changed.</summary>
    /// <param name="cancellationToken">Signals cancellation of the settings read.</param>
    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(raiseEvent: true, cancellationToken);

    private async Task RefreshCoreAsync(bool raiseEvent, CancellationToken cancellationToken)
    {
        SettingsReadResult read = await _settings.ReadAsync(
            AppearanceSettingsDocuments.Path,
            SystemReadContext,
            cancellationToken).ConfigureAwait(false);

        if (read is not { Status: SettingsReadStatus.Success, Document: { } document }
            || !AppearanceSettingsCodec.TryDecode(document.Content, out ThemePreferences preferences))
        {
            return;
        }

        bool changed = preferences != Current;
        Current = preferences;
        if (raiseEvent && changed)
        {
            Changed?.Invoke();
        }
    }
}
