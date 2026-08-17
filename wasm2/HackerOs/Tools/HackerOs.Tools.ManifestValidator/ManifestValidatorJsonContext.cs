using System.Text.Json.Serialization;
using HackerOs.App.Abstractions;

namespace HackerOs.Tools.ManifestValidator;

/// <summary>Trim-safe source-generated JSON metadata for the manifest CLI.</summary>
[JsonSerializable(typeof(AppManifest))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class ManifestValidatorJsonContext : JsonSerializerContext;
