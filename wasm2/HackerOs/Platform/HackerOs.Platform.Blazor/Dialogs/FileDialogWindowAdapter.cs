using HackerOs.Platform.Blazor.Windows;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.Blazor.Dialogs;

/// <summary>Projects the active session dialog into one authoritative owner-modal window.</summary>
public sealed class FileDialogWindowAdapter : IDisposable
{
    private readonly FileDialogCoordinator _coordinator;
    private readonly WindowRuntime _windows;
    private WindowId? _dialogWindowId;
    private bool _disposed;

    /// <summary>Creates an adapter over one session coordinator and its window runtime.</summary>
    public FileDialogWindowAdapter(FileDialogCoordinator coordinator, WindowRuntime windows)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _coordinator.Changed += Synchronize;
        Synchronize();
    }

    /// <summary>Raised after the adapter changes the window runtime.</summary>
    public event Action? Changed;

    /// <summary>Gets the presentation currently projected into a modal window.</summary>
    public FileDialogPresentation? ActivePresentation => _dialogWindowId is null
        ? null
        : _coordinator.ActiveRequest;

    /// <summary>Gets whether a window is the active platform file dialog.</summary>
    public bool IsDialogWindow(WindowId windowId) => _dialogWindowId == windowId;

    /// <summary>Cancels a close request for the active dialog as an ordinary user outcome.</summary>
    public bool Cancel(WindowId windowId) =>
        _dialogWindowId == windowId
        && _coordinator.ActiveRequest is { } presentation
        && _coordinator.Cancel(presentation.Id);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _coordinator.Changed -= Synchronize;
    }

    private void Synchronize()
    {
        if (_disposed)
        {
            return;
        }

        bool changed = CloseProjectedWindow();
        if (_coordinator.ActiveRequest is FileDialogPresentation presentation)
        {
            WindowRuntimeState? owner = _windows.Windows.SingleOrDefault(window =>
                window.ProcessId == presentation.ProcessId
                && window.AppInstanceId == AppInstanceId.FromGuid(presentation.AppInstanceId));
            if (owner is null)
            {
                _coordinator.Cancel(presentation.Id);
                return;
            }

            WindowId dialogId = WindowId.FromGuid(presentation.Id);
            WindowRuntimeState state = new(
                dialogId,
                presentation.AppId,
                presentation.ProcessId,
                owner.AppInstanceId,
                Title(presentation),
                iconAssetPath: null,
                new WindowBounds(owner.Bounds.X + 40, owner.Bounds.Y + 40, 720, 560),
                restoreBounds: null,
                zOrder: 0,
                WindowVisualState.Normal,
                new WindowConstraints(isResizable: false, minWidth: 320, minHeight: 360),
                WindowModality.OwnerModal,
                owner.Id);
            _windows.Apply(new CreateWindowCommand(state));
            _dialogWindowId = dialogId;
            changed = true;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private bool CloseProjectedWindow()
    {
        if (_dialogWindowId is not WindowId dialogId)
        {
            return false;
        }

        _dialogWindowId = null;
        if (_windows.Windows.Any(window => window.Id == dialogId))
        {
            _windows.Apply(new ForceWindowCloseCommand(dialogId));
        }

        return true;
    }

    private static string Title(FileDialogPresentation presentation) => presentation switch
    {
        OpenFileDialogPresentation open => open.Request.Title ?? "Open file",
        SaveFileDialogPresentation save => save.Request.Title ?? "Save file",
        SelectFolderDialogPresentation folder => folder.Request.Title ?? "Select folder",
        _ => "File dialog",
    };
}