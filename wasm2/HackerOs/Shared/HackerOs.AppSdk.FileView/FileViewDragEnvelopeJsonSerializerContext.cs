using System.Text.Json.Serialization;

namespace HackerOs.AppSdk.FileView;

/// <summary>
/// Source-generated (trim-safe) serializer for <see cref="FileViewDragEnvelope"/> — the payload written to
/// and read from <c>DataTransfer</c> by <c>FileView.razor.js</c> (<c>FV-006</c>).
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(FileViewDragEnvelope))]
internal sealed partial class FileViewDragEnvelopeJsonSerializerContext : JsonSerializerContext
{
}
