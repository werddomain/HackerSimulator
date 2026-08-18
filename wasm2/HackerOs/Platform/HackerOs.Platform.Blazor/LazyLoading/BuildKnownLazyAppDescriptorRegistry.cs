using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Platform.Core.Lifecycle;

namespace HackerOs.Platform.Blazor.LazyLoading;

/// <summary>
/// Resolves descriptors only for app manifests present in the host's immutable,
/// build-known catalog. Requests for the same optional app share one load and one
/// discovery operation for the lifetime of the browser host.
/// </summary>
public sealed class BuildKnownLazyAppDescriptorRegistry : IAppDescriptorLoader
{
    private readonly AppCatalog _catalog;
    private readonly BuildKnownAssemblyLoaderRegistry _assemblies;
    private readonly ConcurrentDictionary<string, AppDescriptor> _descriptors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<AppDescriptorLoadResult>>> _loads = new(StringComparer.Ordinal);

    /// <summary>Initializes descriptor discovery for one validated host catalog.</summary>
    public BuildKnownLazyAppDescriptorRegistry(AppCatalog catalog, BuildKnownAssemblyLoaderRegistry assemblies)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _assemblies = assemblies ?? throw new ArgumentNullException(nameof(assemblies));
    }

    /// <summary>Gets dynamically registered descriptors for lifecycle lookup.</summary>
    public IReadOnlyDictionary<string, AppDescriptor> Descriptors => _descriptors;

    /// <inheritdoc />
    public async ValueTask<AppDescriptorLoadResult> EnsureAvailableAsync(string appId, CancellationToken cancellationToken)
    {
        if (!_catalog.Manifests.ContainsKey(appId))
        {
            return AppDescriptorLoadResult.NotDeclared();
        }

        if (_descriptors.ContainsKey(appId))
        {
            return AppDescriptorLoadResult.Available();
        }

        Task<AppDescriptorLoadResult> sharedLoad = _loads.GetOrAdd(appId, id => new Lazy<Task<AppDescriptorLoadResult>>(
            () => LoadAndDiscoverAsync(id), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        try
        {
            return await sharedLoad.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AppDescriptorLoadResult.Unavailable("The app launch was cancelled while its asset was loading.");
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Discovery is restricted to explicit build-known manifests and assemblies retained by the WebAssembly host.")]
    private async Task<AppDescriptorLoadResult> LoadAndDiscoverAsync(string appId)
    {
        AppManifest manifest = _catalog.Manifests[appId];

        // Lazy loading always targets Desktop today: no shell yet resolves discovery for another
        // platform (that is Phase 2/MOB-008), see AppEntryPointDiscovery.Discover's activePlatform
        // default.
        AppPlatformEntryPointResolution resolution = AppPlatformEntryPointResolver.Instance.Resolve(
            manifest, HackerOs.App.Abstractions.WellKnownAppPlatforms.Desktop);
        if (!resolution.IsResolved || resolution.EntryPoint is not AppEntryPointManifest entryPoint)
        {
            return AppDescriptorLoadResult.Unavailable("The optional app manifest does not support the active platform.");
        }

        BuildKnownAssemblyLoadOutcome assembly = await _assemblies
            .LoadAsync(entryPoint.Assembly, CancellationToken.None)
            .ConfigureAwait(false);
        if (assembly.Status != BuildKnownAssemblyLoadStatus.Loaded || assembly.Assembly is null)
        {
            return AppDescriptorLoadResult.Unavailable(assembly.Detail ?? "The optional app asset could not be loaded.");
        }

        // Isolate discovery to the requested manifest. The host catalog can contain
        // other lazy apps whose assemblies have intentionally not been fetched yet.
        AppCatalogBuildResult appCatalogResult = AppCatalog.Build([manifest]);
        if (appCatalogResult.Catalog is not AppCatalog appCatalog)
        {
            return AppDescriptorLoadResult.Unavailable(
                appCatalogResult.Errors.FirstOrDefault()?.Message ?? "The optional app manifest is invalid.");
        }

        AppDiscoveryResult discovery = AppEntryPointDiscovery.Discover(
            appCatalog,
            new Dictionary<string, System.Reflection.Assembly>(StringComparer.Ordinal)
            {
                [entryPoint.Assembly] = assembly.Assembly
            });
        if (!discovery.IsSuccess || discovery.Descriptors is null
            || !discovery.Descriptors.TryGetValue(appId, out AppDescriptor? descriptor))
        {
            string detail = discovery.Errors.FirstOrDefault()?.Message
                ?? "The optional app entry point could not be discovered.";
            return AppDescriptorLoadResult.Unavailable(detail);
        }

        _descriptors.TryAdd(appId, descriptor);
        return AppDescriptorLoadResult.Available();
    }
}
