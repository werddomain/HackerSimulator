using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions;

namespace HackerOs.Platform.Core.Shell;

/// <summary>Validates the human-editable UI-platform-preference settings document.</summary>
public sealed class UiPlatformPreferenceValidator : ISettingsDocumentValidator
{
    private static readonly HashSet<string> KnownSelectionSources = new(StringComparer.Ordinal) { "auto", "explicit" };

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
                return ["ui-platform-preference.root-object-required"];
            }

            if (!root.TryGetProperty("schemaVersion", out JsonElement schemaVersion)
                || schemaVersion.ValueKind != JsonValueKind.Number
                || !schemaVersion.TryGetInt32(out int version)
                || version != 1)
            {
                errors.Add("ui-platform-preference.schema-version-invalid");
            }

            string? selectionSource = null;
            if (!root.TryGetProperty("selectionSource", out JsonElement selectionSourceElement)
                || selectionSourceElement.ValueKind != JsonValueKind.String
                || selectionSourceElement.GetString() is not string sourceValue
                || !KnownSelectionSources.Contains(sourceValue))
            {
                errors.Add("ui-platform-preference.selection-source-invalid");
            }
            else
            {
                selectionSource = sourceValue;
            }

            if (!root.TryGetProperty("explicitPlatformId", out JsonElement explicitPlatformId))
            {
                errors.Add("ui-platform-preference.explicit-platform-id-missing");
            }
            else if (selectionSource == "explicit")
            {
                if (explicitPlatformId.ValueKind != JsonValueKind.String
                    || !AppPlatformId.TryParse(explicitPlatformId.GetString(), out _))
                {
                    errors.Add("ui-platform-preference.explicit-platform-id-invalid");
                }
            }
            else if (selectionSource == "auto" && explicitPlatformId.ValueKind is not JsonValueKind.Null)
            {
                errors.Add("ui-platform-preference.explicit-platform-id-must-be-null-when-auto");
            }
        }
        catch (JsonException)
        {
            errors.Add("settings.json-invalid");
        }

        return errors;
    }
}
