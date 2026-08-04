using HackerOs.AppSdk;
using HackerOs.AppSdk.Blazor;
using HackerOs.Platform.Core.Discovery;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace HackerOs.Platform.Blazor.Windows;

/// <summary>Renders one validated Window app component with its bound execution context.</summary>
public sealed class WindowAppRenderer : ComponentBase, IDisposable
{
    private IDisposable? _closeGuardRegistration;

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

    /// <inheritdoc />
    protected override void OnParametersSet() =>
        WindowAppRenderValidator.Validate(Window, Descriptor, AppContext);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
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
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _closeGuardRegistration?.Dispose();
        _closeGuardRegistration = null;
    }
}
