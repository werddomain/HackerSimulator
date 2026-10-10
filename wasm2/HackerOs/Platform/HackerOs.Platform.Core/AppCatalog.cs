using System.Collections.Frozen;
using System.Collections.ObjectModel;
using HackerOs.App.Abstractions;

namespace HackerOs.Platform.Core;

/// <summary>
/// Contains validated app manifests in deterministic dependency order.
/// </summary>
public sealed class AppCatalog
{
    private AppCatalog(
        IReadOnlyDictionary<string, AppManifest> manifests,
        IReadOnlyList<string> activationOrder,
        IReadOnlySet<string> systemAppIds)
    {
        Manifests = manifests;
        ActivationOrder = activationOrder;
        DeactivationOrder = activationOrder.Reverse().ToArray();
        SystemAppIds = systemAppIds;
    }

    /// <summary>Gets manifests keyed by immutable app ID.</summary>
    public IReadOnlyDictionary<string, AppManifest> Manifests { get; }

    /// <summary>Gets app IDs with dependencies ordered before dependents.</summary>
    public IReadOnlyList<string> ActivationOrder { get; }

    /// <summary>Gets app IDs with dependents ordered before dependencies.</summary>
    public IReadOnlyList<string> DeactivationOrder { get; }

    /// <summary>
    /// Gets the IDs of the apps the host declared system apps (ADR 0048 §1). A system app cannot be
    /// uninstalled and receives its dangerous capabilities without approval (ADR 0049).
    /// </summary>
    /// <remarks>
    /// The set only comes from trusted host input: the <c>isSystemApp</c> flags of the host's
    /// <c>BuiltInApplications.json</c>, written by the build from the <c>IsSystemApp</c> metadata of
    /// its <c>ProjectReference</c>. Neither <see cref="AppManifest"/> nor <c>AppDefinition</c> can
    /// carry the flag, so an app can never declare itself a system app.
    /// </remarks>
    public IReadOnlySet<string> SystemAppIds { get; }

    /// <summary>Gets whether <paramref name="appId"/> is a system app of this catalog.</summary>
    /// <param name="appId">Application identifier (ordinal comparison).</param>
    /// <returns><see langword="true"/> when the host declared the app a system app.</returns>
    public bool IsSystemApp(string appId)
    {
        ArgumentNullException.ThrowIfNull(appId);
        return SystemAppIds.Contains(appId);
    }

    /// <summary>Builds a catalog without loading or executing app assemblies.</summary>
    /// <param name="manifests">Candidate manifests selected by the build or package policy.</param>
    /// <returns>A catalog with no system app, or every deterministic validation error.</returns>
    public static AppCatalogBuildResult Build(IEnumerable<AppManifest> manifests) => Build(manifests, []);

