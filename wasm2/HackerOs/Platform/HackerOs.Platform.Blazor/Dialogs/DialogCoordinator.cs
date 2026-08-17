using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Blazor;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.Blazor.Dialogs;

/// <summary>Describes a queued basic dialog presentation.</summary>
public abstract record DialogPresentation(
    Guid Id,
    string AppId,
    Guid AppInstanceId,
    ProcessId ProcessId,
    string UserId);

/// <summary>Describes an active message-box presentation.</summary>
public sealed record MessageBoxPresentation(
    Guid Id,
    string AppId,
    Guid AppInstanceId,
    ProcessId ProcessId,
    string UserId,
    MessageBoxDialogRequest Request)
    : DialogPresentation(Id, AppId, AppInstanceId, ProcessId, UserId);

/// <summary>Describes an active text-input presentation.</summary>
public sealed record TextInputPresentation(
    Guid Id,
    string AppId,
    Guid AppInstanceId,
    ProcessId ProcessId,
    string UserId,
    TextInputDialogRequest Request)
    : DialogPresentation(Id, AppId, AppInstanceId, ProcessId, UserId);

/// <summary>
/// Coordinates basic modal dialogs (MessageBox, TextInput) for applications,
/// delegating file-system dialogs to <see cref="IFileDialogService"/>.
/// </summary>
public sealed class DialogCoordinator : IDialogService, IDisposable
{
    private readonly Lock _gate = new();
    private readonly LinkedList<PendingDialog> _pending = [];
    private readonly IFileDialogService _fileDialogs;
    private bool _disposed;

    /// <summary>Creates a basic dialog coordinator over an injected file dialog service.</summary>
    public DialogCoordinator(IFileDialogService fileDialogs)
    {
        _fileDialogs = fileDialogs ?? throw new ArgumentNullException(nameof(fileDialogs));
    }

    /// <summary>Raised whenever the active basic presentation changes.</summary>
    public event Action? Changed;

    /// <summary>Gets the basic dialog request currently eligible for rendering.</summary>
    public DialogPresentation? ActiveRequest
    {
        get
        {
            lock (_gate)
            {
                return _pending.First?.Value.Presentation;
            }
        }
    }

    /// <summary>Gets the oldest still-pending request for one owning app instance, independent of other owners.</summary>
    public DialogPresentation? ActiveRequestFor(Guid appInstanceId)
    {
        lock (_gate)
        {
            for (LinkedListNode<PendingDialog>? node = _pending.First; node is not null; node = node.Next)
            {
                if (node.Value.Presentation.AppInstanceId == appInstanceId)
                {
                    return node.Value.Presentation;
                }
            }

            return null;
        }
    }

