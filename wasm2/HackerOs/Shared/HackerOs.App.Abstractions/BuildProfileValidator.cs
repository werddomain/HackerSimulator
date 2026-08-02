namespace HackerOs.App.Abstractions;

/// <summary>
/// Validates build-profile declarations before they are used to assemble a package graph.
/// </summary>
public static class BuildProfileValidator
{
    public static BuildProfileValidationResult Validate(BuildProfileManifest manifest)
        => Validate(manifest, []);

    public static BuildProfileValidationResult Validate(BuildProfileManifest manifest, IReadOnlyList<AppManifest> knownManifests)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        List<BuildProfileValidationError> errors = [];
        HashSet<string> manifestIds = new(knownManifests.Select(item => item.Id), StringComparer.Ordinal);

        ValidatePackages(manifest.Packages, errors);
        ValidateUniqueValues(manifest.DefaultEnabledAppIds, "buildProfile.defaultEnabledAppIds", errors);
        ValidateUniqueValues(manifest.Locales, "buildProfile.locales", errors);
        ValidateUniqueValues(manifest.Themes, "buildProfile.themes", errors);
        ValidateUniqueValues(manifest.OptionalServerFeatures, "buildProfile.optionalServerFeatures", errors);

        ValidateReferences(manifest, manifestIds, errors);

        IReadOnlyList<string> includedAppIds = ComputeIncludedAppIds(manifest, manifestIds);
        ValidateDependencyGraph(manifest, knownManifests, includedAppIds, errors);
        ValidateBootRecovery(manifest, includedAppIds, errors);
        IReadOnlyList<string> publishAssetPaths = ComputePublishAssetPaths(knownManifests, includedAppIds);

