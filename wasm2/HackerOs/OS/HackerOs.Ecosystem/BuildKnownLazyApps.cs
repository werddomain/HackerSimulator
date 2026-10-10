using System.Reflection;
using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;

namespace HackerOs.Ecosystem;

/// <summary>Provides the host-owned manifest catalog for optional build-known apps.</summary>
/// <remarks>
/// The catalog comes from the <c>HackerOs.BuiltInApplications.json</c> resource the build
/// aggregates from every referenced app's generated <c>manifest.json</c> (ADR 0047 §3, §5). Only
/// JSON is read: app assemblies stay lazy and their <c>AppDefinition</c> classes are never
/// instantiated at runtime.
/// TRANSITION (until <c>rebuild/2-app-migration</c>): apps not migrated yet still embed their
/// hand-written <c>app.manifest.json</c> as <c>HackerOs.Ecosystem.Manifests.*</c> resources, which
/// are merged in; they are never system apps.
/// </remarks>
public static class BuildKnownLazyApps
{
    private const string LegacyManifestResourcePrefix = "HackerOs.Ecosystem.Manifests.";

    private static readonly IReadOnlyList<BuiltInApplication> Applications = ReadApplications();

    /// <summary>Gets the validated immutable catalog used for lazy descriptor discovery.</summary>
    public static AppCatalog Catalog { get; } = CreateCatalog();

    /// <summary>
    /// Gets the ids of the apps the host declared system apps (<c>IsSystemApp</c> on their
    /// <c>ProjectReference</c>, ADR 0048). Policy for them lands with <c>rebuild/2-system-apps</c>.
    /// </summary>
    public static IReadOnlySet<string> SystemAppIds { get; } = Applications
        .Where(application => application.IsSystemApp)
        .Select(application => application.Manifest.Id)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>Gets whether <paramref name="appId"/> is a built-in system app.</summary>
    /// <param name="appId">Application identifier.</param>
    /// <returns><see langword="true"/> when the host declared the app a system app.</returns>
    public static bool IsSystemApp(string appId) => SystemAppIds.Contains(appId);

    private static AppCatalog CreateCatalog()
    {
        if (Applications.Count == 0)
        {
            throw new InvalidOperationException("The build contains no embedded first-party app manifests.");
        }

        AppCatalogBuildResult result = AppCatalog.Build(Applications.Select(application => application.Manifest));
        return result.Catalog ?? throw new InvalidOperationException(
            $"The build-known lazy catalog is invalid: {string.Join("; ", result.Errors.Select(error => error.Message))}");
    }

    private static List<BuiltInApplication> ReadApplications()
    {
        Assembly hostAssembly = typeof(BuildKnownLazyApps).Assembly;
        List<BuiltInApplication> applications = [];

        using (Stream? builtIn = hostAssembly.GetManifestResourceStream(BuiltInApplicationsJsonSerializer.ResourceName))
        {
            if (builtIn is not null)
            {
                using StreamReader reader = new(builtIn);
                applications.AddRange(BuiltInApplicationsJsonSerializer.DeserializeStrict(reader.ReadToEnd()));
            }
        }

        applications.AddRange(hostAssembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(LegacyManifestResourcePrefix, StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new BuiltInApplication(IsSystemApp: false, ReadLegacyManifest(hostAssembly, name))));
        return applications;
    }

    private static AppManifest ReadLegacyManifest(Assembly hostAssembly, string resourceName)
    {
        using Stream stream = hostAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded app manifest '{resourceName}' could not be opened.");
        using StreamReader reader = new(stream);
        return AppManifestJsonSerializer.NormalizeOptionalCollections(AppManifestJsonSerializer.DeserializeStrict(reader.ReadToEnd()));
    }
}
