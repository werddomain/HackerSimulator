using HackerOs.Platform.Core.Appearance;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Theming.Abstractions;

namespace HackerOs.Apps.Settings;

/// <summary>
/// Reads and writes the real <c>/etc/hackeros/appearance.json</c> settings document through the
/// app's <see cref="IAppSettingsGateway"/>, so desktop theme, mobile theme, accent, and motion
/// choices persist across sessions instead of living only in the window's local fields.
/// </summary>
public sealed class AppearancePersistenceService(IAppSettingsGateway settings)
{
    /// <summary>Reads the current appearance settings, falling back to defaults if unreadable.</summary>
    public async Task<ThemePreferences> ReadAsync(CancellationToken cancellationToken = default)
    {
        SettingsReadResult read = await settings.ReadAsync(AppearanceSettingsDocuments.Path, cancellationToken);
        return read.Status == SettingsReadStatus.Success
            && read.Document is not null
            && AppearanceSettingsCodec.TryDecode(read.Document.Content, out ThemePreferences preferences)
                ? preferences
                : ThemePreferences.Default;
    }

    /// <summary>Persists new appearance settings using an optimistic read-then-write.</summary>
    /// <param name="preferences">The complete, validated preference value to persist.</param>
    /// <param name="cancellationToken">Signals cancellation of the settings operation.</param>
    /// <returns><see langword="true"/> when the write committed.</returns>
    public async Task<bool> WriteAsync(
        ThemePreferences preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        SettingsReadResult read = await settings.ReadAsync(AppearanceSettingsDocuments.Path, cancellationToken);
        if (read.Status != SettingsReadStatus.Success || read.Document is null)
        {
            return false;
        }

        SettingsWriteResult write = await settings.WriteAsync(
            new SettingsWriteRequest(
                AppearanceSettingsDocuments.Path,
                AppearanceSettingsCodec.Encode(preferences),
                read.Document.Revision),
            cancellationToken);
        return write.Status == SettingsWriteStatus.Success;
    }
}
