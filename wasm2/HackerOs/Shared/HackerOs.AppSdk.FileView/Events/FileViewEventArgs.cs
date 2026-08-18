using HackerOs.App.Abstractions;
using HackerOs.AppSdk.FileView.ContextMenu;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.AppSdk.FileView.Events;

/// <summary>
/// Base type for every cancelable "-ing" event raised by <see cref="FileView"/> before it performs an
/// observable action. Setting <see cref="Cancel"/> stops the action from proceeding — this is the
/// literal <c>e.Cancel = true</c> interception point requested for the control. See
/// <c>docs/Global-FileView-And-MessagingSystem/FileViewControl.md#cancelable-eventing-model</c>.
/// </summary>
public abstract class FileViewCancelEventArgs : EventArgs
{
    /// <summary>Gets or sets whether the pending action should be canceled.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Raised before <see cref="FileView"/> navigates to a new directory.</summary>
public sealed class FileViewNavigatingEventArgs(VirtualPath from, VirtualPath to) : FileViewCancelEventArgs
{
    /// <summary>Gets the directory being navigated away from.</summary>
    public VirtualPath From { get; } = from;

    /// <summary>Gets the directory being navigated to.</summary>
    public VirtualPath To { get; } = to;
}

/// <summary>Raised after <see cref="FileView"/> has navigated. Named to match the control's documented external API.</summary>
public sealed class FileViewPathChangedEventArgs(VirtualPath path) : EventArgs
{
    /// <summary>Gets the current directory.</summary>
    public VirtualPath Path { get; } = path;
}

/// <summary>Raised before an item is activated (double-click, Enter, or context-menu default action).</summary>
public sealed class FileViewOpeningEventArgs(FileViewItem item, FileViewFolderActivationMode mode) : FileViewCancelEventArgs
{
    /// <summary>Gets the item being activated.</summary>
    public FileViewItem Item { get; } = item;

    /// <summary>Gets the configured folder-activation mode in effect for this activation.</summary>
    public FileViewFolderActivationMode Mode { get; } = mode;
}

/// <summary>Raised after an item has been activated.</summary>
public sealed class FileViewOpenedEventArgs(FileViewItem item) : EventArgs
{
    /// <summary>Gets the item that was activated.</summary>
    public FileViewItem Item { get; } = item;
}

/// <summary>Raised before an inline rename is committed to the filesystem.</summary>
public sealed class FileViewRenamingEventArgs(FileViewItem item, string proposedName) : FileViewCancelEventArgs
{
    /// <summary>Gets the item being renamed.</summary>
    public FileViewItem Item { get; } = item;

    /// <summary>Gets the proposed new name.</summary>
    public string ProposedName { get; } = proposedName;
}

/// <summary>Raised after an inline rename has been committed.</summary>
public sealed class FileViewRenamedEventArgs(FileViewItem item, string previousName) : EventArgs
{
    /// <summary>Gets the renamed item.</summary>
    public FileViewItem Item { get; } = item;

    /// <summary>Gets the item's previous name.</summary>
    public string PreviousName { get; } = previousName;
}

/// <summary>Raised before a create action initiated through <see cref="FileView"/>'s own UI proceeds.</summary>
public sealed class FileViewCreatingEventArgs(VirtualPath parentDirectory, string name, FileSystemEntryKind kind) : FileViewCancelEventArgs
{
    /// <summary>Gets the directory the new entry will be created in.</summary>
    public VirtualPath ParentDirectory { get; } = parentDirectory;

