using System.Text.Json;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk.DragDrop;
using HackerOs.AppSdk.FileView.ContextMenu;
using HackerOs.AppSdk.FileView.Events;
using HackerOs.AppSdk.FileView.Icons;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace HackerOs.AppSdk.FileView;

/// <summary>
/// Shared backing for <see cref="FileView"/> — navigation, selection, inline rename, folder activation,
/// live-watch wiring, and control-initiated create/delete. Markup lives in <c>FileView.razor</c>; view-mode
/// renderers (<c>FileViewDetails</c>, ...) call back into the <c>internal</c> members here. See
/// <c>docs/Global-FileView-And-MessagingSystem/FileViewControl.md</c>.
/// </summary>
public partial class FileView : IAsyncDisposable
{
    /// <summary>The custom <c>DataTransfer</c> MIME type <see cref="FileView.razor.js"/> reads/writes drag payloads under.</summary>
    internal const string DragMimeType = "application/vnd.hackeros.file-drag+json";

    private readonly List<FileViewItem> _items = [];
    private readonly HashSet<FileViewItem> _selection = [];
    private bool _initialized;
    private long _currentDirectoryRevision;
    private ITopicChannelSubscription<FileSystemChangeEvent>? _watchSubscription;
    private CancellationTokenSource? _watchLoopCts;
    private FileViewContextMenu? _contextMenu;
    private IJSObjectReference? _jsModule;
    private ElementReference _rootElement;
    private FileSystemDirectorySnapshot? _lastSnapshot;

    /// <summary>Scoped filesystem access supplied by the host app. Required.</summary>
    [Parameter, EditorRequired]
    public IAppFileSystemGateway FileSystem { get; set; } = null!;

    /// <summary>
    /// Optional scoped directory-watch access. When supplied, live change notifications replace/augment
    /// manual <see cref="RefreshAsync"/> calls.
    /// </summary>
    [Parameter]
    public IAppFileSystemWatchGateway? Watch { get; set; }

    /// <summary>Optional scoped intent access, required only when <see cref="FolderActivation"/> is <see cref="FileViewFolderActivationMode.NewWindow"/>.</summary>
    [Parameter]
    public IAppIntentGateway? Intents { get; set; }

    /// <summary>The directory shown on first render.</summary>
    [Parameter, EditorRequired]
    public VirtualPath InitialDirectory { get; set; }

    /// <summary>The active view mode. Two-way bindable.</summary>
    [Parameter]
    public FileViewMode Mode { get; set; } = FileViewMode.Details;

    /// <summary>Callback invoked when <see cref="Mode"/> changes via <c>@bind-Mode</c>.</summary>
    [Parameter]
    public EventCallback<FileViewMode> ModeChanged { get; set; }

    /// <summary>Controls what a directory item's activation (double-click/Enter) does.</summary>
    [Parameter]
    public FileViewFolderActivationMode FolderActivation { get; set; } = FileViewFolderActivationMode.Navigate;

    /// <summary>Required when <see cref="FolderActivation"/> is <see cref="FileViewFolderActivationMode.Custom"/>.</summary>
    [Parameter]
    public Func<FileViewItem, Task>? OnCustomFolderActivate { get; set; }

    /// <summary>Overrides the DI-registered default <see cref="Icons.IShellIconProvider"/> for this instance only.</summary>
    [Parameter]
    public IShellIconProvider? IconProvider { get; set; }

    /// <summary>Injected default icon provider, used when <see cref="IconProvider"/> is not supplied.</summary>
    [Inject]
    private IShellIconProvider? DefaultIconProvider { get; set; }

    /// <summary>
    /// Injected in production; <c>internal</c> (rather than <c>private</c>) so tests can assign a fake
    /// directly, the same way they set <see cref="FileSystem"/> without going through Blazor's parameter
    /// pipeline — this component is never instantiated via DI in this solution's test harness (no bUnit).
    /// </summary>
    [Inject]
    internal IJSRuntime JavaScript { get; set; } = null!;

    /// <summary>Ordered context-menu customizers; see <see cref="IFileViewContextMenuProvider"/>.</summary>
    [Parameter]
    public IReadOnlyList<IFileViewContextMenuProvider> ContextMenuProviders { get; set; } = [];

