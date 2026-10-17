using HackerOs.AppSdk;
using HackerOs.AppSdk.Blazor;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Windowing.Core;
using HackerOs.Windowing.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace HackerOs.Platform.Blazor.Windows;

/// <summary>Renders one validated Window app component with its bound execution context.</summary>
public sealed class WindowAppRenderer : ComponentBase, IDisposable
{
    private IDisposable? _closeGuardRegistration;
    private IDisposable? _backHandlerRegistration;
    private IDisposable? _windowEventsRegistration;

    /// <summary>Gets or sets the authoritative window snapshot.</summary>
    [Parameter, EditorRequired]
    public WindowRuntimeState Window { get; set; } = null!;

    /// <summary>Gets or sets the trusted catalog descriptor.</summary>
    [Parameter, EditorRequired]
    public AppDescriptor Descriptor { get; set; } = null!;

    /// <summary>Gets or sets the execution context bound to this app instance.</summary>
    [Parameter, EditorRequired]
    public IAppExecutionContext AppContext { get; set; } = null!;

    /// <summary>Gets or sets the platform registry for the rendered app's close guard.</summary>
    [Parameter, EditorRequired]
    public WindowCloseGuardRegistry CloseGuards { get; set; } = null!;

    /// <summary>Gets or sets the platform registry for the rendered app's Back handler.</summary>
    [Parameter, EditorRequired]
    public AppBackHandlerRegistry BackHandlers { get; set; } = null!;

    /// <summary>
    /// Gets or sets the dispatcher of window events to the app's <c>OnWindow*</c> hooks (ADR 0095 §1).
    /// Optional: without it the app receives no window events.
    /// </summary>
    [Parameter]
    public WindowAppEventDispatcher? WindowEvents { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet() =>
        WindowAppRenderValidator.Validate(Window, Descriptor, AppContext);

    /// <inheritdoc />
    /// <remarks>
    /// The app is wrapped in a fixed <see cref="CascadingValue{TValue}"/> carrying its context, so
    /// <see cref="HackComponentBase"/> children run inside the same ambient app scope as the window
    /// (ADR 0043). The context never changes for a rendered window (rebinding is rejected).
    /// </remarks>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<IAppExecutionContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<IAppExecutionContext>.Value), AppContext);
        builder.AddComponentParameter(2, nameof(CascadingValue<IAppExecutionContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<IAppExecutionContext>.ChildContent), (RenderFragment)RenderApp);
        builder.CloseComponent();
    }

    private void RenderApp(RenderTreeBuilder builder)
    {
        builder.OpenComponent(0, Descriptor.EntryPointType);
        builder.AddComponentParameter(1, nameof(WindowAppBase.AppContext), AppContext);
        builder.AddComponentReferenceCapture(2, CaptureComponent);
        builder.CloseComponent();
    }

    private void CaptureComponent(object component)
    {
        _closeGuardRegistration?.Dispose();
        _closeGuardRegistration = component is IWindowCloseGuard guard
            ? CloseGuards.Register(Window.Id, guard)
            : null;

        _backHandlerRegistration?.Dispose();
        _backHandlerRegistration = component is IAppBackHandler backHandler
            ? BackHandlers.Register(Window.Id, backHandler)
            : null;

        _windowEventsRegistration?.Dispose();
        _windowEventsRegistration = component is IWindowAppEventTarget target && WindowEvents is not null
            ? WindowEvents.Register(Window.Id, target)
            : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _closeGuardRegistration?.Dispose();
        _closeGuardRegistration = null;

        _backHandlerRegistration?.Dispose();
        _backHandlerRegistration = null;

        _windowEventsRegistration?.Dispose();
        _windowEventsRegistration = null;
    }
}
