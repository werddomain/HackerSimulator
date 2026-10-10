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
/// System apps (ADR 0048 §1) are exposed by <see cref="AppCatalog.SystemAppIds"/> and
/// <see cref="AppCatalog.IsSystemApp"/> on <see cref="Catalog"/>.
/// TRANSITION (until <c>rebuild/2-app-migration</c>): apps not migrated yet still embed their
/// hand-written <c>app.manifest.json</c> as <c>HackerOs.Ecosystem.Manifests.*</c> resources, which
/// are merged in. The build renames the resource of a legacy app whose <c>ProjectReference</c>
/// carries <c>IsSystemApp="true"</c> to <c>HackerOs.Ecosystem.Manifests.System.*</c>
/// (<c>_HackerOsMarkLegacySystemManifests</c> in <c>build/HackerOs.Apps.targets</c>), so the
/// <c>ProjectReference</c> stays the only source of the system flag for them too.
/// </remarks>
public static class BuildKnownLazyApps
{
    private const string LegacyManifestResourcePrefix = "HackerOs.Ecosystem.Manifests.";
    private const string LegacySystemManifestResourcePrefix = "HackerOs.Ecosystem.Manifests.System.";

    private static readonly IReadOnlyList<BuiltInApplication> Applications = ReadApplications();

    /// <summary>Gets the validated immutable catalog used for lazy descriptor discovery.</summary>
    public static AppCatalog Catalog { get; } = CreateCatalog();

    private static AppCatalog CreateCatalog()
    {
        if (Applications.Count == 0)
        {
            throw new InvalidOperationException("The build contains no embedded first-party app manifests.");
        }

        AppCatalogBuildResult result = AppCatalog.BuildFromBuiltInApplications(Applications);
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
            .Select(name => new BuiltInApplication(
                IsSystemApp: name.StartsWith(LegacySystemManifestResourcePrefix, StringComparison.Ordinal),
                ReadLegacyManifest(hostAssembly, name))));
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
