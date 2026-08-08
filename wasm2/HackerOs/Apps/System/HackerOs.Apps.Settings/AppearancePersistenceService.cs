using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Apps.Settings;

/// <summary>Desktop accent color and animation preference, decoded from the appearance document.</summary>
public sealed record AppearanceSettings(string Accent, bool AnimationsEnabled)
{
    /// <summary>Gets the clean-profile default (matches <c>AppearanceSettingsDocuments.EmptyDocumentContent</c>).</summary>
    public static AppearanceSettings Default { get; } = new("green", true);
}

/// <summary>
/// Reads and writes the real <c>/etc/hackeros/appearance.json</c> settings document through the
/// app's <see cref="IAppSettingsGateway"/>, so accent/animation choices persist across sessions
/// instead of living only in the window's local fields.
/// </summary>
public sealed class AppearancePersistenceService(IAppSettingsGateway settings)
{
    private static readonly VirtualPath DocumentPath = VirtualPath.Parse("/etc/hackeros/appearance.json");

    /// <summary>Reads the current appearance settings, falling back to defaults if unreadable.</summary>
    public async Task<AppearanceSettings> ReadAsync(CancellationToken cancellationToken = default)
    {
        SettingsReadResult read = await settings.ReadAsync(DocumentPath, cancellationToken);
        return read.Status == SettingsReadStatus.Success && read.Document is not null
            ? Parse(read.Document.Content)
            : AppearanceSettings.Default;
    }

    /// <summary>Persists new appearance settings using an optimistic read-then-write.</summary>
    /// <returns><see langword="true"/> when the write committed.</returns>
    public async Task<bool> WriteAsync(string accent, bool animationsEnabled, CancellationToken cancellationToken = default)
    {
        SettingsReadResult read = await settings.ReadAsync(DocumentPath, cancellationToken);
        if (read.Status != SettingsReadStatus.Success || read.Document is null)
        {
            return false;
        }

        string content = $$"""{"schemaVersion":1,"accent":"{{accent}}","animationsEnabled":{{(animationsEnabled ? "true" : "false")}}}""";
        SettingsWriteResult write = await settings.WriteAsync(
            new SettingsWriteRequest(DocumentPath, content, read.Document.Revision), cancellationToken);
        return write.Status == SettingsWriteStatus.Success;
    }

    private static AppearanceSettings Parse(string content)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;

            string accent = root.TryGetProperty("accent", out JsonElement accentElement)
                && accentElement.ValueKind == JsonValueKind.String
                && accentElement.GetString() is { Length: > 0 } accentValue
                ? accentValue
                : AppearanceSettings.Default.Accent;

            bool animationsEnabled = !root.TryGetProperty("animationsEnabled", out JsonElement animationsElement)
                || animationsElement.ValueKind != JsonValueKind.False;

            return new AppearanceSettings(accent, animationsEnabled);
        }
        catch (JsonException)
        {
            return AppearanceSettings.Default;
        }
    }
}
