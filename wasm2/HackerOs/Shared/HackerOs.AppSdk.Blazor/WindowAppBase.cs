using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
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
    private bool _permissionErrorHandlerAttached;

    [Inject]
    private IWindowAppFrameworkLifecycle? FrameworkLifecycle { get; set; }

    private IWindowAppFrameworkLifecycle EffectiveFrameworkLifecycle =>
        FrameworkLifecycle ?? NullWindowAppFrameworkLifecycle.Instance;

    /// <summary>Gets or sets the execution context assigned by the window host.</summary>
    [Parameter]
    [EditorRequired]
    public IAppExecutionContext AppContext { get; set; } = default!;

    /// <summary>Gets or sets the ecosystem-owned file dialog service.</summary>
    [Inject]
    protected IFileDialogService FileDialogs { get; set; } = default!;

    /// <summary>Gets or sets the ecosystem-owned dialog service.</summary>
    [Inject]
    protected IDialogService Dialogs { get; set; } = default!;

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
        await EffectiveFrameworkLifecycle.OnAfterRenderAsync(this, firstRender);
        await OnAppAfterRenderAsync(firstRender);
    }

    /// <summary>Displays the standard file-open dialog.</summary>
    protected ValueTask<OpenFileDialogResult> OpenFileAsync(
        OpenFileDialogRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureFileDialogService();
        return FileDialogs.OpenFileAsync(AppContext, request, cancellationToken);
    }

    /// <summary>Displays the standard file-save dialog.</summary>
    protected ValueTask<SaveFileDialogResult> SaveFileAsync(
        SaveFileDialogRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureFileDialogService();
        return FileDialogs.SaveFileAsync(AppContext, request, cancellationToken);
    }

    /// <summary>Displays the standard folder-selection dialog.</summary>
    protected ValueTask<SelectFolderDialogResult> SelectFolderAsync(
        SelectFolderDialogRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureFileDialogService();
        return FileDialogs.SelectFolderAsync(AppContext, request, cancellationToken);
    }

    /// <summary>Displays a message box dialog.</summary>
    protected ValueTask<MessageBoxDialogResult> MessageBox(
        string title,
        string content,
        MessageBoxType dialogType = MessageBoxType.Ok,
        CancellationToken cancellationToken = default)
    {
        EnsureBasicDialogService();
        return Dialogs.MessageBoxAsync(
            AppContext,
            new MessageBoxDialogRequest
            {
                Title = title,
                Content = content,
                DialogType = dialogType
            },
            cancellationToken);
    }

    /// <summary>Displays a text input dialog.</summary>
    protected ValueTask<TextInputDialogResult> TextInput(
        string title,
        string content,
        string? defaultValue = null,
        string? placeholder = null,
        CancellationToken cancellationToken = default)
    {
        EnsureBasicDialogService();
        return Dialogs.TextInputAsync(
            AppContext,
            new TextInputDialogRequest
            {
                Title = title,
                Content = content,
                DefaultValue = defaultValue,
                Placeholder = placeholder
            },
            cancellationToken);
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

        if (!_permissionErrorHandlerAttached)
        {
            _permissionErrorHandlerAttached = true;
            AppContext.PermissionErrors.PermissionDenied += HandlePermissionDenied;
        }
    }

    /// <summary>
    /// Runs when a permission-class filesystem error (missing capability, missing authority, or a
    /// denied selected-handle/mode check) is raised for this app instance. Override and set
    /// <see cref="AppPermissionErrorEventArgs.ErrorHandled"/> to suppress the host's default error
    /// notification when the app already surfaces this failure through its own UI.
    /// </summary>
    protected virtual void OnPermissionErrorRaised(AppPermissionErrorEventArgs e)
    {
    }

    private async void HandlePermissionDenied(object? sender, AppPermissionErrorEventArgs e)
    {
        OnPermissionErrorRaised(e);
        if (e.ErrorHandled)
        {
            return;
        }

        e.ErrorHandled = true;
        await MessageBox("Permission Denied", DescribePermissionError(e.Error), MessageBoxType.Ok);
    }

    private static string DescribePermissionError(FileSystemError error) => error.Code switch
    {
        FileSystemErrorCode.PermissionDenied => $"You don't have permission to access '{error.Path}'.",
        FileSystemErrorCode.CapabilityDenied => $"This app isn't authorized to access '{error.Path}'.",
        FileSystemErrorCode.AuthorityDenied => $"This operation on '{error.Path}' requires elevated authority.",
        _ => $"Access to '{error.Path}' was denied."
    };

    private void EnsureFileDialogService()
    {
        ValidateContext();
        if (FileDialogs is null && Dialogs is not null)
        {
            FileDialogs = Dialogs;
        }

        if (FileDialogs is null)
        {
            throw new InvalidOperationException("The window host must provide IFileDialogService.");
        }
    }

    private void EnsureBasicDialogService()
    {
        ValidateContext();
        if (Dialogs is null && FileDialogs is IDialogService dialogs)
        {
            Dialogs = dialogs;
        }

        if (Dialogs is null)
        {
            throw new InvalidOperationException("The window host must provide IDialogService.");
        }
    }
}