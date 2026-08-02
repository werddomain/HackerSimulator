using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using Microsoft.AspNetCore.Components;

namespace HackerOs.AppSdk.Blazor;

/// <summary>
/// Base component for a HackerOS window application.
/// </summary>
/// <remarks>
/// Component lifecycle overrides are sealed so framework setup cannot be skipped.
/// Apps use the corresponding <c>OnApp*</c> hooks instead.
/// </remarks>
public abstract class WindowAppBase : ComponentBase
{
    private Guid? _boundInstanceId;

    [Inject]
    private IWindowAppFrameworkLifecycle FrameworkLifecycle { get; set; } =
        NullWindowAppFrameworkLifecycle.Instance;

    /// <summary>Gets or sets the execution context assigned by the window host.</summary>
    [Parameter]
    [EditorRequired]
    public IAppExecutionContext AppContext { get; set; } = default!;

    /// <summary>Gets or sets the ecosystem-owned file dialog service.</summary>
    [Inject]
    protected IFileDialogService FileDialogs { get; set; } = default!;

    /// <summary>Gets the validated window app manifest.</summary>
    protected AppManifest Manifest => AppContext.Manifest;

    /// <summary>Prevents apps from replacing parameter assignment and bypassing lifecycle validation.</summary>
    public sealed override Task SetParametersAsync(ParameterView parameters) =>
        base.SetParametersAsync(parameters);

    /// <inheritdoc />
    protected sealed override void OnInitialized()
    {
        ValidateContext();
        OnAppInitialized();
    }

    /// <inheritdoc />
    protected sealed override Task OnInitializedAsync() => OnAppInitializedAsync();

    /// <inheritdoc />
    protected sealed override void OnParametersSet()
    {
        ValidateContext();
        OnAppParametersSet();
    }

    /// <inheritdoc />
    protected sealed override Task OnParametersSetAsync() => OnAppParametersSetAsync();

    /// <inheritdoc />
    protected sealed override bool ShouldRender() => ShouldRenderApp();

    /// <inheritdoc />
    protected sealed override void OnAfterRender(bool firstRender) => OnAppAfterRender(firstRender);

    /// <inheritdoc />
    protected sealed override async Task OnAfterRenderAsync(bool firstRender)
    {
        await FrameworkLifecycle.OnAfterRenderAsync(this, firstRender);
        await OnAppAfterRenderAsync(firstRender);
    }

    /// <summary>Displays the standard file-open dialog.</summary>
    protected ValueTask<OpenFileDialogResult> OpenFileAsync(
        OpenFileDialogRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureDialogService();
        return FileDialogs.OpenFileAsync(AppContext, request, cancellationToken);
    }

    /// <summary>Displays the standard file-save dialog.</summary>
    protected ValueTask<SaveFileDialogResult> SaveFileAsync(
        SaveFileDialogRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureDialogService();
        return FileDialogs.SaveFileAsync(AppContext, request, cancellationToken);
    }

    /// <summary>Displays the standard folder-selection dialog.</summary>
    protected ValueTask<SelectFolderDialogResult> SelectFolderAsync(
        SelectFolderDialogRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureDialogService();
        return FileDialogs.SelectFolderAsync(AppContext, request, cancellationToken);
    }

    /// <summary>Runs app-specific synchronous initialization.</summary>
    protected virtual void OnAppInitialized()
    {
    }

    /// <summary>Runs app-specific asynchronous initialization.</summary>
    protected virtual Task OnAppInitializedAsync() => Task.CompletedTask;

    /// <summary>Runs after the host supplies or updates component parameters.</summary>
    protected virtual void OnAppParametersSet()
    {
    }

    /// <summary>Runs asynchronously after the host supplies or updates component parameters.</summary>
    protected virtual Task OnAppParametersSetAsync() => Task.CompletedTask;

    /// <summary>Lets an app suppress a render without replacing framework lifecycle code.</summary>
    protected virtual bool ShouldRenderApp() => true;

    /// <summary>Runs app-specific synchronous post-render work.</summary>
    protected virtual void OnAppAfterRender(bool firstRender)
    {
    }

    /// <summary>Runs app-specific asynchronous post-render work after framework setup.</summary>
    protected virtual Task OnAppAfterRenderAsync(bool firstRender) => Task.CompletedTask;

    private void ValidateContext()
    {
        if (AppContext is null)
        {
            throw new InvalidOperationException("The window host must provide AppContext.");
        }

        ManifestValidationResult validation = AppManifestValidator.Validate(AppContext.Manifest);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("The window host provided an invalid app manifest.");
        }

        if (AppContext.Manifest.Kind != AppKind.Window)
        {
            throw new InvalidOperationException(
                $"Manifest kind '{AppContext.Manifest.Kind}' cannot be hosted by WindowAppBase.");
        }

        if (_boundInstanceId is Guid instanceId && instanceId != AppContext.InstanceId)
        {
            throw new InvalidOperationException("A window component cannot be rebound to another app instance.");
        }

        _boundInstanceId = AppContext.InstanceId;
    }

    private void EnsureDialogService()
    {
        ValidateContext();
        if (FileDialogs is null)
        {
            throw new InvalidOperationException("The window host must provide IFileDialogService.");
        }
    }
}