    /// <summary>Whether more than one item may be selected at once. Defaults to <see langword="true"/>.</summary>
    [Parameter]
    public bool AllowMultiSelect { get; set; } = true;

    /// <summary>Whether drag & drop is enabled. Defaults to <see langword="true"/>.</summary>
    [Parameter]
    public bool AllowDragDrop { get; set; } = true;

    /// <summary>
    /// This instance's app id, carried as <c>SourceAppId</c> on outgoing drag payloads (e.g.
    /// <c>context.Manifest.Id</c>). Purely informational — a drop never needs to know or trust the
    /// source app, so an unset value does not disable drag-drop.
    /// </summary>
    [Parameter]
    public string? OwnerAppId { get; set; }

    /// <summary>Overrides the default Details-mode column set.</summary>
    [Parameter]
    public IReadOnlyList<FileViewColumn>? Columns { get; set; }

    /// <summary>
    /// Optional predicate limiting which entries appear in <see cref="Items"/>, across all three modes —
    /// e.g. a host's search box. Evaluated fresh whenever <see cref="Items"/> is (re)built; call
    /// <see cref="RefreshFilter"/> after changing what this delegate would return to re-apply it against
    /// the most recently fetched listing without a filesystem round-trip. A filtered-out item behaves as
    /// though it doesn't exist: not rendered, not selectable, not counted.
    /// </summary>
    [Parameter]
    public Func<FileViewItem, bool>? Filter { get; set; }

    /// <summary>Gets the directory currently displayed.</summary>
    public VirtualPath CurrentDirectory { get; private set; }

    /// <summary>Gets the current directory's items.</summary>
    public IReadOnlyList<FileViewItem> Items { get; private set; } = [];

    /// <summary>Gets the primary selected item, or <see langword="null"/> when nothing is selected.</summary>
    public FileViewItem? SelectedItem { get; private set; }

    /// <summary>Gets every currently selected item.</summary>
    public IReadOnlyList<FileViewItem> SelectedItems { get; private set; } = [];

    /// <summary>Gets the effective icon provider for this instance: <see cref="IconProvider"/> if set, else the injected default.</summary>
    internal IShellIconProvider EffectiveIconProvider =>
        IconProvider ?? DefaultIconProvider ?? throw new InvalidOperationException(
            "No IShellIconProvider is available: supply the IconProvider parameter or register a default in DI.");

    /// <summary>Raised before navigating to a new directory. Set <c>e.Cancel = true</c> to veto.</summary>
    public event EventHandler<FileViewNavigatingEventArgs>? Navigating;

    /// <summary>Raised after navigation completes.</summary>
    public event EventHandler<FileViewPathChangedEventArgs>? OnPathChange;

    /// <summary>Raised before an item is activated. Set <c>e.Cancel = true</c> to veto.</summary>
    public event EventHandler<FileViewOpeningEventArgs>? Opening;

    /// <summary>Raised after an item has been activated.</summary>
    public event EventHandler<FileViewOpenedEventArgs>? Opened;

    /// <summary>Raised before an inline rename commits. Set <c>e.Cancel = true</c> to veto.</summary>
    public event EventHandler<FileViewRenamingEventArgs>? Renaming;

    /// <summary>Raised after an inline rename commits.</summary>
    public event EventHandler<FileViewRenamedEventArgs>? Renamed;

    /// <summary>Raised before a control-initiated create proceeds. Set <c>e.Cancel = true</c> to veto.</summary>
    public event EventHandler<FileViewCreatingEventArgs>? Creating;

    /// <summary>Raised after a control-initiated create completes.</summary>
    public event EventHandler<FileViewCreatedEventArgs>? Created;

    /// <summary>Raised before a control-initiated delete proceeds. Set <c>e.Cancel = true</c> to veto.</summary>
    public event EventHandler<FileViewDeletingEventArgs>? Deleting;

    /// <summary>Raised after a control-initiated delete completes.</summary>
    public event EventHandler<FileViewDeletedEventArgs>? Deleted;

    /// <summary>Raised before a move (drag-drop or rename) proceeds. Set <c>e.Cancel = true</c> to veto.</summary>
    public event EventHandler<FileViewMovingEventArgs>? Moving;

