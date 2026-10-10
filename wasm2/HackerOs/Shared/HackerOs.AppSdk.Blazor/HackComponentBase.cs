using HackerOs.AppSdk;
using Microsoft.AspNetCore.Components;

namespace HackerOs.AppSdk.Blazor;

/// <summary>
/// Base class for a child component of a HackerOS window app. It receives the window's
/// <see cref="IAppExecutionContext"/> through a cascading value (set by the window host) and runs its
/// lifecycle methods, event handlers and after-render work inside that app's
/// <see cref="HackExecutionContext"/> scope, so it can call the <c>Hack*</c> file API directly
/// (ADR 0043, section 2).
/// </summary>
/// <remarks>
/// A plain <see cref="ComponentBase"/> child still works for rendering, but its <c>@onclick</c>
/// handlers run outside any ambient scope; derive from this class whenever a child component touches
/// files. Outside a window (in a test, for instance) with no cascading context, the component simply
/// keeps whatever scope is already ambient.
/// </remarks>
public abstract class HackComponentBase : ComponentBase, IHandleEvent, IHandleAfterRender
{
    private bool _hasCalledOnAfterRender;

    /// <summary>Gets the execution context of the window app this component belongs to.</summary>
    [CascadingParameter]
    protected IAppExecutionContext? AppContext { get; set; }

    /// <summary>Assigns parameters and runs the synchronous lifecycle inside the app's ambient scope.</summary>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        IAppExecutionContext? context = parameters.TryGetValue(nameof(AppContext), out IAppExecutionContext? cascaded)
            ? cascaded
            : AppContext;
        using IDisposable? scope = AmbientComponentDispatch.Enter(context);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg) =>
        AmbientComponentDispatch.HandleEventAsync(AppContext, callback, arg, StateHasChanged);

    /// <inheritdoc />
    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        bool firstRender = !_hasCalledOnAfterRender;
        _hasCalledOnAfterRender = true;

        using IDisposable? scope = AmbientComponentDispatch.Enter(AppContext);
        OnAfterRender(firstRender);
        return OnAfterRenderAsync(firstRender);
    }
}