    /// <inheritdoc />
    public ValueTask<OpenFileDialogResult> OpenFileAsync(
        IAppExecutionContext context,
        OpenFileDialogRequest request,
        CancellationToken cancellationToken = default) =>
        _fileDialogs.OpenFileAsync(context, request, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SaveFileDialogResult> SaveFileAsync(
        IAppExecutionContext context,
        SaveFileDialogRequest request,
        CancellationToken cancellationToken = default) =>
        _fileDialogs.SaveFileAsync(context, request, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SelectFolderDialogResult> SelectFolderAsync(
        IAppExecutionContext context,
        SelectFolderDialogRequest request,
        CancellationToken cancellationToken = default) =>
        _fileDialogs.SelectFolderAsync(context, request, cancellationToken);

    /// <inheritdoc />
    public ValueTask<MessageBoxDialogResult> MessageBoxAsync(
        IAppExecutionContext context,
        MessageBoxDialogRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        Guid id = Guid.NewGuid();
        return Enqueue<MessageBoxDialogResult>(
            new MessageBoxPresentation(
                id, context.Manifest.Id, context.InstanceId, context.ProcessId, context.UserId, request),
            static () => new MessageBoxDialogResult(MessageBoxResult.Cancel),
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<TextInputDialogResult> TextInputAsync(
        IAppExecutionContext context,
        TextInputDialogRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        Guid id = Guid.NewGuid();
        return Enqueue<TextInputDialogResult>(
            new TextInputPresentation(
                id, context.Manifest.Id, context.InstanceId, context.ProcessId, context.UserId, request),
            static () => new TextInputDialogResult(TextInputStatus.Cancelled, null),
            cancellationToken);
    }

    /// <summary>Completes the active message-box request with the clicked button result.</summary>
    public bool SelectMessageBox(Guid requestId, MessageBoxResult result)
    {
        if (FindActive(requestId) is not MessageBoxPresentation)
        {
            return false;
        }

        return Complete(requestId, new MessageBoxDialogResult(result));
    }

    /// <summary>Completes the active text-input request with the submitted value.</summary>
    public bool SelectTextInput(Guid requestId, string? value)
    {
        if (FindActive(requestId) is not TextInputPresentation)
        {
            return false;
        }

        return Complete(requestId, new TextInputDialogResult(TextInputStatus.Submitted, value));
    }

    /// <summary>Cancels an active or queued basic dialog request.</summary>
    public bool Cancel(Guid requestId) => Complete(requestId, null);

    /// <inheritdoc />
    public void Dispose()
    {
        List<PendingDialog> cancelled;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            cancelled = [.. _pending];
            _pending.Clear();
        }

        foreach (PendingDialog pending in cancelled)
        {
            pending.Cancel();
            pending.Dispose();
        }

        Changed?.Invoke();
    }

    private ValueTask<TResult> Enqueue<TResult>(
        DialogPresentation presentation,
        Func<TResult> cancelledResult,
        CancellationToken cancellationToken)
    {
        PendingDialog<TResult> pending = new(presentation, cancelledResult);
        bool activeChanged;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            activeChanged = !_pending.Any(queued =>
                queued.Presentation.ProcessId == presentation.ProcessId
                && queued.Presentation.AppInstanceId == presentation.AppInstanceId);
            pending.Node = _pending.AddLast(pending);
            pending.CancellationRegistration = cancellationToken.Register(
                static state => ((CancellationState)state!).Coordinator.Cancel(((CancellationState)state).RequestId),
                new CancellationState(this, presentation.Id));
        }

        if (activeChanged)
        {
            Changed?.Invoke();
        }

        return new ValueTask<TResult>(pending.Task);
    }

    private bool Complete(Guid requestId, object? selectedResult)
    {
        PendingDialog? pending;
        bool activeChanged;
        lock (_gate)
        {
            LinkedListNode<PendingDialog>? node = _pending.First;
            while (node is not null && node.Value.Presentation.Id != requestId)
            {
                node = node.Next;
            }

            if (node is null)
            {
                return false;
            }

            pending = node.Value;
            activeChanged = IsOwnerHead(node);
            _pending.Remove(node);
        }

        bool completed = selectedResult is null
            ? pending.Cancel()
            : pending.TryComplete(selectedResult);
        pending.Dispose();
        if (activeChanged)
        {
            Changed?.Invoke();
        }

        return completed;
    }

    private DialogPresentation? FindActive(Guid requestId)
    {
        lock (_gate)
        {
            DialogPresentation? active = _pending.First?.Value.Presentation;
            return active?.Id == requestId ? active : null;
        }
    }

    /// <summary>Gets whether no earlier queued item shares this node's owning app instance.</summary>
    private static bool IsOwnerHead(LinkedListNode<PendingDialog> node)
    {
        for (LinkedListNode<PendingDialog>? scan = node.List!.First; scan != node; scan = scan!.Next)
        {
            if (scan!.Value.Presentation.ProcessId == node.Value.Presentation.ProcessId
                && scan.Value.Presentation.AppInstanceId == node.Value.Presentation.AppInstanceId)
            {
                return false;
            }
        }

        return true;
    }

    private abstract class PendingDialog(DialogPresentation presentation) : IDisposable
    {
        public DialogPresentation Presentation { get; } = presentation;
        public LinkedListNode<PendingDialog>? Node { get; set; }
        public CancellationTokenRegistration CancellationRegistration { get; set; }
        public abstract bool Cancel();
        public abstract bool TryComplete(object result);
        public void Dispose() => CancellationRegistration.Dispose();
    }

    private sealed class PendingDialog<TResult>(
        DialogPresentation presentation,
        Func<TResult> cancelledResult) : PendingDialog(presentation)
    {
        private readonly TaskCompletionSource<TResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<TResult> Task => _completion.Task;
        public override bool Cancel() => _completion.TrySetResult(cancelledResult());
        public override bool TryComplete(object result) =>
            result is TResult typed && _completion.TrySetResult(typed);
    }

    private sealed record CancellationState(DialogCoordinator Coordinator, Guid RequestId);
}
