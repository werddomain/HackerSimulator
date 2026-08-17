using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HackerOs.App.Abstractions;

namespace HackerOs.Platform.Core.Discovery;

/// <summary>
/// Resolves each catalog manifest's declared entry point to a concrete, correctly based type
/// using only an explicit host-provided assembly list, per `P1-APP-002` and `P1-APP-003`.
/// </summary>
/// <remarks>
/// Platform Core intentionally has no project reference to the Blazor App SDK (window rendering
/// is out of scope for Phase 1), so a <see cref="AppKind.Window"/> entry point cannot be verified
/// with a direct <c>typeof(WindowAppBase).IsAssignableFrom(...)</c> check. Every app kind is
/// therefore verified the same way: by walking the candidate type's base-type chain and comparing
/// each ancestor's full name against the expected SDK base type name for that <see cref="AppKind"/>.
/// This never instantiates app code and never loads an assembly beyond what the host supplied.
/// </remarks>
public static class AppEntryPointDiscovery
{
    private static readonly IReadOnlyDictionary<AppKind, string> ExpectedBaseTypeFullNames = new Dictionary<AppKind, string>
    {
        [AppKind.Window] = "HackerOs.AppSdk.Blazor.WindowAppBase",
        [AppKind.Terminal] = "HackerOs.AppSdk.TerminalAppBase",
        [AppKind.Service] = "HackerOs.AppSdk.ServiceAppBase"
    };

    /// <summary>
    /// Resolves every manifest in <paramref name="catalog"/> to a validated <see cref="AppDescriptor"/>.
    /// </summary>
    /// <param name="catalog">Catalog whose manifests already passed dependency/version validation.</param>
    /// <param name="hostAssemblies">
    /// The explicit, host-provided set of assemblies apps may be loaded from, keyed by simple
    /// assembly name. Discovery never scans <see cref="AppDomain"/> or loads assemblies itself.
    /// </param>
    /// <returns>Every resolved descriptor, or every deterministic resolution error.</returns>
    /// <remarks>
    /// Resolves manifest-declared type names by name (<see cref="Assembly.GetType(string, bool)"/>),
    /// which is fundamentally reflection-based and cannot be statically analyzed for trimming. This
    /// is a bounded, explicit reflection boundary: only assemblies the host itself passes in
    /// <paramref name="hostAssemblies"/> are consulted, matching the build profile's discovery list
    /// (`P1-BLD-006`). A future host publish step (Phase 2/6) must add matching trim root
    /// descriptors for every build-profile-selected entry-point type; see the Problem Register entry
    /// linked from `P1-GATE-004` in `integration-task-list.md` until that exists.
    /// </remarks>
    [RequiresUnreferencedCode(
        "Resolves manifest entry-point types by name from an explicit host assembly list; the " +
        "eventual host publish step must add matching trim root descriptors (see P1-GATE-004).")]
    public static AppDiscoveryResult Discover(
        AppCatalog catalog,
        IReadOnlyDictionary<string, Assembly> hostAssemblies)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(hostAssemblies);

        List<AppDiscoveryError> errors = [];
        Dictionary<string, AppDescriptor> descriptors = new(StringComparer.Ordinal);

        foreach (AppManifest manifest in catalog.Manifests.Values.OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            if (!hostAssemblies.TryGetValue(manifest.EntryPoint.Assembly, out Assembly? assembly))
            {
                errors.Add(new AppDiscoveryError(
                    "discovery.assembly.not-allowed",
                    manifest.Id,
                    $"Assembly '{manifest.EntryPoint.Assembly}' is not in the host's explicit assembly list."));
                continue;
            }

            Type? entryType = assembly.GetType(manifest.EntryPoint.Type, throwOnError: false);
            if (entryType is null)
            {
                errors.Add(new AppDiscoveryError(
                    "discovery.type.not-found",
                    manifest.Id,
                    $"Type '{manifest.EntryPoint.Type}' was not found in assembly '{manifest.EntryPoint.Assembly}'."));
                continue;
            }

            if (!entryType.IsClass || entryType.IsAbstract)
            {
                errors.Add(new AppDiscoveryError(
                    "discovery.type.not-concrete",
                    manifest.Id,
                    $"Type '{entryType.FullName}' must be a concrete, non-abstract class."));
                continue;
            }

            string expectedBase = ExpectedBaseTypeFullNames[manifest.Kind];
            if (!DerivesFrom(entryType, expectedBase))
            {
                errors.Add(new AppDiscoveryError(
                    "discovery.type.wrong-base",
                    manifest.Id,
                    $"Type '{entryType.FullName}' must derive from '{expectedBase}' for app kind '{manifest.Kind}'."));
                continue;
            }

            descriptors.Add(manifest.Id, new AppDescriptor(manifest, entryType, assembly));
        }

        return errors.Count > 0
            ? new AppDiscoveryResult(null, errors)
            : new AppDiscoveryResult(descriptors, []);
    }

    /// <summary>Walks a type's base-type chain, comparing full names without loading the ancestor assembly by reference.</summary>
    private static bool DerivesFrom(Type candidate, string expectedBaseFullName)
    {
        for (Type? current = candidate.BaseType; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.FullName, expectedBaseFullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
