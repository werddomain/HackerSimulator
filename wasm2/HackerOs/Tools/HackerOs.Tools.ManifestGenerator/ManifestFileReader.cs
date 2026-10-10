using System.Text.Json;
using HackerOs.App.Abstractions;

namespace HackerOs.Tools.ManifestGenerator;

/// <summary>
/// Reads a manifest file back with the strict schema-v1 deserializer (unknown and duplicate
/// properties rejected), normalizes omitted collections and runs <see cref="AppManifestValidator"/>.
/// </summary>
public static class ManifestFileReader
{
    /// <summary>Reads and validates <paramref name="path"/>.</summary>
    /// <param name="path">Manifest file path.</param>
    /// <param name="manifest">The manifest when valid.</param>
    /// <param name="diagnostics">Problems found; empty when valid.</param>
    /// <returns><see langword="true"/> when the file holds a valid manifest.</returns>
    public static bool TryRead(string path, out AppManifest? manifest, out IReadOnlyList<GeneratorDiagnostic> diagnostics)
    {
        manifest = null;
        try
        {
            manifest = Normalize(AppManifestJsonSerializer.DeserializeStrict(File.ReadAllText(path)));
        }
        catch (Exception exception) when (exception is JsonException or IOException or ArgumentException or UnauthorizedAccessException)
        {
            diagnostics = [new GeneratorDiagnostic(GeneratorCodes.UnreadableManifest, path, exception.Message)];
            return false;
        }

        diagnostics = [.. AppManifestValidator.Validate(manifest).Errors
            .Select(error => new GeneratorDiagnostic(GeneratorCodes.InvalidManifest, path, $"{error.Path}: {error.Message} ({error.Code})"))];
        if (diagnostics.Count > 0)
        {
            manifest = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Source-generated deserialization leaves omitted interface-typed collections null; the runtime
    /// model expects empty lists.
    /// </summary>
    /// <param name="manifest">Freshly deserialized manifest.</param>
    /// <returns>The same manifest with every optional collection non-null.</returns>
    public static AppManifest Normalize(AppManifest manifest) => manifest with
    {
        Presentation = manifest.Presentation with { IconAssetPaths = manifest.Presentation.IconAssetPaths ?? [] },
        Localizations = manifest.Localizations ?? [],
        Capabilities = manifest.Capabilities ?? [],
        DeclaredTopicPermissions = manifest.DeclaredTopicPermissions ?? [],
        Intents = manifest.Intents ?? [],
        Dependencies = manifest.Dependencies ?? [],
        Assets = manifest.Assets ?? [],
        FileHandlers = manifest.FileHandlers ?? [],
        Terminal = manifest.Terminal is null ? null : manifest.Terminal with { Aliases = manifest.Terminal.Aliases ?? [] }
    };
}
