using System.Reflection;
using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;

namespace HackerOs.Ecosystem;

/// <summary>Provides the host-owned manifest catalog for optional build-known apps.</summary>
public static class BuildKnownLazyApps
{
    private const string ManifestResourcePrefix = "HackerOs.Ecosystem.Manifests.";

    /// <summary>Gets the validated immutable catalog used for lazy descriptor discovery.</summary>
    public static AppCatalog Catalog { get; } = CreateCatalog();

    private static AppCatalog CreateCatalog()
    {
        Assembly hostAssembly = typeof(BuildKnownLazyApps).Assembly;
        AppManifest[] manifests = hostAssembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ManifestResourcePrefix, StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => ReadManifest(hostAssembly, name))
            .ToArray();
        if (manifests.Length == 0)
        {
            throw new InvalidOperationException("The build contains no embedded first-party app manifests.");
        }

        AppCatalogBuildResult result = AppCatalog.Build(manifests);
        return result.Catalog ?? throw new InvalidOperationException(
            $"The build-known lazy catalog is invalid: {string.Join("; ", result.Errors.Select(error => error.Message))}");
    }

    private static AppManifest ReadManifest(Assembly hostAssembly, string resourceName)
    {
        using Stream stream = hostAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded app manifest '{resourceName}' could not be opened.");
        using StreamReader reader = new(stream);
        AppManifest manifest = AppManifestJsonSerializer.DeserializeStrict(reader.ReadToEnd());
        return manifest with
        {
            Presentation = manifest.Presentation with
            {
                IconAssetPaths = manifest.Presentation.IconAssetPaths ?? []
            },
            Localizations = manifest.Localizations ?? [],
            Capabilities = manifest.Capabilities ?? [],
            Intents = manifest.Intents ?? [],
            Dependencies = manifest.Dependencies ?? [],
            Assets = manifest.Assets ?? [],
            FileHandlers = manifest.FileHandlers ?? [],
            Terminal = manifest.Terminal is null
                ? null
                : manifest.Terminal with { Aliases = manifest.Terminal.Aliases ?? [] }
        };
    }
}
