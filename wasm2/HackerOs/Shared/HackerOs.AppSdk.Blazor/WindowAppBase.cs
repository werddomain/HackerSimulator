using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Diagnostics;
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
public abstract partial class WindowAppBase : ComponentBase, IHandleEvent, IHandleAfterRender, IWindowAppEventTarget
{
    private Guid? _boundInstanceId;
    private bool _permissionErrorHandlerAttached;
    private bool _hasCalledOnAfterRender;

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

    /// <summary>
    /// Gets or sets the admin-credential verifier used to offer an approval prompt on a denied
    /// permission (`ADR 0042`). Optional: when unavailable (e.g. in tests), a denial simply falls
    /// back to the plain "Permission Denied" message.
    /// </summary>
    [Inject]
    protected IAdminElevationVerifier? ElevationVerifier { get; set; }

    /// <summary>Gets the validated window app manifest.</summary>
    protected AppManifest Manifest => AppContext.Manifest;

    /// <summary>
    /// Gets the arguments this window was launched with, without <c>argv[0]</c> (the app id), like
    /// <c>Main(string[] args)</c>. Opening a file from the File Explorer or with <c>open</c> starts
    /// the handler app with the file's absolute path as its first argument
    /// (<c>OpenFileIntent</c>, ADR 0050), so a document app reads its initial file here.
    /// </summary>
    protected IReadOnlyList<string> LaunchArguments
    {
        get
        {
            IReadOnlyList<string> argv = AppContext.Environment.GetCommandLineArgs();
            return argv.Count > 1 ? [.. argv.Skip(1)] : [];
        }
    }

    /// <summary>
    /// Gets the file this window was asked to open: the first launch argument when it is an
    /// absolute virtual path, otherwise <see langword="null"/> (a plain launch from the menu).
    /// </summary>
    protected VirtualPath? LaunchFile
    {
        get
        {
            if (LaunchArguments is not [{ } first, ..] || !first.StartsWith('/'))
            {
                return null;
            }

            try
            {
                return VirtualPath.Parse(first);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Prevents apps from replacing parameter assignment and bypassing lifecycle validation. The
    /// synchronous lifecycle (and the continuations of the asynchronous one) runs inside the app's
    /// <see cref="HackExecutionContext"/> scope (ADR 0043).
    /// </summary>
    public sealed override Task SetParametersAsync(ParameterView parameters)
    {
        IAppExecutionContext? context = parameters.TryGetValue(nameof(AppContext), out IAppExecutionContext? supplied)
            ? supplied
            : AppContext;
        using IDisposable? scope = AmbientComponentDispatch.Enter(context);
        return base.SetParametersAsync(parameters);
    }

    /// <summary>Runs every UI event handler of the app inside its ambient scope (ADR 0043).</summary>
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg) =>
        AmbientComponentDispatch.HandleEventAsync(AppContext, callback, arg, StateHasChanged);

    /// <summary>Runs the after-render lifecycle inside the app's ambient scope (ADR 0043).</summary>
    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        bool firstRender = !_hasCalledOnAfterRender;
        _hasCalledOnAfterRender = true;

        using IDisposable? scope = AmbientComponentDispatch.Enter(AppContext);
        OnAfterRender(firstRender);
        return OnAfterRenderAsync(firstRender);
    }

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
            AppContext.StopRequested += HandleStopRequested;
        }
    }

    /// <summary>
    /// Runs when the OS asks the app's process to stop (ADR 0069): <c>kill</c>, "End task", logout,
    /// shutdown. The window closes once the returned task completes, or at
    /// <see cref="HackStopRequestedEventArgs.Deadline"/> at the latest, when the OS kills the process.
    /// An app that holds state finishes or cancels its current operation here, and keeps a recovery
    /// draft of unsaved work instead of losing it silently. It is not called when the user closes the
    /// window: <see cref="IWindowCloseGuard"/> asks the user then.
    /// </summary>
    /// <param name="e">Reason and deadline of the request.</param>
    /// <returns>The work the OS waits for before closing the window.</returns>
    protected virtual Task OnAppStopRequestedAsync(HackStopRequestedEventArgs e) => Task.CompletedTask;

    /// <summary>
    /// Hands the stop request to the app on the renderer's dispatcher (component state is touched)
    /// and asks the OS to wait for it.
    /// </summary>
    private void HandleStopRequested(object? sender, HackStopRequestedEventArgs e)
    {
        Task work;
        try
        {
            work = InvokeAsync(() =>
            {
                using IDisposable? scope = AmbientComponentDispatch.Enter(AppContext);
                return OnAppStopRequestedAsync(e);
            });
        }
        catch (InvalidOperationException)
        {
            // Not attached to a renderer (never rendered): nothing on screen to save from.
            work = OnAppStopRequestedAsync(e);
        }

        e.Defer(work);
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

        // ADR 0049 section 3: under dangerousCapabilities=block, a dangerous capability of a
        // non-system app cannot be approved once either, so no prompt is offered.
        if (ElevationVerifier is not null
            && e.Error.RequiredCapability is not null
            && !AppContext.OneShotElevation.IsBlocked(e.Error.RequiredCapability))
        {
            EnsureBasicDialogService();
            CredentialPromptDialogResult prompt = await Dialogs.CredentialPromptAsync(AppContext, new CredentialPromptDialogRequest
            {
                Title = "Permission Denied — Request Approval?",
                Description = $"'{Manifest.Name}' was denied access to '{e.Error.Path}'. " +
                    "Enter an enabled Administrator's credentials to approve this one action."
            });

            if (prompt.Status == CredentialPromptStatus.Submitted)
            {
                AdminElevationVerificationResult verification = await ElevationVerifier.VerifyAsync(
                    prompt.LoginName!, prompt.Password!);
                if (verification.Success)
                {
                    AppContext.OneShotElevation.Grant(e.Error.RequiredCapability, e.Error.Path);
                    await MessageBox(
                        "Access Approved",
                        "An administrator approved this action once. Please try the action again.",
                        MessageBoxType.Ok);
                    return;
                }
            }
        }

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