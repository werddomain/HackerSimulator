using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace HackerOs.App.Abstractions;

/// <summary>
/// Provides strict, source-generated JSON serialization for build-profile fixtures.
/// </summary>
public static class BuildProfileJsonSerializer
{
    public static string SerializeCanonical(BuildProfileManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(CreateCanonicalManifest(manifest), BuildProfileJsonSerializerContext.Default.BuildProfileManifest) + "\n";
    }

    private static BuildProfileManifest CreateCanonicalManifest(BuildProfileManifest manifest)
    {
        return manifest with
        {
            Packages = [.. manifest.Packages.OrderBy(item => item.AppId, StringComparer.Ordinal)],
            DefaultEnabledAppIds = [.. manifest.DefaultEnabledAppIds.OrderBy(value => value, StringComparer.Ordinal)],
            RequiredGrants = [.. manifest.RequiredGrants.OrderBy(item => item.AppId, StringComparer.Ordinal).ThenBy(item => item.CapabilityId, StringComparer.Ordinal)],
            Associations = [.. manifest.Associations.OrderBy(item => item.AppId, StringComparer.Ordinal).ThenBy(item => item.Extension, StringComparer.Ordinal)],
            Locales = [.. manifest.Locales.OrderBy(value => value, StringComparer.Ordinal)],
            Themes = [.. manifest.Themes.OrderBy(value => value, StringComparer.Ordinal)],
            OptionalServerFeatures = [.. manifest.OptionalServerFeatures.OrderBy(value => value, StringComparer.Ordinal)]
        };
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    UseStringEnumConverter = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(BuildProfileManifest))]
[JsonSerializable(typeof(BuildProfilePackageManifest))]
[JsonSerializable(typeof(BuildProfileGrantManifest))]
[JsonSerializable(typeof(BuildProfileAssociationManifest))]
[JsonSerializable(typeof(BuildProfileLoadMode))]
public sealed partial class BuildProfileJsonSerializerContext : JsonSerializerContext
{
}
