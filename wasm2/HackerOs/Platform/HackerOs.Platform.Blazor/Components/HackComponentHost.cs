using System.Diagnostics.CodeAnalysis;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Blazor.Components;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Platform.Core.Lifecycle;

namespace HackerOs.Platform.Blazor.Components;

/// <summary>
/// The OS registry of embeddable components (ADR 0063): reads the <c>components</c> of the installed
/// apps' manifests, loads the provider's assembly the way a launch does, and checks the type before a
/// window renders it. Like KDE's KParts trader, the host asks for a contract, never for an app.
/// </summary>
public sealed class HackComponentHost : IHackComponentHost
{
    private readonly AppCatalog _catalog;
    private readonly IAppEnablementRegistry _enablement;
    private readonly Func<string, CancellationToken, Task<(AppDescriptor? Descriptor, string? Error)>> _descriptors;

    /// <summary>Initializes the registry over the host catalog.</summary>
    /// <param name="catalog">Build-known apps and their manifests.</param>
    /// <param name="enablement">Installed state: an uninstalled app provides nothing.</param>
    /// <param name="descriptors">Gets an app's descriptor, lazy-loading its assembly (<c>AppLifecycleOrchestrator.EnsureDescriptorAsync</c>).</param>
    public HackComponentHost(
        AppCatalog catalog,
        IAppEnablementRegistry enablement,
        Func<string, CancellationToken, Task<(AppDescriptor? Descriptor, string? Error)>> descriptors)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _enablement = enablement ?? throw new ArgumentNullException(nameof(enablement));
        _descriptors = descriptors ?? throw new ArgumentNullException(nameof(descriptors));
    }

    /// <summary>
    /// Lists the installed apps registering <paramref name="contract"/>, in the order they are
    /// tried: system apps first (the OS's own provider), then by app id.
    /// </summary>
    /// <param name="contract">Contract name.</param>
    public IReadOnlyList<(string AppId, AppComponentManifest Component)> GetProviders(string contract) =>
        [.. _catalog.Manifests.Values
            .Where(manifest => _enablement.IsEnabled(manifest.Id))
            .SelectMany(manifest => (manifest.Components ?? [])
                .Where(component => string.Equals(component.Contract, contract, StringComparison.Ordinal))
                .Select(component => (manifest.Id, component)))
            .OrderBy(entry => _catalog.IsSystemApp(entry.Id) ? 0 : 1)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)];

    /// <inheritdoc />
    public async Task<HackComponentResolution> ResolveAsync(string contract, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contract);
        IReadOnlyList<(string AppId, AppComponentManifest Component)> providers = GetProviders(contract);
        if (providers.Count == 0)
        {
            return HackComponentResolution.Unavailable(contract, $"No installed app provides '{contract}'.");
        }

        // The first provider that loads wins; the reason of the first failure is kept for the user.
        HackComponentResolution? firstFailure = null;
        foreach ((string appId, AppComponentManifest component) in providers)
        {
            HackComponentResolution resolution = await ResolveProviderAsync(contract, appId, component, cancellationToken)
                .ConfigureAwait(false);
            if (resolution.IsResolved)
            {
                return resolution;
            }

            firstFailure ??= resolution;
        }

        return firstFailure!;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Component types come from build-known manifests whose assemblies a trimmed host keeps whole (ADR 0113: generated root descriptor).")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072",
        Justification = "Component types come from build-known manifests whose assemblies a trimmed host keeps whole (ADR 0113: generated root descriptor).")]
    private async Task<HackComponentResolution> ResolveProviderAsync(
        string contract, string appId, AppComponentManifest component, CancellationToken cancellationToken)
    {
        (AppDescriptor? descriptor, string? error) = await _descriptors(appId, cancellationToken).ConfigureAwait(false);
        if (descriptor is null)
        {
            return HackComponentResolution.Unavailable(contract, error ?? $"The app '{appId}' could not be loaded.", appId);
        }

        // The manifest validator keeps the component in the entry-point assembly, the one lazy
        // loading has just fetched.
        Type? type = descriptor.Assembly.GetType(component.Type, throwOnError: false);
        if (type is null || type.IsAbstract || !typeof(HackEmbeddableComponent).IsAssignableFrom(type))
        {
            return HackComponentResolution.Unavailable(
                contract,
                $"The app '{appId}' registers '{component.Type}', which is not an embeddable component.",
                appId);
        }

        return HackComponentResolution.Resolved(contract, type, appId);
    }
}
