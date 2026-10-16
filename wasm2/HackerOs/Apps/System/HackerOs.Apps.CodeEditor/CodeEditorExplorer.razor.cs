using HackerOs.App.Abstractions;
using HackerOs.AppSdk.FileView;
using HackerOs.AppSdk.FileView.ContextMenu;
using HackerOs.AppSdk.FileView.Events;
using HackerOs.AppSdk.Icons;
using HackerOs.AppSdk.FileView.Icons;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace HackerOs.Apps.CodeEditor;

/// <summary>Code-behind of the Explorer view: selection, inline creation, context menu entries, rename tracking.</summary>
public partial class CodeEditorExplorer
{
    private FileView? _fileView;
    private ElementReference _createInput;
    private CreationKind? _creating;
    private string _createName = string.Empty;
    private VirtualPath _createDirectory;
    private string? _createError;
    private bool _focusCreateInput;
    private bool _committing;
    private FileView? _subscribedView;
    private IReadOnlyList<IFileViewContextMenuProvider> _menuProviders = [];

    private enum CreationKind
    {
        File,
        Folder
    }

    /// <summary>Gets or sets the open folder, or null when none is open.</summary>
    [Parameter]
    public CodeEditorWorkspace? Workspace { get; set; }

    /// <summary>Gets or sets the watch gateway, so the tree follows changes made elsewhere (terminal, other apps).</summary>
    [Parameter]
    public IAppFileSystemWatchGateway? Watch { get; set; }

    /// <summary>Gets or sets the owning app's id, for FileView's drag-and-drop envelope.</summary>
    [Parameter]
    public string? OwnerAppId { get; set; }

    /// <summary>Gets or sets the callback that opens a file in a tab and focuses it (double click, Enter, after creating it).</summary>
    [Parameter]
    public EventCallback<VirtualPath> OnOpenFile { get; set; }

    /// <summary>
    /// Gets or sets the callback that shows a file without taking the focus (single click), so the
    /// tree keeps the keyboard (arrows, Delete, F2), as in VS Code.
    /// </summary>
    [Parameter]
    public EventCallback<VirtualPath> OnPreviewFile { get; set; }

    /// <summary>Gets or sets the callback that asks the window for its Open Folder dialog.</summary>
    [Parameter]
    public EventCallback OnOpenFolder { get; set; }

    /// <summary>Gets or sets the callback of the folder menu's "Find in Folder..." entry.</summary>
    [Parameter]
    public EventCallback<VirtualPath> OnFindInFolder { get; set; }

    /// <summary>Gets or sets the callback that opens the integrated terminal in a folder.</summary>
    [Parameter]
    public EventCallback<VirtualPath> OnOpenInTerminal { get; set; }

    /// <summary>Gets or sets the callback told that an entry was renamed (old path, new path), so open tabs follow it.</summary>
    [Parameter]
    public EventCallback<(VirtualPath OldPath, VirtualPath NewPath)> OnRenamed { get; set; }

    /// <summary>Gets or sets the delete handler (Delete key, menu): the window moves the items to the trash.</summary>
    [Parameter]
    public EventCallback<FileViewDeleteRequestedEventArgs> OnDeleteRequested { get; set; }

    /// <summary>Starts creating a file in the selected folder (the window's "New File" command when a folder is open).</summary>
    public void BeginNewFile() => BeginCreate(CreationKind.File, null);

    /// <summary>Re-reads the tree from the file system.</summary>
    public async Task RefreshAsync()
    {
        if (_fileView is not null)
        {
            await _fileView.RefreshAsync();
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _menuProviders =
        [
            new MenuProvider(FileViewContextMenuScope.Background, (context, items) =>
            {
                // Replaces FileView's "New Folder" (it creates "New Folder" then renames) with
                // VS Code's pair, the name typed first.
                items.Remove("new-folder");
                items.Add(Item("explorer-new-file", "New File...", "file-plus", () => BeginCreateAsync(CreationKind.File, Workspace?.Root)));
                items.Add(Item("explorer-new-folder", "New Folder...", "folder-plus", () => BeginCreateAsync(CreationKind.Folder, Workspace?.Root)));
            }),
            new MenuProvider(FileViewContextMenuScope.Directory, (context, items) =>
            {
                if (context.Item is not { } folder)
                {
                    return;
                }

                items.Add(Item("explorer-new-file", "New File...", "file-plus", () => BeginCreateAsync(CreationKind.File, folder.FullPath), separator: true));
                items.Add(Item("explorer-new-folder", "New Folder...", "folder-plus", () => BeginCreateAsync(CreationKind.Folder, folder.FullPath)));
                items.Add(Item("explorer-find-in-folder", "Find in Folder...", "search", () => OnFindInFolder.InvokeAsync(folder.FullPath)));
                items.Add(Item("explorer-open-in-terminal", "Open in Integrated Terminal", "terminal", () => OnOpenInTerminal.InvokeAsync(folder.FullPath)));
            })
        ];
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!ReferenceEquals(_subscribedView, _fileView))
        {
            Unsubscribe();
            if (_fileView is not null)
            {
                _fileView.SelectionChanged += OnSelectionChanged;
                _fileView.Opened += OnOpened;
                _fileView.Renamed += OnRenamedItem;
            }

            _subscribedView = _fileView;
        }

        if (_focusCreateInput && _creating is not null)
        {
            _focusCreateInput = false;
            try
            {
                await _createInput.FocusAsync();
            }
            catch (Exception)
            {
                // Not rendered (headless test DOM).
            }
        }
    }

