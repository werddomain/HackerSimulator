using System.Text;
using System.Text.Json;

namespace HackerOs.App.Abstractions;

/// <summary>
/// One application shipped with a host build, as listed in <c>BuiltInApplications.json</c>
/// (ADR 0047 §3).
/// </summary>
/// <param name="IsSystemApp">
/// Whether the host declared the app a system app (<c>IsSystemApp</c> metadata on its
/// <c>ProjectReference</c>, ADR 0048). An app can never set this itself.
/// </param>
/// <param name="Manifest">The app's schema-version-1 manifest.</param>
public sealed record BuiltInApplication(bool IsSystemApp, AppManifest Manifest);

/// <summary>
/// Strict JSON format of <c>BuiltInApplications.json</c>, the catalog a host build aggregates from
/// the generated manifests of every referenced app and embeds as the
/// <c>HackerOs.BuiltInApplications.json</c> resource:
/// <c>{"schemaVersion":1,"applications":[{"isSystemApp":true,"manifest":{...}}]}</c>.
/// Same conventions as <see cref="AppManifestJsonSerializer"/>: camelCase, unknown and duplicate
/// properties rejected, canonical output (applications sorted by id, canonical manifests).
/// </summary>
public static class BuiltInApplicationsJsonSerializer
{
    /// <summary>Logical name of the embedded resource holding the document in a host assembly.</summary>
    public const string ResourceName = "HackerOs.BuiltInApplications.json";

    private const int CurrentSchemaVersion = 1;

    /// <summary>Writes the canonical document, terminated by a newline.</summary>
    /// <param name="applications">Applications to list; order is irrelevant.</param>
    /// <returns>Canonical JSON text.</returns>
    public static string SerializeCanonical(IEnumerable<BuiltInApplication> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);

        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
            writer.WriteStartArray("applications");
            foreach (BuiltInApplication application in applications.OrderBy(item => item.Manifest.Id, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteBoolean("isSystemApp", application.IsSystemApp);
                writer.WritePropertyName("manifest");
                writer.WriteRawValue(AppManifestJsonSerializer.SerializeCanonical(application.Manifest).TrimEnd('\n'));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    /// <summary>
    /// Reads a document strictly. Each manifest goes through
    /// <see cref="AppManifestJsonSerializer.DeserializeStrict"/>; omitted optional collections are
    /// normalized to empty lists. Manifests are not semantically validated here: callers build an
    /// <c>AppCatalog</c>, which validates them.
    /// </summary>
    /// <param name="json">Document text.</param>
    /// <returns>The applications in document order.</returns>
    /// <exception cref="JsonException">The document is malformed, has an unknown/duplicate property or an unsupported version.</exception>
    public static IReadOnlyList<BuiltInApplication> DeserializeStrict(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        RequireObject(root, "document", ["schemaVersion", "applications"]);

        if (!root.TryGetProperty("schemaVersion", out JsonElement version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out int schemaVersion)
            || schemaVersion != CurrentSchemaVersion)
        {
            throw new JsonException("BuiltInApplications.json must declare schemaVersion 1.");
        }

        if (!root.TryGetProperty("applications", out JsonElement applications) || applications.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("BuiltInApplications.json must contain an 'applications' array.");
        }

        List<BuiltInApplication> result = [];
        int index = 0;
        foreach (JsonElement entry in applications.EnumerateArray())
        {
            string path = $"applications[{index++}]";
            RequireObject(entry, path, ["isSystemApp", "manifest"]);
            if (!entry.TryGetProperty("isSystemApp", out JsonElement isSystem)
                || isSystem.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new JsonException($"'{path}.isSystemApp' must be a boolean.");
            }

            if (!entry.TryGetProperty("manifest", out JsonElement manifest) || manifest.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"'{path}.manifest' must be an object.");
            }

            result.Add(new BuiltInApplication(
                isSystem.GetBoolean(),
                AppManifestJsonSerializer.NormalizeOptionalCollections(AppManifestJsonSerializer.DeserializeStrict(manifest.GetRawText()))));
        }

        return result;
    }

    private static void RequireObject(JsonElement element, string path, IReadOnlyCollection<string> allowed)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"'{path}' must be an object.");
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw new JsonException($"Duplicate property '{property.Name}' at '{path}'.");
            }

            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new JsonException($"Unknown property '{property.Name}' at '{path}'.");
            }
        }
    }
}
