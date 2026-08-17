using System.Text.Json;
using System.Text.Json.Serialization;

namespace HackerOs.App.Abstractions;

/// <summary>
/// Declares the build-time profile that selects apps, assets, defaults, grants, and optional runtime features.
/// </summary>
public sealed record BuildProfileManifest
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<BuildProfilePackageManifest> Packages { get; init; } = [];

    public IReadOnlyList<string> DefaultEnabledAppIds { get; init; } = [];

    public IReadOnlyList<BuildProfileGrantManifest> RequiredGrants { get; init; } = [];

    public IReadOnlyList<BuildProfileAssociationManifest> Associations { get; init; } = [];

    public IReadOnlyList<string> Locales { get; init; } = [];

    public IReadOnlyList<string> Themes { get; init; } = [];

    public IReadOnlyList<string> OptionalServerFeatures { get; init; } = [];
}

/// <summary>Describes one included package or app project entry in a build profile.</summary>
public sealed record BuildProfilePackageManifest(
    string AppId,
    string ProjectReference,
    BuildProfileLoadMode LoadMode,
    bool IsBootCritical = false,
    bool EnabledByDefault = true);

/// <summary>Describes a capability grant that must be seeded for an included package.</summary>
public sealed record BuildProfileGrantManifest(string CapabilityId, string AppId);

/// <summary>Describes a default file association that the profile seeds.</summary>
public sealed record BuildProfileAssociationManifest(
    string Extension,
    string MediaType,
    string Action,
    string AppId);

[JsonConverter(typeof(BuildProfileLoadModeJsonConverter))]
public enum BuildProfileLoadMode
{
    Eager,
    Lazy
}

internal sealed class BuildProfileLoadModeJsonConverter : JsonConverter<BuildProfileLoadMode>
{
    public override BuildProfileLoadMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        return value switch
        {
            "eager" => BuildProfileLoadMode.Eager,
            "lazy" => BuildProfileLoadMode.Lazy,
            _ => throw new JsonException($"Unknown build profile load mode '{value}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, BuildProfileLoadMode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            BuildProfileLoadMode.Eager => "eager",
            BuildProfileLoadMode.Lazy => "lazy",
            _ => throw new JsonException($"Unsupported build profile load mode '{value}'.")
        });
    }
}
