using Microsoft.AspNetCore.Components;

namespace HackerOs.AppSdk.Blazor.Components;

/// <summary>
/// Finds the component an installed app registered for a contract (ADR 0063). Implemented by the
/// platform, which reads the manifests' <c>components</c>, loads the provider's assembly when it is
/// lazy and checks the type; apps embed through <see cref="HackEmbeddedComponent{TController}"/>
/// instead of calling it.
/// </summary>
public interface IHackComponentHost
{
    /// <summary>Finds the component registered for <paramref name="contract"/> by an installed app.</summary>
    /// <param name="contract">Contract name, e.g. <c>HackComponentContracts.Terminal</c>.</param>
    /// <param name="cancellationToken">Cancels the wait for the provider's assembly.</param>
    /// <returns>The component type and its provider, or why none can be embedded.</returns>
    Task<HackComponentResolution> ResolveAsync(string contract, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="IHackComponentHost.ResolveAsync"/>.</summary>
/// <param name="Contract">The requested contract.</param>
/// <param name="ComponentType">The provider's component, deriving from <see cref="HackEmbeddableComponent"/>; <see langword="null"/> when unavailable.</param>
/// <param name="ProviderAppId">The app that registered it, when one was found.</param>
/// <param name="Error">Why no component can be embedded, for the user.</param>
public sealed record HackComponentResolution(
    string Contract,
    Type? ComponentType,
    string? ProviderAppId,
    string? Error)
{
    /// <summary>Gets whether a component can be embedded.</summary>
    public bool IsResolved => ComponentType is not null;

    /// <summary>Creates a resolved outcome.</summary>
    public static HackComponentResolution Resolved(
        string contract,
        Type componentType,
        string providerAppId) =>
        new(contract, componentType, providerAppId, null);

    /// <summary>Creates an unavailable outcome with its reason.</summary>
    public static HackComponentResolution Unavailable(string contract, string error, string? providerAppId = null) =>
        new(contract, null, providerAppId, error);
}

/// <summary>
/// Base class of a component an app registers for a contract (ADR 0063), as a KPart: the host
/// renders it inside its own window, through <see cref="HackEmbeddedComponent{TController}"/>.
/// </summary>
/// <remarks>
/// The component runs in the <b>host</b> app's context (the cascading <c>AppContext</c> of its
/// window): files, processes and capabilities are the host's, as a library loaded in its process.
/// Once ready, it hands its controller (the contract's interface) to the host through
/// <see cref="ControllerReady"/>; the controller stays valid until the host stops rendering it.
/// </remarks>
public abstract class HackEmbeddableComponent : HackComponentBase
{
    /// <summary>Gets or sets the contract's options record, chosen by the host (e.g. <c>HackTerminalOptions</c>).</summary>
    [Parameter]
    public object? Options { get; set; }

    /// <summary>Gets or sets the callback receiving the controller once the component is ready.</summary>
    [Parameter]
    public EventCallback<object?> ControllerReady { get; set; }

    /// <summary>Gets the execution context of the host window, or throws when the component is not inside one.</summary>
    protected IAppExecutionContext HostContext =>
        AppContext ?? throw new InvalidOperationException("An embedded component must be rendered inside a HackerOS window.");
}