    /// <summary>
    /// Builds a host catalog from its aggregated <c>BuiltInApplications.json</c> entries: the
    /// manifests, plus the system-app flags the host wrote for them (ADR 0048 §1).
    /// </summary>
    /// <param name="applications">Built-in applications read from the host resource.</param>
    /// <returns>A catalog or every deterministic validation error.</returns>
    public static AppCatalogBuildResult BuildFromBuiltInApplications(IEnumerable<BuiltInApplication> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);
        BuiltInApplication[] items = applications.ToArray();
        return Build(
            items.Select(item => item.Manifest),
            items.Where(item => item.IsSystemApp).Select(item => item.Manifest.Id));
    }

    /// <summary>Builds a catalog without loading or executing app assemblies.</summary>
    /// <param name="manifests">Candidate manifests selected by the build or package policy.</param>
    /// <param name="systemAppIds">
    /// IDs the trusted host declared system apps. Each must name a manifest of the catalog
    /// (<c>catalog.system-app.unknown</c> otherwise), so a typo cannot silently leave an app
    /// uninstallable or unprotected.
    /// </param>
    /// <returns>A catalog or every deterministic validation error.</returns>
    public static AppCatalogBuildResult Build(IEnumerable<AppManifest> manifests, IEnumerable<string> systemAppIds)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        ArgumentNullException.ThrowIfNull(systemAppIds);

        AppManifest[] candidates = manifests.ToArray();
        List<AppCatalogError> errors = [];
        Dictionary<string, AppManifest> byId = new(StringComparer.Ordinal);

        foreach (AppManifest manifest in candidates.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            ManifestValidationResult validation = AppManifestValidator.Validate(manifest);
            foreach (ManifestValidationError error in validation.Errors)
            {
                errors.Add(new AppCatalogError(
                    "catalog.manifest.invalid",
                    manifest.Id,
                    $"{error.Path}: {error.Message}"));
            }

            if (!byId.TryAdd(manifest.Id, manifest))
            {
                errors.Add(new AppCatalogError(
                    "catalog.app-id.duplicate",
                    manifest.Id,
                    $"App ID '{manifest.Id}' is registered more than once."));
            }
        }

        HashSet<string> systemIds = new(StringComparer.Ordinal);
        foreach (string systemAppId in systemAppIds.Order(StringComparer.Ordinal))
        {
            if (systemIds.Add(systemAppId) && !byId.ContainsKey(systemAppId))
            {
                errors.Add(new AppCatalogError(
                    "catalog.system-app.unknown",
                    systemAppId,
                    $"System app '{systemAppId}' is not part of the catalog."));
            }
        }

        if (errors.Count > 0)
        {
            return new AppCatalogBuildResult(null, errors);
        }

        ValidateDependencies(byId, errors);
        if (errors.Count > 0)
        {
            return new AppCatalogBuildResult(null, errors);
        }

        IReadOnlyList<string>? activationOrder = CreateActivationOrder(byId, errors);
        if (activationOrder is null)
        {
            return new AppCatalogBuildResult(null, errors);
        }

        ReadOnlyDictionary<string, AppManifest> readOnlyManifests =
            new(new Dictionary<string, AppManifest>(byId, StringComparer.Ordinal));
        return new AppCatalogBuildResult(
            new AppCatalog(readOnlyManifests, activationOrder, systemIds.ToFrozenSet(StringComparer.Ordinal)),
            []);
    }

    private static void ValidateDependencies(
        IReadOnlyDictionary<string, AppManifest> manifests,
        List<AppCatalogError> errors)
    {
        foreach (AppManifest manifest in manifests.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            foreach (AppDependencyManifest dependency in manifest.Dependencies.OrderBy(item => item.AppId, StringComparer.Ordinal))
            {
                if (!manifests.TryGetValue(dependency.AppId, out AppManifest? installed))
                {
                    if (!dependency.Optional)
                    {
                        errors.Add(new AppCatalogError(
                            "catalog.dependency.missing",
                            manifest.Id,
                            $"Required dependency '{dependency.AppId}' is missing."));
                    }

                    continue;
                }

                SemanticVersion installedVersion = SemanticVersion.Parse(installed.Version);
                SemanticVersion minimumVersion = SemanticVersion.Parse(dependency.MinimumVersion);
                SemanticVersion? maximumVersion = dependency.MaximumVersion is null
                    ? null
                    : SemanticVersion.Parse(dependency.MaximumVersion);

                if (installedVersion.CompareTo(minimumVersion) < 0
                    || maximumVersion is SemanticVersion maximum
                    && installedVersion.CompareTo(maximum) > 0)
                {
                    errors.Add(new AppCatalogError(
                        "catalog.dependency.incompatible",
                        manifest.Id,
                        $"Dependency '{dependency.AppId}' version '{installed.Version}' is outside the supported range."));
                }
            }
        }
    }

    private static IReadOnlyList<string>? CreateActivationOrder(
        IReadOnlyDictionary<string, AppManifest> manifests,
        List<AppCatalogError> errors)
    {
        Dictionary<string, int> dependencyCounts = manifests.Keys.ToDictionary(
            appId => appId,
            _ => 0,
            StringComparer.Ordinal);
        Dictionary<string, List<string>> dependents = manifests.Keys.ToDictionary(
            appId => appId,
            _ => new List<string>(),
            StringComparer.Ordinal);

        foreach (AppManifest manifest in manifests.Values)
        {
            foreach (AppDependencyManifest dependency in manifest.Dependencies)
            {
                if (!manifests.ContainsKey(dependency.AppId))
                {
                    continue;
                }

                dependencyCounts[manifest.Id]++;
                dependents[dependency.AppId].Add(manifest.Id);
            }
        }

        SortedSet<string> ready = new(
            dependencyCounts.Where(item => item.Value == 0).Select(item => item.Key),
            StringComparer.Ordinal);
        List<string> order = new(manifests.Count);

        while (ready.Count > 0)
        {
            string appId = ready.Min!;
            ready.Remove(appId);
            order.Add(appId);

            foreach (string dependent in dependents[appId].OrderBy(value => value, StringComparer.Ordinal))
            {
                dependencyCounts[dependent]--;
                if (dependencyCounts[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        if (order.Count == manifests.Count)
        {
            return order;
        }

        string[] cycleMembers = dependencyCounts
            .Where(item => item.Value > 0)
            .Select(item => item.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        errors.Add(new AppCatalogError(
            "catalog.dependency.cycle",
            string.Join(',', cycleMembers),
            $"Dependency cycle detected among: {string.Join(", ", cycleMembers)}."));
        return null;
    }
}

/// <summary>
/// Describes a catalog construction failure.
/// </summary>
/// <param name="Code">Stable machine-readable error code.</param>
/// <param name="AppId">App associated with the error, when available.</param>
/// <param name="Message">Human-readable diagnostic.</param>
public sealed record AppCatalogError(string Code, string AppId, string Message);

/// <summary>
/// Contains either a complete immutable catalog or deterministic build errors.
/// </summary>
/// <param name="Catalog">Constructed catalog when successful.</param>
/// <param name="Errors">Catalog errors in deterministic order.</param>
public sealed record AppCatalogBuildResult(
    AppCatalog? Catalog,
    IReadOnlyList<AppCatalogError> Errors)
{
    /// <summary>Gets whether catalog construction succeeded.</summary>
    public bool IsSuccess => Catalog is not null && Errors.Count == 0;
}