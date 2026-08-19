using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace HackerOs.App.Abstractions;

/// <summary>
/// Provides strict, source-generated JSON serialization for canonical manifest fixtures and runtime manifests.
/// </summary>
public static class AppManifestJsonSerializer
{
    public static string SerializeCanonical(AppManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        AppManifest canonical = CreateCanonicalManifest(manifest);
        return JsonSerializer.Serialize(canonical, AppManifestJsonSerializerContext.Default.AppManifest) + "\n";
    }

    public static AppManifest DeserializeStrict(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using JsonDocument document = JsonDocument.Parse(json);
        ValidateObject(document.RootElement, "manifest", new[]
        {
            "schemaVersion",
            "id",
            "name",
            "version",
            "publisherId",
            "description",
            "kind",
            "entryPoint",
            "platform",
            "sdkCompatibility",
            "osCompatibility",
            "presentation",
            "localizations",
            "capabilities",
            "declaredTopicPermissions",
            "resources",
            "settings",
            "intents",
            "dependencies",
            "assets",
            "update",
            "fileHandlers",
            "terminal",
            "singleInstancePerUser",
            "autoStart",
            "supportsBack"
        });

        return JsonSerializer.Deserialize(document.RootElement.GetRawText(), AppManifestJsonSerializerContext.Default.AppManifest)
            ?? throw new JsonException("The provided manifest payload deserialized to null.");
    }

    private static AppManifest CreateCanonicalManifest(AppManifest manifest)
    {
        PresentationManifest presentation = manifest.Presentation with
        {
            IconAssetPaths = [.. manifest.Presentation.IconAssetPaths.OrderBy(path => path, StringComparer.Ordinal)]
        };

        AppSettingsSchemaManifest? settings = manifest.Settings is null
            ? null
            : new AppSettingsSchemaManifest(
                manifest.Settings.SchemaVersion,
                [.. manifest.Settings.Fields
                    .OrderBy(field => field.Group ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(field => field.Key, StringComparer.Ordinal)
                    .Select(field => field with
                    {
                        AllowedValues = field.AllowedValues is null
                            ? null
                            : [.. field.AllowedValues.OrderBy(value => value, StringComparer.Ordinal)]
                    })],
                manifest.Settings.MigrationIds is null
                    ? null
                    : [.. manifest.Settings.MigrationIds.OrderBy(value => value, StringComparer.Ordinal)]);

        AppManifestPlatform? platform = manifest.Platform is null
            ? null
            : new AppManifestPlatform(
                [.. manifest.Platform.Supported.OrderBy(value => value, StringComparer.Ordinal)],
                [.. manifest.Platform.EntryPoints
                    .Select(entryPoint => entryPoint with
                    {
                        Platforms = [.. entryPoint.Platforms.OrderBy(value => value, StringComparer.Ordinal)]
                    })
                    .OrderBy(entryPoint => string.Join("|", entryPoint.Platforms), StringComparer.Ordinal)]);

        return manifest with
        {
            Presentation = presentation,
            Platform = platform,
            Localizations = [.. manifest.Localizations.OrderBy(item => item.Culture, StringComparer.Ordinal)],
            Capabilities = [.. manifest.Capabilities.OrderBy(value => value, StringComparer.Ordinal)],
            Dependencies = [.. manifest.Dependencies.OrderBy(item => item.AppId, StringComparer.Ordinal)
                .ThenBy(item => item.MinimumVersion, StringComparer.Ordinal)
                .ThenBy(item => item.MaximumVersion ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.Optional)],
            Assets = [.. manifest.Assets.OrderBy(item => item.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.Role ?? string.Empty, StringComparer.Ordinal)],
            Intents = [.. manifest.Intents.OrderBy(item => item.IntentId, StringComparer.Ordinal)],
            Settings = settings,
            FileHandlers = [.. manifest.FileHandlers.OrderBy(item => item.MediaType ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.Extensions.Count)
                .ThenBy(item => string.Join("|", item.Extensions), StringComparer.Ordinal)]
        };
    }

    private static void ValidateObject(JsonElement element, string path, IReadOnlyCollection<string> allowedProperties)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw new JsonException($"Duplicate property '{property.Name}' at '{path}'.");
            }

