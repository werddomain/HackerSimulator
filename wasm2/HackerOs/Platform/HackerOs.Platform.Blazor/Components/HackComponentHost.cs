using System.Diagnostics.CodeAnalysis;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Blazor.Components;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Platform.Core.Lifecycle;

namespace HackerOs.Platform.Blazor.Components;

/// <summary>
/// A component the platform provides itself for a contract, without a manifest (ADR 0148 §3.2),
/// such as the OS context menu.
/// </summary>
public sealed class HackPlatformComponent
{
    /// <summary>Initializes a platform registration.</summary>
    /// <param name="contract">The contract name.</param>
    /// <param name="componentType">The component, deriving from <see cref="HackEmbeddableComponent"/>.</param>
    public HackPlatformComponent(
        string contract,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        Type componentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contract);
        ArgumentNullException.ThrowIfNull(componentType);
        if (componentType.IsAbstract || !typeof(HackEmbeddableComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException($"'{componentType.FullName}' is not an embeddable component.", nameof(componentType));
        }

        Contract = contract;
        ComponentType = componentType;
    }

    /// <summary>Gets the contract name.</summary>
    public string Contract { get; }

    /// <summary>Gets the component the platform renders for the contract.</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public Type ComponentType { get; }
}

/// <summary>
/// The OS registry of embeddable components (ADR 0063, ADR 0148 §3): the providers the catalog read
/// from the installed apps' manifests at boot (<see cref="AppCatalog.ComponentProviders"/>), plus the
/// platform's own. It loads an app provider's assembly the way a launch does and checks the type
/// before a window renders it. Like KDE's KParts trader, the host asks for a contract, never for an app.
/// </summary>
public sealed class HackComponentHost : IHackComponentHost
{
    private readonly AppCatalog _catalog;
    private readonly IAppEnablementRegistry _enablement;
    private readonly Func<string, CancellationToken, Task<(AppDescriptor? Descriptor, string? Error)>> _descriptors;
    private readonly Dictionary<string, HackPlatformComponent> _platform = new(StringComparer.Ordinal);
    private readonly Action<string>? _reportError;

    /// <summary>Initializes the registry over the host catalog, with no platform component and no system log.</summary>
    /// <param name="catalog">Build-known apps and their manifests.</param>
    /// <param name="enablement">Installed state: an uninstalled app provides nothing.</param>
    /// <param name="descriptors">Gets an app's descriptor, lazy-loading its assembly (<c>AppLifecycleOrchestrator.EnsureDescriptorAsync</c>).</param>
    public HackComponentHost(
        AppCatalog catalog,
        IAppEnablementRegistry enablement,
        Func<string, CancellationToken, Task<(AppDescriptor? Descriptor, string? Error)>> descriptors)
        : this(catalog, enablement, descriptors, platformComponents: null, reportError: null)
    {
    }

    /// <summary>Initializes the registry over the host catalog.</summary>
    /// <param name="catalog">Build-known apps and their manifests.</param>
    /// <param name="enablement">Installed state: an uninstalled app provides nothing.</param>
    /// <param name="descriptors">Gets an app's descriptor, lazy-loading its assembly (<c>AppLifecycleOrchestrator.EnsureDescriptorAsync</c>).</param>
    /// <param name="platformComponents">The components the platform provides itself (ADR 0148 §3.2); tried before any app.</param>
    /// <param name="reportError">
    /// Writes a line to the system log (<c>/var/log/syslog</c>): a reserved contract with no
    /// working provider is an error of the OS, never a normal state (ADR 0148 §5).
    /// </param>
    public HackComponentHost(
        AppCatalog catalog,
        IAppEnablementRegistry enablement,
        Func<string, CancellationToken, Task<(AppDescriptor? Descriptor, string? Error)>> descriptors,
        IEnumerable<HackPlatformComponent>? platformComponents,
        Action<string>? reportError)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _enablement = enablement ?? throw new ArgumentNullException(nameof(enablement));
        _descriptors = descriptors ?? throw new ArgumentNullException(nameof(descriptors));
        _reportError = reportError;
        foreach (HackPlatformComponent component in platformComponents ?? [])
        {
            if (!_platform.TryAdd(component.Contract, component))
            {
                throw new ArgumentException($"The platform registers '{component.Contract}' twice.", nameof(platformComponents));
            }
        }
    }

    /// <summary>
    /// Lists the installed apps registering <paramref name="contract"/>, in the order they are
    /// tried: system apps first (the OS's own provider), then by app id. Registrations the catalog
    /// refused (ADR 0148 §3.3) are not listed; platform components are not apps and are not listed.
    /// </summary>
    /// <param name="contract">Contract name.</param>
    public IReadOnlyList<(string AppId, AppComponentManifest Component)> GetProviders(string contract) =>
        AppComponentContracts.GetProviderScope(contract) == AppComponentProviderScope.Platform
            ? []
            : [.. _catalog.GetComponentProviders(contract)
                .Where(provider => _enablement.IsEnabled(provider.AppId))
                .Select(provider => (provider.AppId, provider.Component))];

    /// <inheritdoc />
    public async Task<HackComponentResolution> ResolveAsync(string contract, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contract);
        if (_platform.TryGetValue(contract, out HackPlatformComponent? platform))
        {
            return HackComponentResolution.Resolved(contract, platform.ComponentType, HackComponentResolution.PlatformProviderId);
        }

        IReadOnlyList<(string AppId, AppComponentManifest Component)> providers = GetProviders(contract);
        if (providers.Count == 0)
        {
            return Report(HackComponentResolution.Unavailable(contract, $"No installed app provides '{contract}'."));
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

        return Report(firstFailure!);
    }

    /// <summary>
    /// Writes an unavailable reserved contract to the system log (ADR 0148 §5): its providers are
    /// the OS's own, so their absence is a fault to investigate. An open contract without a
    /// provider (no terminal installed, say) is a normal state and is only shown to the user.
    /// </summary>
    private HackComponentResolution Report(HackComponentResolution resolution)
    {
        if (_reportError is not null
            && AppComponentContracts.GetProviderScope(resolution.Contract) != AppComponentProviderScope.AnyApp)
        {
            _reportError($"component.unavailable: '{resolution.Contract}' has no working provider: {resolution.Error}");
        }

        return resolution;
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
