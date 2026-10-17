using System.Reflection;
using HackerOs.App.Abstractions;
using HackerOs.Platform.Blazor.LazyLoading;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Discovery;

namespace HackerOs.Ecosystem;

/// <summary>
/// The apps a host was built with (ADR 0047 §3, ADR 0098 §2): the
/// <c>HackerOs.BuiltInApplications.json</c> resource the build aggregates into the host project's own
/// assembly (<c>HackerOsAppHost</c>) from every app it references, as a project or as a package.
/// </summary>
/// <remarks>
/// Only JSON is read: app assemblies stay lazy and their <c>AppDefinition</c> classes are never
/// instantiated at run time. Like <c>/var/lib/dpkg/status</c>, it is the list of what the image
/// shipped with, decided at build time, never at run time.
/// </remarks>
public sealed class HostAppCatalog
{
    private HostAppCatalog(IReadOnlyList<BuiltInApplication> applications)
    {
        if (applications.Count == 0)
        {
            throw new InvalidOperationException("The build contains no embedded app manifests.");
        }

        AppCatalogBuildResult result = AppCatalog.BuildFromBuiltInApplications(applications);
        Catalog = result.Catalog ?? throw new InvalidOperationException(
            $"The host's app catalog is invalid: {string.Join("; ", result.Errors.Select(error => error.Message))}");
        Images = applications
            .Where(static application => application.Image is not null)
            .ToDictionary(static application => application.Manifest.Id, static application => application.Image!, StringComparer.Ordinal);
        LazyAssemblyNames =
        [.. Catalog.Manifests.Values
            .Select(manifest => AppPlatformEntryPointResolver.Instance.Resolve(manifest, WellKnownAppPlatforms.Desktop))
            .Where(resolution => resolution.IsResolved)
            .Select(resolution => resolution.EntryPoint!.Assembly)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)];
    }

    /// <summary>Gets the validated, immutable catalog (system apps per ADR 0048 included).</summary>
    public AppCatalog Catalog { get; }

    /// <summary>
    /// Gets the executable image of each app, by app id: the assembly sizes the host build measured
    /// (ADR 0068, image layer). An app missing here has no image layer.
    /// </summary>
    public IReadOnlyDictionary<string, AppImage> Images { get; }

    /// <summary>
    /// Gets the exact app assemblies the catalog declares, resolved for the Desktop platform (see
    /// <see cref="BuildKnownLazyAppDescriptorRegistry"/>'s matching default): the only names the
    /// lazy loader will ever fetch.
    /// </summary>
    public IReadOnlyList<string> LazyAssemblyNames { get; }

    /// <summary>Reads the catalog embedded in a host assembly.</summary>
    /// <param name="hostAssembly">The host project's assembly (for example <c>typeof(Program).Assembly</c>).</param>
    /// <returns>The host's apps.</returns>
    /// <exception cref="InvalidOperationException">
    /// The assembly embeds no catalog (its project does not set <c>HackerOsAppHost</c>), or the catalog is invalid.
    /// </exception>
    public static HostAppCatalog Load(Assembly hostAssembly)
    {
        ArgumentNullException.ThrowIfNull(hostAssembly);
        using Stream builtIn = hostAssembly.GetManifestResourceStream(BuiltInApplicationsJsonSerializer.ResourceName)
            ?? throw new InvalidOperationException(
                $"'{hostAssembly.GetName().Name}' does not embed '{BuiltInApplicationsJsonSerializer.ResourceName}': set HackerOsAppHost to true in its project (ADR 0047).");
        using StreamReader reader = new(builtIn);
        return new HostAppCatalog(BuiltInApplicationsJsonSerializer.DeserializeStrict(reader.ReadToEnd()));
    }
}
