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
        IReadOnlyList<string> activationOrder)
    {
        Manifests = manifests;
        ActivationOrder = activationOrder;
        DeactivationOrder = activationOrder.Reverse().ToArray();
    }

    /// <summary>Gets manifests keyed by immutable app ID.</summary>
    public IReadOnlyDictionary<string, AppManifest> Manifests { get; }

    /// <summary>Gets app IDs with dependencies ordered before dependents.</summary>
    public IReadOnlyList<string> ActivationOrder { get; }

    /// <summary>Gets app IDs with dependents ordered before dependencies.</summary>
    public IReadOnlyList<string> DeactivationOrder { get; }

    /// <summary>Builds a catalog without loading or executing app assemblies.</summary>
    /// <param name="manifests">Candidate manifests selected by the build or package policy.</param>
    /// <returns>A catalog or every deterministic validation error.</returns>
    public static AppCatalogBuildResult Build(IEnumerable<AppManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);

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
            new AppCatalog(readOnlyManifests, activationOrder),
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