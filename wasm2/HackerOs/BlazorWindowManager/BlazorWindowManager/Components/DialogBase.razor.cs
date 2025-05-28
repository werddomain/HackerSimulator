using BlazorWindowManager.Models;
using BlazorWindowManager.Services;
using Microsoft.AspNetCore.Components;

namespace BlazorWindowManager.Components;

/// <summary>
/// Base class for dialog components that provides modal dialog functionality
/// </summary>
/// <typeparam name="TResult">The type of result returned by the dialog</typeparam>
public partial class DialogBase<TResult> : WindowBase, IDialogBase
{    /// <summary>
    /// Content to display inside the dialog
    /// </summary>
    [Parameter] public new RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Whether this dialog is modal (blocks interaction with parent/other windows)
    /// </summary>
    [Parameter] public new bool IsModal { get; set; } = true;

    /// <summary>
    /// Whether clicking the modal overlay should close the dialog
    /// </summary>
    [Parameter] public bool CloseOnOverlayClick { get; set; } = false;

    /// <summary>
    /// The parent window that owns this dialog (null for application-modal)
    /// </summary>
    [Parameter] public WindowBase? OwnerWindow { get; set; }

    /// <summary>
    /// Event raised when the dialog is about to close, provides the result
    /// </summary>
    [Parameter] public EventCallback<DialogResult<TResult>> OnDialogClosing { get; set; }

    /// <summary>
    /// Event raised after the dialog has closed
    /// </summary>
    [Parameter] public EventCallback<DialogResult<TResult>> OnDialogClosed { get; set; }

    private TaskCompletionSource<DialogResult<TResult>>? _dialogCompletionSource;
    private bool _isVisible = false;
    protected DialogResult<TResult>? _result;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Dialogs should not be resizable by default
        Resizable = false;

        // Set initial state
        _isVisible = true;

        // Subscribe to parent window events if we have an owner
        if (OwnerWindow != null)
        {
            OwnerWindow.OnBeforeClose = EventCallback.Factory.Create<WindowCancelEventArgs>(this, OnOwnerWindowClosing);
            OwnerWindow.OnAfterClose = EventCallback.Factory.Create(this, OnOwnerWindowClosed);
        }

        // Override the close button behavior
        ShowCloseButton = true;
    }

    /// <summary>
    /// Shows the dialog and returns a task that completes when the dialog is closed
    /// </summary>
    /// <returns>A task that resolves to the dialog result</returns>
    public Task<DialogResult<TResult>> ShowDialogAsync()
    {
        if (_dialogCompletionSource != null)
        {
            throw new InvalidOperationException("Dialog is already showing");
        }

        _dialogCompletionSource = new TaskCompletionSource<DialogResult<TResult>>();
        _isVisible = true;

        StateHasChanged();
        return _dialogCompletionSource.Task;
    }

    /// <summary>
    /// Closes the dialog with the specified result
    /// </summary>
    /// <param name="result">The dialog result</param>
    public async Task CloseDialogAsync(DialogResult<TResult> result)
    {
        if (!_isVisible) return;

        _result = result;

        // Raise the closing event
        if (OnDialogClosing.HasDelegate)
        {
            await OnDialogClosing.InvokeAsync(result);
        }

        // Actually close the dialog
        await Close(force: true);
    }

    /// <summary>
    /// Closes the dialog with a successful result
    /// </summary>
    /// <param name="data">The result data</param>
    /// <param name="closeReason">The reason for closing</param>
    public async Task CloseWithResultAsync(TResult data, string closeReason = "OK")
    {
        await CloseDialogAsync(DialogResult<TResult>.Ok(data, closeReason));
    }

    /// <summary>
    /// Closes the dialog as cancelled
    /// </summary>
    /// <param name="closeReason">The reason for cancelling</param>
    public async Task CancelDialogAsync(string closeReason = "Cancel")
    {
        await CloseDialogAsync(DialogResult<TResult>.Cancel(closeReason));
    }

    /// <summary>
    /// Closes the dialog with an error
    /// </summary>
    /// <param name="errorMessage">The error message</param>
    /// <param name="closeReason">The reason for closing</param>
    public async Task CloseWithErrorAsync(string errorMessage, string closeReason = "Error")
    {
        await CloseDialogAsync(DialogResult<TResult>.Error(errorMessage, closeReason));
    }

    protected override async Task<bool> OnBeforeCloseAsync()
    {
        // If we don't have a result yet, treat as cancelled
        if (_result == null)
        {
            _result = DialogResult<TResult>.Cancel();
        }

        return await Task.FromResult(true); // Allow close to proceed
    }

    protected override async Task OnAfterCloseAsync()
    {
        _isVisible = false;

        // Complete the dialog task
        var result = _result ?? DialogResult<TResult>.Cancel();
        _dialogCompletionSource?.SetResult(result);
        _dialogCompletionSource = null;

        // Raise the closed event
        if (OnDialogClosed.HasDelegate)
        {
            await OnDialogClosed.InvokeAsync(result);
        }

        // Unsubscribe from owner events - events are auto-cleaned when component disposes
        await Task.CompletedTask;
    }

    private async Task OnOverlayClick()
    {
        if (CloseOnOverlayClick)
        {
            await CancelDialogAsync("OverlayClick");
        }
    }

    private async Task OnOwnerWindowClosing(WindowCancelEventArgs e)
    {
        if (!e.Force)
        {
            // Don't allow owner to close while dialog is open unless forced
            e.Cancel = true;
        }
        else
        {
            // Owner is being force-closed, close this dialog too
            await CloseDialogAsync(DialogResult<TResult>.Cancel("OwnerClosed"));
        }
    }

    private async Task OnOwnerWindowClosed()
    {
        // Owner was closed, close this dialog
        await CloseDialogAsync(DialogResult<TResult>.Cancel("OwnerClosed"));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // If dialog is still open, cancel it
            if (_dialogCompletionSource != null && !_dialogCompletionSource.Task.IsCompleted)
            {
                _dialogCompletionSource.SetResult(DialogResult<TResult>.Cancel("Disposed"));
            }
        }

        base.Dispose(disposing);
    }
}
