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
/// </remarks>
public static class BuildKnownLazyApps
{
    private static readonly IReadOnlyList<BuiltInApplication> Applications = ReadApplications();

    /// <summary>Gets the validated immutable catalog used for lazy descriptor discovery.</summary>
    public static AppCatalog Catalog { get; } = CreateCatalog();

    /// <summary>
    /// Gets the executable image of each built-in app, by app id: the assembly sizes the host build
    /// measured (ADR 0068, image layer). An app missing here has no image layer.
    /// </summary>
    public static IReadOnlyDictionary<string, AppImage> Images { get; } = Applications
        .Where(static application => application.Image is not null)
        .ToDictionary(static application => application.Manifest.Id, static application => application.Image!, StringComparer.Ordinal);

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

    private static IReadOnlyList<BuiltInApplication> ReadApplications()
    {
        using Stream builtIn = typeof(BuildKnownLazyApps).Assembly
            .GetManifestResourceStream(BuiltInApplicationsJsonSerializer.ResourceName)
            ?? throw new InvalidOperationException(
                $"The build did not embed '{BuiltInApplicationsJsonSerializer.ResourceName}' (HackerOsAppHost aggregation).");
        using StreamReader reader = new(builtIn);
        return BuiltInApplicationsJsonSerializer.DeserializeStrict(reader.ReadToEnd());
    }
}