    /// <summary>Gets the new entry's name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the kind of entry being created.</summary>
    public FileSystemEntryKind Kind { get; } = kind;
}

/// <summary>Raised after a create action has completed.</summary>
public sealed class FileViewCreatedEventArgs(FileViewItem item) : EventArgs
{
    /// <summary>Gets the created item.</summary>
    public FileViewItem Item { get; } = item;
}

/// <summary>Raised before a delete action initiated through <see cref="FileView"/>'s own UI proceeds.</summary>
public sealed class FileViewDeletingEventArgs(IReadOnlyList<FileViewItem> items) : FileViewCancelEventArgs
{
    /// <summary>Gets the items pending deletion.</summary>
    public IReadOnlyList<FileViewItem> Items { get; } = items;
}

/// <summary>Raised after a delete action has completed.</summary>
public sealed class FileViewDeletedEventArgs(IReadOnlyList<VirtualPath> paths) : EventArgs
{
    /// <summary>Gets the paths that were deleted.</summary>
    public IReadOnlyList<VirtualPath> Paths { get; } = paths;
}

/// <summary>Raised before a move (drag-drop move or inline rename) proceeds.</summary>
public sealed class FileViewMovingEventArgs(IReadOnlyList<FileViewItem> items, VirtualPath destination) : FileViewCancelEventArgs
{
    /// <summary>Gets the items being moved.</summary>
    public IReadOnlyList<FileViewItem> Items { get; } = items;

    /// <summary>Gets the destination directory.</summary>
    public VirtualPath Destination { get; } = destination;
}

/// <summary>Raised after a move has completed.</summary>
public sealed class FileViewMovedEventArgs(IReadOnlyList<FileViewItem> items, VirtualPath destination) : EventArgs
{
    /// <summary>Gets the moved items.</summary>
    public IReadOnlyList<FileViewItem> Items { get; } = items;

    /// <summary>Gets the destination directory.</summary>
    public VirtualPath Destination { get; } = destination;
}

/// <summary>Raised before a drag-drop copy (Ctrl-modified drop) proceeds.</summary>
public sealed class FileViewCopyingEventArgs(IReadOnlyList<FileViewItem> items, VirtualPath destination) : FileViewCancelEventArgs
{
    /// <summary>Gets the items being copied.</summary>
    public IReadOnlyList<FileViewItem> Items { get; } = items;

    /// <summary>Gets the destination directory.</summary>
    public VirtualPath Destination { get; } = destination;
}

/// <summary>Raised after a drag-drop copy has completed.</summary>
public sealed class FileViewCopiedEventArgs(IReadOnlyList<FileViewItem> items, VirtualPath destination) : EventArgs
{
    /// <summary>Gets the copied items.</summary>
    public IReadOnlyList<FileViewItem> Items { get; } = items;

    /// <summary>Gets the destination directory.</summary>
    public VirtualPath Destination { get; } = destination;
}

/// <summary>Raised before a selection change (programmatic or user-driven) takes effect.</summary>
public sealed class FileViewSelectionChangingEventArgs(IReadOnlyList<FileViewItem> proposedSelection) : FileViewCancelEventArgs
{
    /// <summary>Gets the selection that would result if not canceled.</summary>
    public IReadOnlyList<FileViewItem> ProposedSelection { get; } = proposedSelection;
}

/// <summary>Raised after the selection has changed.</summary>
public sealed class FileViewSelectionChangedEventArgs(IReadOnlyList<FileViewItem> selection) : EventArgs
{
    /// <summary>Gets the current selection.</summary>
    public IReadOnlyList<FileViewItem> Selection { get; } = selection;
}

/// <summary>
/// Raised when a context menu is about to open, after every matching
/// <see cref="IFileViewContextMenuProvider"/> has already customized <see cref="Items"/> — this is the
/// final, host-level adjustment point.
/// </summary>
public sealed class FileViewContextMenuOpeningEventArgs(
    FileViewContextMenuScope scope,
    FileViewItem? item,
    FileViewMenuItemCollection items) : EventArgs
{
    /// <summary>Gets which scope triggered this menu.</summary>
    public FileViewContextMenuScope Scope { get; } = scope;

    /// <summary>Gets the item that was right-clicked, or <see langword="null"/> for the background scope.</summary>
    public FileViewItem? Item { get; } = item;

    /// <summary>Gets the mutable menu item collection about to be shown.</summary>
    public FileViewMenuItemCollection Items { get; } = items;
}