            if (!allowedProperties.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new JsonException($"Unknown property '{property.Name}' at '{path}'.");
            }

            switch (property.Name)
            {
                case "entryPoint":
                    ValidateObject(property.Value, $"{path}.entryPoint", ["assembly", "type"]);
                    break;
                case "platform":
                    ValidateObject(property.Value, $"{path}.platform", ["supported", "entryPoints"]);
                    break;
                case "entryPoints":
                    ValidateArray(property.Value, $"{path}.entryPoints", ["platforms", "assembly", "type"]);
                    break;
                case "sdkCompatibility":
                case "osCompatibility":
                    ValidateObject(property.Value, $"{path}.{property.Name}", ["minimumVersion", "maximumVersion"]);
                    break;
                case "presentation":
                    ValidateObject(property.Value, $"{path}.presentation", ["category", "launchVisibility", "iconAssetPaths"]);
                    break;
                case "localizations":
                    ValidateArray(property.Value, $"{path}.localizations", ["culture", "resourcePath"]);
                    break;
                case "declaredTopicPermissions":
                    ValidateArray(property.Value, $"{path}.declaredTopicPermissions", ["id", "description"]);
                    break;
                case "resources":
                    ValidateObject(property.Value, $"{path}.resources", ["baselineCpuWeight", "burstCpuWeight", "baselineMemoryWeight", "burstMemoryWeight", "baselineStorageIoWeight", "burstStorageIoWeight", "baselineNetworkIoWeight", "burstNetworkIoWeight"]);
                    break;
                case "settings":
                    ValidateObject(property.Value, $"{path}.settings", ["schemaVersion", "migrationIds", "fields"]);
                    break;
                case "intents":
                    ValidateArray(property.Value, $"{path}.intents", ["intentId", "payloadSchemaAssetPath"]);
                    break;
                case "dependencies":
                    ValidateArray(property.Value, $"{path}.dependencies", ["appId", "minimumVersion", "maximumVersion", "optional"]);
                    break;
                case "assets":
                    ValidateArray(property.Value, $"{path}.assets", ["path", "kind", "sha256", "role"]);
                    break;
                case "update":
                    ValidateObject(property.Value, $"{path}.update", ["migrationIds", "minimumUpgradableVersion"]);
                    break;
                case "fileHandlers":
                    ValidateArray(property.Value, $"{path}.fileHandlers", ["mediaType", "extensions", "actions", "priority"]);
                    break;
                case "terminal":
                    ValidateObject(property.Value, $"{path}.terminal", ["name", "aliases", "usage"]);
                    break;
                default:
                    break;
            }
        }
    }

    private static void ValidateArray(JsonElement element, string path, IReadOnlyCollection<string> allowedProperties)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            ValidateObject(item, $"{path}[{index}]", allowedProperties);
            index++;
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    UseStringEnumConverter = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(AppManifest))]
[JsonSerializable(typeof(AppEntryPointManifest))]
[JsonSerializable(typeof(AppManifestPlatform))]
[JsonSerializable(typeof(AppPlatformEntryPointManifest))]
[JsonSerializable(typeof(AppSdkCompatibilityManifest))]
[JsonSerializable(typeof(PresentationManifest))]
[JsonSerializable(typeof(LocalizationManifest))]
[JsonSerializable(typeof(AppResourceProfileManifest))]
[JsonSerializable(typeof(AppSettingsSchemaManifest))]
[JsonSerializable(typeof(AppSettingFieldManifest))]
[JsonSerializable(typeof(AppIntentDeclarationManifest))]
[JsonSerializable(typeof(AppDependencyManifest))]
[JsonSerializable(typeof(AssetManifest))]
[JsonSerializable(typeof(UpdateManifest))]
[JsonSerializable(typeof(FileHandlerManifest))]
[JsonSerializable(typeof(TerminalCommandManifest))]
[JsonSerializable(typeof(AppKind))]
[JsonSerializable(typeof(AppLaunchVisibility))]
[JsonSerializable(typeof(AppSettingValueType))]
[JsonSerializable(typeof(AppSettingScope))]
[JsonSerializable(typeof(AppSettingSensitivity))]
public sealed partial class AppManifestJsonSerializerContext : JsonSerializerContext
{
}
