using HackerOs.Platform.Blazor.Windows;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.Blazor.Dialogs;

/// <summary>Projects active platform basic and file dialogs into owner-modal windows.</summary>
public sealed class FileDialogWindowAdapter : IDisposable
{
    private readonly FileDialogCoordinator _fileCoordinator;
    private readonly DialogCoordinator? _basicCoordinator;
    private readonly WindowRuntime _windows;
    private WindowId? _dialogWindowId;
    private bool _disposed;

    /// <summary>Creates an adapter over session dialog coordinators and their window runtime.</summary>
    public FileDialogWindowAdapter(
        FileDialogCoordinator fileCoordinator,
        WindowRuntime windows,
        DialogCoordinator? basicCoordinator = null)
    {
        _fileCoordinator = fileCoordinator ?? throw new ArgumentNullException(nameof(fileCoordinator));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _basicCoordinator = basicCoordinator;

        _fileCoordinator.Changed += Synchronize;
        if (_basicCoordinator is not null)
        {
            _basicCoordinator.Changed += Synchronize;
        }

        Synchronize();
    }

    /// <summary>Raised after the adapter changes the window runtime.</summary>
    public event Action? Changed;

    /// <summary>Gets the file presentation currently projected into a modal window.</summary>
    public FileDialogPresentation? ActivePresentation => _dialogWindowId is null
        ? null
        : _fileCoordinator.ActiveRequest;

    /// <summary>Gets the basic presentation currently projected into a modal window.</summary>
    public DialogPresentation? ActiveBasicPresentation => _dialogWindowId is null
        ? null
        : _basicCoordinator?.ActiveRequest;

    /// <summary>Gets whether a window is the active platform file or basic dialog.</summary>
    public bool IsDialogWindow(WindowId windowId) => _dialogWindowId == windowId;

    /// <summary>Cancels a close request for the active dialog as an ordinary user outcome.</summary>
    public bool Cancel(WindowId windowId)
    {
        if (_dialogWindowId != windowId)
        {
            return false;
        }

        if (_fileCoordinator.ActiveRequest is { } filePresentation)
        {
            return _fileCoordinator.Cancel(filePresentation.Id);
        }

        if (_basicCoordinator?.ActiveRequest is { } basicPresentation)
        {
            return _basicCoordinator.Cancel(basicPresentation.Id);
        }

        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _fileCoordinator.Changed -= Synchronize;
        if (_basicCoordinator is not null)
        {
            _basicCoordinator.Changed -= Synchronize;
        }
    }

    private void Synchronize()
    {
        if (_disposed)
        {
            return;
        }

        bool changed = CloseProjectedWindow();

        if (_fileCoordinator.ActiveRequest is FileDialogPresentation filePresentation)
        {
            WindowRuntimeState? owner = FindOwner(filePresentation.ProcessId, filePresentation.AppInstanceId);
            if (owner is null)
            {
                _fileCoordinator.Cancel(filePresentation.Id);
                return;
            }

            CreateWindow(filePresentation.Id, filePresentation.AppId, filePresentation.ProcessId, owner, FileTitle(filePresentation), (720, 560));
            changed = true;
        }
        else if (_basicCoordinator?.ActiveRequest is DialogPresentation basicPresentation)
        {
            WindowRuntimeState? owner = FindOwner(basicPresentation.ProcessId, basicPresentation.AppInstanceId);
            if (owner is null)
            {
                _basicCoordinator.Cancel(basicPresentation.Id);
                return;
            }

            (int width, int height) dimensions = basicPresentation switch
            {
                MessageBoxPresentation => (460, 220),
                TextInputPresentation => (480, 240),
                _ => (480, 240)
            };

            CreateWindow(basicPresentation.Id, basicPresentation.AppId, basicPresentation.ProcessId, owner, BasicTitle(basicPresentation), dimensions);
            changed = true;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private WindowRuntimeState? FindOwner(ProcessId processId, Guid appInstanceIdGuid) =>
        _windows.Windows.SingleOrDefault(window =>
            window.ProcessId == processId
            && window.AppInstanceId == AppInstanceId.FromGuid(appInstanceIdGuid));

    private void CreateWindow(
        Guid requestId,
        string appId,
        ProcessId processId,
        WindowRuntimeState owner,
        string title,
        (int width, int height) dimensions)
    {
        WindowId dialogId = WindowId.FromGuid(requestId);
        WindowRuntimeState state = new(
            dialogId,
            appId,
            processId,
            owner.AppInstanceId,
            title,
            iconAssetPath: null,
            new WindowBounds(owner.Bounds.X + 40, owner.Bounds.Y + 40, dimensions.width, dimensions.height),
            restoreBounds: null,
            zOrder: 0,
            WindowVisualState.Normal,
            new WindowConstraints(isResizable: false, minWidth: 320, minHeight: 180),
            WindowModality.OwnerModal,
            owner.Id);
        _windows.Apply(new CreateWindowCommand(state));
        _dialogWindowId = dialogId;
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

    private static string FileTitle(FileDialogPresentation presentation) => presentation switch
    {
        OpenFileDialogPresentation open => open.Request.Title ?? "Open file",
        SaveFileDialogPresentation save => save.Request.Title ?? "Save file",
        SelectFolderDialogPresentation folder => folder.Request.Title ?? "Select folder",
        _ => "File dialog",
    };

    private static string BasicTitle(DialogPresentation presentation) => presentation switch
    {
        MessageBoxPresentation msgBox => !string.IsNullOrWhiteSpace(msgBox.Request.Title) ? msgBox.Request.Title : "Message",
        TextInputPresentation input => !string.IsNullOrWhiteSpace(input.Request.Title) ? input.Request.Title : "Input",
        _ => "Dialog",
    };
}