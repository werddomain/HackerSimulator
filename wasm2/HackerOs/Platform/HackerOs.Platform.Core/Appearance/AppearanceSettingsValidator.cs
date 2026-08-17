using System.Text.Json;
using HackerOs.Simulation.Abstractions;

namespace HackerOs.Platform.Core.Appearance;

/// <summary>Validates the human-editable desktop-appearance settings document.</summary>
public sealed class AppearanceSettingsValidator : ISettingsDocumentValidator
{
    private static readonly HashSet<string> KnownAccents = new(StringComparer.Ordinal) { "green", "cyan", "purple" };

    /// <inheritdoc />
    public IReadOnlyList<string> Validate(string content)
    {
        List<string> errors = [];

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ["appearance.root-object-required"];
            }

            if (!root.TryGetProperty("schemaVersion", out JsonElement schemaVersion)
                || schemaVersion.ValueKind != JsonValueKind.Number
                || !schemaVersion.TryGetInt32(out int version)
                || version != 1)
            {
                errors.Add("appearance.schema-version-invalid");
            }

            if (!root.TryGetProperty("accent", out JsonElement accent)
                || accent.ValueKind != JsonValueKind.String
                || accent.GetString() is not string accentValue
                || !KnownAccents.Contains(accentValue))
            {
                errors.Add("appearance.accent-invalid");
            }

            if (!root.TryGetProperty("animationsEnabled", out JsonElement animationsEnabled)
                || animationsEnabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add("appearance.animations-enabled-invalid");
            }
        }
        catch (JsonException)
        {
            errors.Add("settings.json-invalid");
        }

        return errors;
    }
}
