using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Settings;

namespace HackerOs.Platform.Core.Settings;

/// <summary>
/// Validates a `.config` settings document's structure and values against a declared schema.
/// </summary>
public sealed class SchemaConfigSettingsDocumentValidator(SettingsSchema schema) : ISettingsDocumentValidator
{
    private readonly SettingsSchema _schema = schema ?? throw new ArgumentNullException(nameof(schema));

    /// <inheritdoc />
    public IReadOnlyList<string> Validate(string content)
    {
        ConfigDocumentParseResult parsed = ConfigDocumentFormat.Parse(content);
        if (!parsed.Success)
        {
            return parsed.Errors
                .Select(static error => $"{error.Code}:line-{error.LineNumber}")
                .ToArray();
        }

        return _schema.Validate(parsed.Values);
    }
}