        return new BuildProfileValidationResult(errors, includedAppIds, publishAssetPaths);
    }

    /// <summary>
    /// Builds the explicit discovery order for apps that should be published or registered from a profile.
    /// </summary>
    /// <param name="manifest">The build profile whose package and default selections should be assembled.</param>
    /// <param name="knownManifests">The manifest catalog available to the build.</param>
    /// <returns>A deterministic list of app IDs ordered by package declaration, then default-enabled additions.</returns>
    public static IReadOnlyList<string> BuildDiscoveryAppIds(BuildProfileManifest manifest, IReadOnlyList<AppManifest> knownManifests)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(knownManifests);

        HashSet<string> manifestIds = new(knownManifests.Select(item => item.Id), StringComparer.Ordinal);
        HashSet<string> discovered = new(StringComparer.Ordinal);
        List<string> discoveryOrder = [];

        foreach (BuildProfilePackageManifest package in manifest.Packages.OrderBy(item => item.AppId, StringComparer.Ordinal))
        {
            if (!manifestIds.Contains(package.AppId) || !discovered.Add(package.AppId))
            {
                continue;
            }

            discoveryOrder.Add(package.AppId);
        }

        foreach (string appId in manifest.DefaultEnabledAppIds.OrderBy(item => item, StringComparer.Ordinal))
        {
            if (!manifestIds.Contains(appId) || !discovered.Add(appId))
            {
                continue;
            }

            discoveryOrder.Add(appId);
        }

        return discoveryOrder;
    }

    private static void ValidatePackages(IReadOnlyList<BuildProfilePackageManifest> packages, List<BuildProfileValidationError> errors)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (BuildProfilePackageManifest package in packages)
        {
            if (!seen.Add(package.AppId))
            {
                AddError(errors, "buildProfile.package.duplicate", "packages", $"Package '{package.AppId}' is declared more than once.");
            }

            if (!Enum.IsDefined(package.LoadMode))
            {
                AddError(errors, "buildProfile.package.loadMode.invalid", "packages.loadMode", $"Package '{package.AppId}' uses an unrecognized load mode.");
            }
        }
    }

    private static void ValidateUniqueValues(IEnumerable<string> values, string path, List<BuildProfileValidationError> errors)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string value in values)
        {
            if (!seen.Add(value))
            {
                AddError(errors, "buildProfile.value.duplicate", path, $"Duplicate value '{value}'.");
            }
        }
    }

    private static void ValidateReferences(BuildProfileManifest manifest, HashSet<string> manifestIds, List<BuildProfileValidationError> errors)
    {
        foreach (BuildProfilePackageManifest package in manifest.Packages)
        {
            if (!manifestIds.Contains(package.AppId))
            {
                AddError(errors, "buildProfile.reference.unresolved", "buildProfile.packages.appId", $"Package app '{package.AppId}' is not declared in the known manifest catalog.");
            }
        }

        foreach (BuildProfileGrantManifest grant in manifest.RequiredGrants)
        {
            if (!manifestIds.Contains(grant.AppId))
            {
                AddError(errors, "buildProfile.reference.unresolved", "buildProfile.requiredGrants.appId", $"Required grant app '{grant.AppId}' is not declared in the known manifest catalog.");
            }
        }

        foreach (BuildProfileAssociationManifest association in manifest.Associations)
        {
            if (!manifestIds.Contains(association.AppId))
            {
                AddError(errors, "buildProfile.reference.unresolved", "buildProfile.associations.appId", $"Association app '{association.AppId}' is not declared in the known manifest catalog.");
            }
        }

        foreach (string appId in manifest.DefaultEnabledAppIds)
        {
            if (!manifestIds.Contains(appId))
            {
                AddError(errors, "buildProfile.reference.unresolved", "buildProfile.defaultEnabledAppIds", $"Default-enabled app '{appId}' is not declared in the known manifest catalog.");
            }
        }
    }

    private static void ValidateDependencyGraph(
        BuildProfileManifest manifest,
        IReadOnlyList<AppManifest> knownManifests,
        IReadOnlyList<string> includedAppIds,
        List<BuildProfileValidationError> errors)
    {
        HashSet<string> includedAppIdSet = new(includedAppIds, StringComparer.Ordinal);
        Dictionary<string, AppManifest> manifestsById = knownManifests
            .Where(item => includedAppIdSet.Contains(item.Id))
            .ToDictionary(item => item.Id, StringComparer.Ordinal);

        Dictionary<string, int> states = manifestsById.Keys.ToDictionary(
            item => item,
            _ => 0,
            StringComparer.Ordinal);

        foreach (string appId in manifestsById.Keys.OrderBy(item => item, StringComparer.Ordinal))
        {
            DetectCycle(appId, manifestsById, states, errors);
        }
    }

    private static void ValidateBootRecovery(
        BuildProfileManifest manifest,
        IReadOnlyList<string> includedAppIds,
        List<BuildProfileValidationError> errors)
    {
        bool hasBootCriticalPackage = manifest.Packages
            .Where(package => includedAppIds.Contains(package.AppId))
            .Any(package => package.IsBootCritical);

        if (!hasBootCriticalPackage)
        {
            AddError(errors, "buildProfile.bootRecovery.required", "buildProfile.packages", "A boot-critical package is required to support boot recovery.");
        }
    }

    private static bool DetectCycle(
        string appId,
        IReadOnlyDictionary<string, AppManifest> manifestsById,
        Dictionary<string, int> states,
        List<BuildProfileValidationError> errors)
    {
        if (states[appId] == 1)
        {
            AddError(errors, "buildProfile.dependency.cycle", "buildProfile.dependencies", $"Dependency cycle detected involving '{appId}'.");
            return true;
        }

        if (states[appId] == 2)
        {
            return false;
        }

        states[appId] = 1;
        AppManifest? manifestEntry = manifestsById.GetValueOrDefault(appId);

        if (manifestEntry is not null)
        {
            foreach (AppDependencyManifest dependency in manifestEntry.Dependencies
                .Where(item => !item.Optional && manifestsById.ContainsKey(item.AppId))
                .OrderBy(item => item.AppId, StringComparer.Ordinal))
            {
                if (DetectCycle(dependency.AppId, manifestsById, states, errors))
                {
                    return true;
                }
            }
        }

        states[appId] = 2;
        return false;
    }

    private static IReadOnlyList<string> ComputeIncludedAppIds(BuildProfileManifest manifest, HashSet<string> manifestIds)
    {
        HashSet<string> included = new(StringComparer.Ordinal);
        foreach (BuildProfilePackageManifest package in manifest.Packages)
        {
            if (manifestIds.Contains(package.AppId))
            {
                included.Add(package.AppId);
            }
        }

        foreach (string appId in manifest.DefaultEnabledAppIds)
        {
            if (manifestIds.Contains(appId))
            {
                included.Add(appId);
            }
        }

        return [.. included.OrderBy(item => item, StringComparer.Ordinal)];
    }

    private static IReadOnlyList<string> ComputePublishAssetPaths(IReadOnlyList<AppManifest> knownManifests, IReadOnlyList<string> includedAppIds)
    {
        HashSet<string> includedAppIdSet = new(includedAppIds, StringComparer.Ordinal);
        List<string> assetPaths = [];

        foreach (AppManifest manifest in knownManifests)
        {
            if (!includedAppIdSet.Contains(manifest.Id))
            {
                continue;
            }

            foreach (AssetManifest asset in manifest.Assets)
            {
                assetPaths.Add(asset.Path);
            }
        }

        return [.. assetPaths.OrderBy(path => path, StringComparer.Ordinal)];
    }

    private static void AddError(List<BuildProfileValidationError> errors, string code, string path, string message)
        => errors.Add(new BuildProfileValidationError(code, path, message));
}

public sealed record BuildProfileValidationResult(
    IReadOnlyList<BuildProfileValidationError> Errors,
    IReadOnlyList<string> IncludedAppIds,
    IReadOnlyList<string> PublishAssetPaths)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record BuildProfileValidationError(string Code, string Path, string Message);