    /// <summary>Raised after a move completes.</summary>
    public event EventHandler<FileViewMovedEventArgs>? Moved;

    /// <summary>Raised before a drag-drop copy proceeds. Set <c>e.Cancel = true</c> to veto.</summary>
    public event EventHandler<FileViewCopyingEventArgs>? Copying;

    /// <summary>Raised after a drag-drop copy completes.</summary>
    public event EventHandler<FileViewCopiedEventArgs>? Copied;

    /// <summary>Raised before a selection change takes effect. Set <c>e.Cancel = true</c> to veto.</summary>
    public event EventHandler<FileViewSelectionChangingEventArgs>? SelectionChanging;

    /// <summary>Raised after the selection changes.</summary>
    public event EventHandler<FileViewSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised when a context menu is about to open, after provider customization has run.</summary>
    public event EventHandler<FileViewContextMenuOpeningEventArgs>? ContextMenuOpening;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (!_initialized)
        {
            _initialized = true;
            await NavigateAsync(InitialDirectory);
        }
    }

    /// <summary>Navigates this instance to <paramref name="path"/>.</summary>
    public async Task NavigateAsync(VirtualPath path, CancellationToken cancellationToken = default)
    {
        FileViewNavigatingEventArgs navigatingArgs = new(CurrentDirectory, path);
        Navigating?.Invoke(this, navigatingArgs);
        if (navigatingArgs.Cancel)
        {
            return;
        }

        FileSystemResult<FileSystemDirectorySnapshot> result = await FileSystem.EnumerateAsync(
            new FileSystemEnumerateRequest(path), cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not enumerate '{path.Value}': {result.Error?.Code.ToString() ?? "unknown error"}.");
        }

        await AttachWatchAsync(path, cancellationToken);
        _lastSnapshot = result.Value;
        RebuildItems(result.Value!, preserveSelection: false);
        CurrentDirectory = path;
        StateHasChanged();
        OnPathChange?.Invoke(this, new FileViewPathChangedEventArgs(path));
    }

    /// <summary>Reloads the current directory's contents, preserving selection where possible (matched by name).</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        FileSystemResult<FileSystemDirectorySnapshot> result = await FileSystem.EnumerateAsync(
            new FileSystemEnumerateRequest(CurrentDirectory), cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not refresh '{CurrentDirectory.Value}': {result.Error?.Code.ToString() ?? "unknown error"}.");
        }

        _lastSnapshot = result.Value;
        RebuildItems(result.Value!, preserveSelection: true);
        StateHasChanged();
    }

    /// <summary>
    /// Re-applies <see cref="Filter"/> against the most recently fetched directory listing, without a
    /// filesystem round-trip — call after changing what the delegate would return (e.g. a search box's
    /// query changed) so <see cref="Items"/> reflects it immediately. A no-op before the first successful
    /// <see cref="NavigateAsync"/>/<see cref="RefreshAsync"/>.
    /// </summary>
    public void RefreshFilter()
    {
        if (_lastSnapshot is not null)
        {
            RebuildItems(_lastSnapshot, preserveSelection: true);
            StateHasChanged();
        }
    }

    /// <summary>Clears the current selection.</summary>
    public void ClearSelect() => ChangeSelection([]);

    /// <summary>Selects the item with the given file name, if present in the current directory.</summary>
    public void SelectByName(string fileName)
    {
        FileViewItem? match = _items.Find(item => string.Equals(item.FileName, fileName, StringComparison.Ordinal));
        if (match is not null)
        {
            ChangeSelection([match]);
        }
    }

    private void RebuildItems(FileSystemDirectorySnapshot snapshot, bool preserveSelection)
    {
        HashSet<string> selectedNames = preserveSelection
            ? new HashSet<string>(_selection.Select(item => item.FileName), StringComparer.Ordinal)
            : [];

        _items.Clear();
        _selection.Clear();
        _currentDirectoryRevision = snapshot.Revision;

        foreach (FileSystemDirectoryItem entry in snapshot.Entries)
        {
            VirtualPath itemPath = CombineChild(snapshot.Path, entry.Name.Value);
            FileViewItem item = new(itemPath, entry.Name.Value, entry.Metadata.Kind == FileSystemEntryKind.Directory, entry.Metadata)
            {
                Owner = this
            };
            if (Filter is not null && !Filter(item))
            {
                continue;
            }

            if (selectedNames.Contains(item.FileName))
            {
                item.IsSelected = true;
                _selection.Add(item);
            }

            _items.Add(item);
        }

        Items = _items.AsReadOnly();
        SelectedItems = [.. _selection];
        SelectedItem = _selection.FirstOrDefault();
    }

    private static VirtualPath CombineChild(VirtualPath parent, string name) =>
        VirtualPath.Parse(parent.Value == "/" ? $"/{name}" : $"{parent.Value}/{name}");

    internal void SelectItemInternal(FileViewItem item, bool additive)
    {
        IReadOnlyList<FileViewItem> proposed = additive && AllowMultiSelect
            ? [.. _selection.Where(existing => existing != item), item]
            : [item];
        ChangeSelection(proposed);
    }

    internal void DeselectItemInternal(FileViewItem item)
    {
        IReadOnlyList<FileViewItem> proposed = [.. _selection.Where(existing => existing != item)];
        ChangeSelection(proposed);
    }

    /// <summary>Replaces the selection with exactly the given items in one step (e.g. Icons mode's marquee select).</summary>
    internal void SetSelectionInternal(IReadOnlyList<FileViewItem> items) => ChangeSelection(items);

    /// <summary>
    /// Roving-tabindex arrow-key navigation (<c>FV-011</c>): selects <paramref name="item"/> (replacing the
    /// current selection, matching standard single-item arrow-key movement) and moves real DOM focus to it,
    /// so the browser's own Tab order stays correct — only the active item is ever a Tab stop; arrow keys
    /// move that active item without ever leaving the control.
    /// </summary>
    internal async Task MoveActiveItemAsync(FileViewItem item)
    {
        item.Select();
        await FocusItemAsync(item);
    }

    private async Task FocusItemAsync(FileViewItem item)
    {
        IJSObjectReference module = await EnsureJsModuleAsync();
        await module.InvokeAsync<bool>("focusItem", _rootElement, item.FullPath.Value);
    }

    private void ChangeSelection(IReadOnlyList<FileViewItem> proposed)
    {
        FileViewSelectionChangingEventArgs changingArgs = new(proposed);
        SelectionChanging?.Invoke(this, changingArgs);
        if (changingArgs.Cancel)
        {
            return;
        }

        foreach (FileViewItem item in _selection)
        {
            item.IsSelected = false;
        }

        _selection.Clear();
        foreach (FileViewItem item in proposed)
        {
            item.IsSelected = true;
            _selection.Add(item);
        }

        SelectedItems = [.. _selection];
        SelectedItem = _selection.FirstOrDefault();
        StateHasChanged();
        SelectionChanged?.Invoke(this, new FileViewSelectionChangedEventArgs(SelectedItems));
    }

    internal void NotifyItemRenamingStateChanged(FileViewItem item) => StateHasChanged();

    /// <summary>Cancels an in-progress inline rename without touching the filesystem.</summary>
    internal void CancelRename(FileViewItem item)
    {
        item.IsRenaming = false;
        StateHasChanged();
    }

    /// <summary>Commits an in-progress inline rename: routes through the same <c>Moving</c>/<c>Moved</c> path as a drag-drop move.</summary>
    internal async Task CommitRenameAsync(FileViewItem item, string proposedName)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.IsRenaming)
        {
            // Already committed/canceled by a prior call (e.g. Enter's keydown handler running before the
            // input's blur handler also fires) — idempotent no-op.
            return;
        }

        if (string.IsNullOrWhiteSpace(proposedName) || string.Equals(proposedName, item.FileName, StringComparison.Ordinal))
        {
            CancelRename(item);
            return;
        }

        FileViewRenamingEventArgs renamingArgs = new(item, proposedName);
        Renaming?.Invoke(this, renamingArgs);
        if (renamingArgs.Cancel)
        {
            CancelRename(item);
            return;
        }

        VirtualPath destination = CombineChild(CurrentDirectory, proposedName);
        FileViewMovingEventArgs movingArgs = new([item], destination);
        Moving?.Invoke(this, movingArgs);
        if (movingArgs.Cancel)
        {
            CancelRename(item);
            return;
        }

        FileSystemMutationResult result = await FileSystem.MoveAsync(
            new FileSystemMoveRequest(
                item.FullPath, destination, item.Metadata.Revision, _currentDirectoryRevision, _currentDirectoryRevision));

        item.IsRenaming = false;
        if (result.Transaction.Status != FileSystemTransactionStatus.Committed)
        {
            StateHasChanged();
            return;
        }

        string previousName = item.FileName;
        await RefreshAsync();
        FileViewItem? renamedItem = _items.Find(candidate => string.Equals(candidate.FileName, proposedName, StringComparison.Ordinal));
        if (renamedItem is not null)
        {
            Moved?.Invoke(this, new FileViewMovedEventArgs([renamedItem], CurrentDirectory));
            Renamed?.Invoke(this, new FileViewRenamedEventArgs(renamedItem, previousName));
        }
    }

    /// <summary>Activates one item (double-click, Enter, or the context menu's default action).</summary>
    internal async Task ActivateItemAsync(FileViewItem item)
    {
        FileViewOpeningEventArgs openingArgs = new(item, FolderActivation);
        Opening?.Invoke(this, openingArgs);
        if (openingArgs.Cancel)
        {
            return;
        }

        if (item.IsDirectory)
        {
            switch (FolderActivation)
            {
                case FileViewFolderActivationMode.Navigate:
                    await NavigateAsync(item.FullPath);
                    break;
                case FileViewFolderActivationMode.NewWindow:
                    if (Intents is null)
                    {
                        throw new InvalidOperationException(
                            "FolderActivation is NewWindow but no Intents gateway was supplied.");
                    }

                    await Intents.OpenFileAsync(item.FullPath, mediaType: "inode/directory");
                    break;
                case FileViewFolderActivationMode.Custom:
                    if (OnCustomFolderActivate is null)
                    {
                        throw new InvalidOperationException(
                            "FolderActivation is Custom but no OnCustomFolderActivate callback was supplied.");
                    }

                    await OnCustomFolderActivate(item);
                    break;
            }
        }

        Opened?.Invoke(this, new FileViewOpenedEventArgs(item));
    }

    /// <summary>Deletes every currently selected item via the control's own context menu/keyboard shortcut.</summary>
    internal async Task DeleteSelectedAsync()
    {
        if (_selection.Count == 0)
        {
            return;
        }

        FileViewItem[] targets = [.. _selection];
        FileViewDeletingEventArgs deletingArgs = new(targets);
        Deleting?.Invoke(this, deletingArgs);
        if (deletingArgs.Cancel)
        {
            return;
        }

        List<VirtualPath> deleted = [];
        foreach (FileViewItem item in targets)
        {
            FileSystemMutationResult result = await FileSystem.DeleteAsync(
                new FileSystemDeleteRequest(item.FullPath, item.Metadata.Revision, _currentDirectoryRevision));
            if (result.Transaction.Status == FileSystemTransactionStatus.Committed)
            {
                deleted.Add(item.FullPath);
            }
        }

        await RefreshAsync();
        if (deleted.Count > 0)
        {
            Deleted?.Invoke(this, new FileViewDeletedEventArgs(deleted));
        }
    }

    /// <summary>Copies the given items into <paramref name="destinationDirectory"/> (e.g. from drag-drop with a copy modifier held).</summary>
    internal async Task CopyItemsAsync(IReadOnlyList<FileViewItem> items, VirtualPath destinationDirectory)
    {
        if (items.Count == 0)
        {
            return;
        }

        FileViewCopyingEventArgs copyingArgs = new(items, destinationDirectory);
        Copying?.Invoke(this, copyingArgs);
        if (copyingArgs.Cancel)
        {
            return;
        }

        FileSystemResult<FileSystemEntrySnapshot> destinationStat = await FileSystem.StatAsync(
            new FileSystemStatRequest(destinationDirectory));
        if (!destinationStat.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not stat destination '{destinationDirectory.Value}': {destinationStat.Error?.Code.ToString() ?? "unknown error"}.");
        }

        long destinationRevision = destinationStat.Value!.Metadata.Revision;
        List<FileViewItem> succeeded = [];
        foreach (FileViewItem item in items)
        {
            VirtualPath destinationPath = CombineChild(destinationDirectory, item.FileName);
            FileSystemMutationResult result = await FileSystem.CopyAsync(
                new FileSystemCopyRequest(item.FullPath, destinationPath, item.Metadata.Revision, destinationRevision));
            if (result.Transaction.Status == FileSystemTransactionStatus.Committed)
            {
                succeeded.Add(item);
            }
        }

        if (Equals(destinationDirectory, CurrentDirectory))
        {
            await RefreshAsync();
        }

        if (succeeded.Count > 0)
        {
            Copied?.Invoke(this, new FileViewCopiedEventArgs(succeeded, destinationDirectory));
        }
    }

    /// <summary>
    /// Moves the given items into <paramref name="destinationDirectory"/> (drag-drop without a copy
    /// modifier held). Unlike <see cref="CopyItemsAsync"/>, a move mutates each item's source parent
    /// directory too, so — unlike every other mutation method here, which assumes its items came from
    /// <see cref="Items"/> (this instance's own current directory) — this one re-derives each item's own
    /// parent, because a drag-drop source item may belong to a different <see cref="FileView"/> instance
    /// (inter-control drag) with a directory this instance has never enumerated.
    /// </summary>
    internal async Task MoveItemsAsync(IReadOnlyList<FileViewItem> items, VirtualPath destinationDirectory)
    {
        if (items.Count == 0)
        {
            return;
        }

        FileViewMovingEventArgs movingArgs = new(items, destinationDirectory);
        Moving?.Invoke(this, movingArgs);
        if (movingArgs.Cancel)
        {
            return;
        }

        FileSystemResult<FileSystemEntrySnapshot> destinationStat = await FileSystem.StatAsync(
            new FileSystemStatRequest(destinationDirectory));
        if (!destinationStat.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not stat destination '{destinationDirectory.Value}': {destinationStat.Error?.Code.ToString() ?? "unknown error"}.");
        }

        long destinationRevision = destinationStat.Value!.Metadata.Revision;
        bool touchesCurrentDirectory = Equals(destinationDirectory, CurrentDirectory);
        List<FileViewItem> succeeded = [];
        foreach (FileViewItem item in items)
        {
            VirtualPath sourceParent = ParentOf(item.FullPath);
            FileSystemResult<FileSystemEntrySnapshot> sourceParentStat = await FileSystem.StatAsync(
                new FileSystemStatRequest(sourceParent));
            if (!sourceParentStat.Succeeded)
            {
                continue;
            }

            VirtualPath destinationPath = CombineChild(destinationDirectory, item.FileName);
            FileSystemMutationResult result = await FileSystem.MoveAsync(
                new FileSystemMoveRequest(
                    item.FullPath, destinationPath, item.Metadata.Revision, sourceParentStat.Value!.Metadata.Revision, destinationRevision));
            if (result.Transaction.Status == FileSystemTransactionStatus.Committed)
            {
                succeeded.Add(item);
                touchesCurrentDirectory |= Equals(sourceParent, CurrentDirectory);
            }
        }

        if (touchesCurrentDirectory)
        {
            await RefreshAsync();
        }

        if (succeeded.Count > 0)
        {
            Moved?.Invoke(this, new FileViewMovedEventArgs(succeeded, destinationDirectory));
        }
    }

    private static VirtualPath ParentOf(VirtualPath path)
    {
        string value = path.Value;
        int lastSlash = value.LastIndexOf('/');
        return VirtualPath.Parse(lastSlash <= 0 ? "/" : value[..lastSlash]);
    }

    /// <summary>
    /// Starts a native HTML5 drag for <paramref name="item"/> (drag-drop, <c>FV-006</c>): if <paramref name="item"/>
    /// isn't already selected it becomes the sole selection (matching common OS behavior — dragging an
    /// unselected row drags just that row), then the resulting selection is serialized onto the native
    /// <c>DataTransfer</c> via <see cref="FileView.razor.js"/>.
    /// </summary>
    internal async Task OnItemDragStartAsync(FileViewItem item, DragEventArgs args)
    {
        if (!AllowDragDrop)
        {
            return;
        }

        if (!item.IsSelected)
        {
            item.Select();
        }

        string? json = BuildDragPayloadJson(SelectedItems);
        if (json is null)
        {
            return;
        }

        IJSObjectReference module = await EnsureJsModuleAsync();
        await module.InvokeAsync<bool>("setDragData", DragMimeType, json);
    }

    /// <summary>
    /// Completes a drop onto <paramref name="target"/> (drag-drop, <c>FV-006</c>): ignored for non-directory
    /// targets and for drags that aren't a HackerOS-internal file drag (an OS-level file drop is out of
    /// scope — see <c>docs/Global-FileView-And-MessagingSystem/FileViewControl.md</c>). Ctrl held routes
    /// through the existing <see cref="CopyItemsAsync"/>; otherwise through <see cref="MoveItemsAsync"/> —
    /// both already raise the cancelable <see cref="Moving"/>/<see cref="Copying"/> events.
    /// </summary>
    internal async Task OnItemDropAsync(FileViewItem target, DragEventArgs args)
    {
        if (!AllowDragDrop || !target.IsDirectory)
        {
            return;
        }

        if (args.DataTransfer?.Types is not { } types || !types.Contains(DragMimeType))
        {
            return;
        }

        IJSObjectReference module = await EnsureJsModuleAsync();
        string? json = await module.InvokeAsync<string?>("getDragData", DragMimeType);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        FileViewDragEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize(json, FileViewDragEnvelopeJsonSerializerContext.Default.FileViewDragEnvelope);
        }
        catch (JsonException)
        {
            return;
        }

        if (envelope is null)
        {
            return;
        }

        List<FileViewItem> resolved = [];
        foreach (VirtualFileDragPayload file in envelope.Files)
        {
            await ResolveDraggedItemAsync(file.FilePath, file.FileName, isDirectory: false, target, resolved);
        }

        foreach (VirtualFolderDragPayload folder in envelope.Folders)
        {
            await ResolveDraggedItemAsync(folder.FolderPath, folder.FolderName, isDirectory: true, target, resolved);
        }

        if (resolved.Count == 0)
        {
            return;
        }

        if (args.CtrlKey)
        {
            await CopyItemsAsync(resolved, target.FullPath);
        }
        else
        {
            await MoveItemsAsync(resolved, target.FullPath);
        }
    }

    private async Task ResolveDraggedItemAsync(
        string pathValue, string name, bool isDirectory, FileViewItem target, List<FileViewItem> resolved)
    {
        VirtualPath path = VirtualPath.Parse(pathValue);
        if (Equals(path, target.FullPath))
        {
            // Dropping an item directly onto itself — a no-op, not an error.
            return;
        }

        FileSystemResult<FileSystemEntrySnapshot> stat = await FileSystem.StatAsync(new FileSystemStatRequest(path));
        if (stat.Succeeded)
        {
            resolved.Add(new FileViewItem(path, name, isDirectory, stat.Value!.Metadata) { Owner = this });
        }
    }

    private string? BuildDragPayloadJson(IReadOnlyList<FileViewItem> items)
    {
        if (items.Count == 0)
        {
            return null;
        }

        List<VirtualFileDragPayload> files = [];
        List<VirtualFolderDragPayload> folders = [];
        string sourceAppId = OwnerAppId ?? string.Empty;
        foreach (FileViewItem item in items)
        {
            if (item.IsDirectory)
            {
                // ItemCount is decorative only (drop-side logic never reads it) — not worth an extra
                // enumerate-per-dragged-folder round trip just to populate it accurately.
                folders.Add(new VirtualFolderDragPayload(sourceAppId, item.FullPath.Value, item.FileName, ItemCount: -1));
            }
            else
            {
                long size = item.Metadata is FileMetadata file ? file.Length : 0;
                string? mimeType = item.Metadata is FileMetadata fileMetadata ? fileMetadata.MediaType : null;
                files.Add(new VirtualFileDragPayload(sourceAppId, item.FullPath.Value, item.FileName, size, mimeType));
            }
        }

        return JsonSerializer.Serialize(
            new FileViewDragEnvelope(files, folders), FileViewDragEnvelopeJsonSerializerContext.Default.FileViewDragEnvelope);
    }

    private async Task<IJSObjectReference> EnsureJsModuleAsync() =>
        _jsModule ??= await JavaScript.InvokeAsync<IJSObjectReference>(
            "import", "./_content/HackerOs.AppSdk.FileView/FileView.razor.js");

    /// <summary>Creates a new empty folder in the current directory via the control's own context menu.</summary>
    internal async Task CreateFolderAsync(string name)
    {
        VirtualPath path = CombineChild(CurrentDirectory, name);
        FileViewCreatingEventArgs creatingArgs = new(CurrentDirectory, name, FileSystemEntryKind.Directory);
        Creating?.Invoke(this, creatingArgs);
        if (creatingArgs.Cancel)
        {
            return;
        }

        FileSystemMutationResult result = await FileSystem.CreateAsync(
            new FileSystemCreateRequest(
                path, FileSystemEntryKind.Directory, FileSystemPermissions.FromMode(0x01FF), _currentDirectoryRevision));
        if (result.Transaction.Status != FileSystemTransactionStatus.Committed)
        {
            return;
        }

        await RefreshAsync();
        FileViewItem? created = _items.Find(candidate => string.Equals(candidate.FileName, name, StringComparison.Ordinal));
        if (created is not null)
        {
            Created?.Invoke(this, new FileViewCreatedEventArgs(created));
        }
    }

    /// <summary>
    /// The default "New Folder" context-menu action: creates a uniquely named folder, then selects and
    /// enters inline rename on it — the same imperative sequence the control's own scripting surface uses
    /// (<c>ClearSelect()</c>/<c>Select()</c>/<c>Rename()</c>).
    /// </summary>
    internal async Task CreateFolderInteractiveAsync()
    {
        string name = "New Folder";
        int suffix = 2;
        while (_items.Any(item => string.Equals(item.FileName, name, StringComparison.Ordinal)))
        {
            name = $"New Folder ({suffix++})";
        }

        await CreateFolderAsync(name);
        ClearSelect();
        SelectByName(name);
        SelectedItem?.Rename();
    }

    internal void RaiseContextMenuOpening(FileViewContextMenuOpeningEventArgs args) => ContextMenuOpening?.Invoke(this, args);

    internal Task OpenItemContextMenuAsync(MouseEventArgs args, FileViewItem item) =>
        _contextMenu is null
            ? Task.CompletedTask
            : _contextMenu.OpenAsync(args, item.IsDirectory ? FileViewContextMenuScope.Directory : FileViewContextMenuScope.File, item);

    private async Task AttachWatchAsync(VirtualPath path, CancellationToken cancellationToken)
    {
        if (Watch is null)
        {
            return;
        }

        _watchLoopCts?.Cancel();
        _watchLoopCts?.Dispose();
        if (_watchSubscription is not null)
        {
            await _watchSubscription.DisposeAsync();
            _watchSubscription = null;
        }

        _watchSubscription = await Watch.WatchAsync(path, FileSystemWatchScope.ImmediateChildren, cancellationToken);
        _watchLoopCts = new CancellationTokenSource();
        _ = WatchLoopAsync(_watchSubscription, _watchLoopCts.Token);
    }

    private async Task WatchLoopAsync(ITopicChannelSubscription<FileSystemChangeEvent> subscription, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (TopicMessage<FileSystemChangeEvent> _ in subscription.Reader.ReadAllAsync(cancellationToken))
            {
                await InvokeAsync(async () => await RefreshAsync(cancellationToken));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the watched directory changes or the component is disposed.
        }
    }

    internal Task OnBackgroundContextMenuAsync(MouseEventArgs args) =>
        _contextMenu is null ? Task.CompletedTask : _contextMenu.OpenAsync(args, FileViewContextMenuScope.Background, item: null);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _watchLoopCts?.Cancel();
        _watchLoopCts?.Dispose();
        if (_watchSubscription is not null)
        {
            await _watchSubscription.DisposeAsync();
        }

        if (_jsModule is not null)
        {
            await _jsModule.DisposeAsync();
        }
    }
}
