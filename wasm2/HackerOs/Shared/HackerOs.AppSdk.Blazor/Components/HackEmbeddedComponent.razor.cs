using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace HackerOs.AppSdk.Blazor.Components;

/// <summary>
/// Shows the component an installed app registered for <see cref="Contract"/> (ADR 0063) and hands
/// its controller, typed as <typeparamref name="TController"/>, to the host.
/// </summary>
/// <typeparam name="TController">The contract's interface, e.g. <c>IHackTerminal</c>.</typeparam>
public partial class HackEmbeddedComponent<TController> : IAsyncDisposable
    where TController : class
{
    private HackComponentResolution? _resolution;
    private Dictionary<string, object?> _parameters = new(StringComparer.Ordinal);
    private string? _resolvedContract;
    private readonly CancellationTokenSource _disposeCts = new();

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    /// <summary>Gets or sets the contract name, e.g. <c>HackComponentContracts.Terminal</c>.</summary>
    [Parameter, EditorRequired]
    public string Contract { get; set; } = string.Empty;

    /// <summary>Gets or sets the contract's options, passed to the component when it is created.</summary>
    [Parameter]
    public object? Options { get; set; }

    /// <summary>
    /// Gets or sets the callback receiving the controller once the component is ready (or
    /// <see langword="null"/> when the provider does not implement the contract). The controller
    /// stays valid until this component is removed from the render tree.
    /// </summary>
    [Parameter]
    public EventCallback<TController?> ControllerChanged { get; set; }

    /// <summary>Gets or sets what to show, with the reason, when no installed app provides the contract.</summary>
    [Parameter]
    public RenderFragment<string>? Unavailable { get; set; }

    /// <summary>Gets the app that provides the component, once found.</summary>
    public string? ProviderAppId => _resolution?.ProviderAppId;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        // A new dictionary only when the options change: the embedded component is not
        // re-rendered each time its host re-renders.
        if (_parameters.Count == 0 || !ReferenceEquals(_parameters[nameof(HackEmbeddableComponent.Options)], Options))
        {
            _parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [nameof(HackEmbeddableComponent.Options)] = Options,
                [nameof(HackEmbeddableComponent.ControllerReady)] = EventCallback.Factory.Create<object?>(this, OnControllerReadyAsync)
            };
        }

        if (string.Equals(_resolvedContract, Contract, StringComparison.Ordinal))
        {
            return;
        }

        _resolvedContract = Contract;
        _resolution = null;
        // Optional service: a host without the platform (a unit test) shows the fallback.
        if (Services.GetService(typeof(IHackComponentHost)) is not IHackComponentHost host)
        {
            _resolution = HackComponentResolution.Unavailable(Contract, L["Embedded.Unavailable"]);
            return;
        }

        try
        {
            _resolution = await host.ResolveAsync(Contract, _disposeCts.Token);
        }
        catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
        {
            // Closed while the provider was loading.
        }
    }

    /// <summary>Gets the SDK's texts in the app's language (the cascaded context, or the ambient app).</summary>
    private Microsoft.Extensions.Localization.IStringLocalizer<Strings> L => AppContext is { } context
        ? HackerOs.AppSdk.Globalization.HackLocalization.GetLocalizer<Strings>(context)
        : HackerOs.AppSdk.Globalization.HackLocalization.GetLocalizer<Strings>();

    /// <summary>Renders the provider's component with the contract's parameters, as <c>DynamicComponent</c> does.</summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072",
        Justification = "Component types come from build-known manifests whose assemblies a trimmed host keeps whole (ADR 0113: generated root descriptor).")]
    private void BuildProvider(RenderTreeBuilder builder)
    {
        if (_resolution?.ComponentType is not { } componentType)
        {
            return;
        }

        builder.OpenComponent(0, componentType);
        int sequence = 1;
        foreach ((string name, object? value) in _parameters)
        {
            builder.AddComponentParameter(sequence++, name, value);
        }

        builder.CloseComponent();
    }

    private Task OnControllerReadyAsync(object? controller)
    {
        if (controller is not null and not TController)
        {
            // The provider does not implement the contract it registered for: show why instead of
            // handing the host an object it cannot use.
            _resolution = HackComponentResolution.Unavailable(
                Contract,
                L["Embedded.NotImplemented", _resolution?.ProviderAppId ?? string.Empty, typeof(TController).Name],
                _resolution?.ProviderAppId);
            return ControllerChanged.InvokeAsync(null);
        }

        return ControllerChanged.InvokeAsync(controller as TController);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        return ValueTask.CompletedTask;
    }
}