    /// <summary>VS Code opens a file on a single click; folders just expand.</summary>
    private void OnSelectionChanged(object? sender, FileViewSelectionChangedEventArgs e)
    {
        if (e.Selection is [{ IsDirectory: false } file])
        {
            _ = InvokeAsync(() => OnPreviewFile.InvokeAsync(file.FullPath));
        }
    }

    private void OnOpened(object? sender, FileViewOpenedEventArgs e)
    {
        if (!e.Item.IsDirectory)
        {
            _ = InvokeAsync(() => OnOpenFile.InvokeAsync(e.Item.FullPath));
        }
    }

    private void OnRenamedItem(object? sender, FileViewRenamedEventArgs e)
    {
        VirtualPath newPath = e.Item.FullPath;
        VirtualPath oldPath = CodeEditorWorkspace.Combine(CodeEditorWorkspace.ParentOf(newPath), e.PreviousName);
        _ = InvokeAsync(() => OnRenamed.InvokeAsync((oldPath, newPath)));
    }

    private async Task OnTreeDeleteRequestedAsync(FileViewDeleteRequestedEventArgs e)
    {
        await OnDeleteRequested.InvokeAsync(e);
        await RefreshAsync();
    }

    private Task BeginCreateAsync(CreationKind kind, VirtualPath? directory)
    {
        BeginCreate(kind, directory);
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows the name box. The target is the given folder, else the selected folder, else the
    /// selected file's folder, else the root, as VS Code picks it.
    /// </summary>
    private void BeginCreate(CreationKind kind, VirtualPath? directory)
    {
        if (Workspace is null)
        {
            return;
        }

        VirtualPath target = directory ?? Workspace.Root;
        if (directory is null && _fileView?.SelectedItem is { } selected && Workspace.Contains(selected.FullPath))
        {
            target = selected.IsDirectory ? selected.FullPath : CodeEditorWorkspace.ParentOf(selected.FullPath);
        }

        _creating = kind;
        _createDirectory = target;
        _createName = string.Empty;
        _createError = null;
        _focusCreateInput = true;
    }

    private void OnCreateKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
        {
            CancelCreate();
        }
        else if (args.Key != "Enter")
        {
            _createError = null;
        }
    }

    /// <summary>
    /// Enter creates on key-up: a new file opens with the editor focused, and on key-down the rest
    /// of the keystroke would land in it as a new line.
    /// </summary>
    private async Task OnCreateKeyUpAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await CommitCreateAsync();
        }
    }

    /// <summary>Leaving the box creates the entry when a name is typed, as VS Code does; an empty box is dropped.</summary>
    private async Task OnCreateBlurAsync()
    {
        if (string.IsNullOrWhiteSpace(_createName))
        {
            CancelCreate();
            return;
        }

        await CommitCreateAsync();
    }

    private async Task CommitCreateAsync()
    {
        if (_creating is not { } kind || Workspace is null || _committing)
        {
            return;
        }

        if (CodeEditorWorkspace.ValidateName(_createName) is { } invalid)
        {
            _createError = invalid;
            return;
        }

        _committing = true;
        try
        {
            CodeEditorCreateResult result = kind == CreationKind.File
                ? await Workspace.CreateFileAsync(_createDirectory, _createName)
                : await Workspace.CreateDirectoryAsync(_createDirectory, _createName);
            if (!result.Succeeded || result.Path is not { } created)
            {
                _createError = result.Error;
                return;
            }

            CancelCreate();
            await RefreshAsync();
            if (kind == CreationKind.File)
            {
                await OnOpenFile.InvokeAsync(created);
            }
        }
        finally
        {
            _committing = false;
        }
    }

    private void CancelCreate()
    {
        _creating = null;
        _createName = string.Empty;
        _createError = null;
    }

    private static FileViewMenuItem Item(string id, string label, string icon, Func<Task> onActivate, bool separator = false) =>
        new(id, label, ShellIconDescriptor.Vector(IconLibrary.Lucide, icon), onActivate, IsSeparatorBefore: separator);

    private void Unsubscribe()
    {
        if (_subscribedView is not null)
        {
            _subscribedView.SelectionChanged -= OnSelectionChanged;
            _subscribedView.Opened -= OnOpened;
            _subscribedView.Renamed -= OnRenamedItem;
            _subscribedView = null;
        }
    }

    /// <inheritdoc />
    public void Dispose() => Unsubscribe();

    /// <summary>A context-menu customization given as a delegate.</summary>
    private sealed class MenuProvider(
        FileViewContextMenuScope scope,
        Action<FileViewContextMenuContext, FileViewMenuItemCollection> customize) : IFileViewContextMenuProvider
    {
        public FileViewContextMenuScope Scope => scope;

        public bool Matches(FileViewItem item) => true;

        public void Customize(FileViewContextMenuContext context, FileViewMenuItemCollection items) => customize(context, items);
    }
}
