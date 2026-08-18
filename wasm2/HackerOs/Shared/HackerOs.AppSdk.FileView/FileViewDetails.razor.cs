using HackerOs.AppSdk.FileView.Icons;
using HackerOs.Simulation.Abstractions.FileSystem;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace HackerOs.AppSdk.FileView;

/// <summary>Code-behind for the sortable Details-mode renderer (<c>FV-002</c>).</summary>
public partial class FileViewDetails
{
    private const string NameColumnKey = "name";

    private static readonly IReadOnlyList<FileViewColumn> DefaultColumns =
    [
        new(NameColumnKey, "Name", static item => item.FileName),
        new("kind", "Kind", static item => item.IsDirectory ? "Folder" : "File"),
        new("size", "Size", static item => item.Metadata is FileMetadata file ? file.Length : -1L),
        new("modified", "Modified", static item => item.Metadata.Timestamps.ContentModifiedAtUtc)
    ];

    private readonly Dictionary<FileViewItem, string> _renameBuffers = [];
    private string _sortColumnKey = NameColumnKey;
    private bool _sortDescending;

    /// <summary>The owning <see cref="FileView"/> whose items this renderer displays.</summary>
    [Parameter, EditorRequired]
    public FileView Owner { get; set; } = null!;

    private IReadOnlyList<FileViewColumn> EffectiveColumns => Owner.Columns ?? DefaultColumns;

    private IEnumerable<FileViewItem> SortedItems
    {
        get
        {
            FileViewColumn column = EffectiveColumns.FirstOrDefault(c => c.Key == _sortColumnKey) ?? EffectiveColumns[0];
            IOrderedEnumerable<FileViewItem> ordered = Owner.Items.OrderBy(column.SortAccessor);
            return _sortDescending ? ordered.Reverse() : ordered;
        }
    }

    private void ToggleSort(string key)
    {
        if (_sortColumnKey == key)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortColumnKey = key;
            _sortDescending = false;
        }
    }

    private string AriaSort(string key) =>
        key != _sortColumnKey ? "none" : _sortDescending ? "descending" : "ascending";

    private static readonly IReadOnlyDictionary<string, object> EmptyAttributes =
        new Dictionary<string, object>();

    private static IReadOnlyDictionary<string, object> GetHeaderAttributes(FileViewColumn column) =>
        column.Width is { } width
            ? new Dictionary<string, object> { ["style"] = FormattableString.Invariant($"width:{width}px") }
            : EmptyAttributes;

    /// <summary>
    /// Roving tabindex (<c>FV-011</c>): only the active item — the selection, or the first row when
    /// nothing is selected yet — is ever a Tab stop, so Tab moves past the whole table in one step
    /// instead of stopping at every row. Arrow keys move the active item within the table.
    /// </summary>
    internal bool IsTabStop(FileViewItem item) =>
        item.IsSelected || (Owner.SelectedItem is null && ReferenceEquals(item, SortedItems.FirstOrDefault()));

    internal void OnRowClick(FileViewItem item, MouseEventArgs args) => item.Select(additive: args.CtrlKey);

    internal Task OnRowDoubleClickAsync(FileViewItem item) => Owner.ActivateItemAsync(item);

    private Task OnRowContextMenuAsync(FileViewItem item, MouseEventArgs args)
    {
        item.Select();
        return Owner.OpenItemContextMenuAsync(args, item);
    }

    internal Task OnRowKeyDownAsync(FileViewItem item, KeyboardEventArgs args) => args.Key switch
    {
        "Enter" => Owner.ActivateItemAsync(item),
        "F2" => RenameAsync(item),
        "Delete" => DeleteAsync(item),
        "ArrowDown" => MoveActiveAsync(item, 1),
        "ArrowUp" => MoveActiveAsync(item, -1),
        _ => Task.CompletedTask
    };

    private Task MoveActiveAsync(FileViewItem item, int direction)
    {
        List<FileViewItem> ordered = [.. SortedItems];
        int index = ordered.IndexOf(item) + direction;
        return index >= 0 && index < ordered.Count ? Owner.MoveActiveItemAsync(ordered[index]) : Task.CompletedTask;
    }

    private Task OnRowDragStartAsync(FileViewItem item, DragEventArgs args) => Owner.OnItemDragStartAsync(item, args);

    private Task OnRowDropAsync(FileViewItem item, DragEventArgs args) => Owner.OnItemDropAsync(item, args);

    private Task RenameAsync(FileViewItem item)
    {
        item.Select();
        item.Rename();
        return Task.CompletedTask;
    }

    private Task DeleteAsync(FileViewItem item)
    {
        item.Select();
        return Owner.DeleteSelectedAsync();
    }

    private string GetRenameBuffer(FileViewItem item)
    {
        if (!_renameBuffers.TryGetValue(item, out string? value))
        {
            value = item.FileName;
            _renameBuffers[item] = value;
        }

        return value;
    }

    private void SetRenameBuffer(FileViewItem item, string value) => _renameBuffers[item] = value;

    private Task OnRenameKeyDownAsync(FileViewItem item, KeyboardEventArgs args) => args.Key switch
    {
        "Enter" => CommitRenameAsync(item),
        "Escape" => CancelRenameAsync(item),
        _ => Task.CompletedTask
    };

    private Task CommitRenameAsync(FileViewItem item)
    {
        string value = GetRenameBuffer(item);
        _renameBuffers.Remove(item);
        return Owner.CommitRenameAsync(item, value);
    }

    private Task CancelRenameAsync(FileViewItem item)
    {
        _renameBuffers.Remove(item);
        Owner.CancelRename(item);
        return Task.CompletedTask;
    }

    private ShellIconDescriptor ResolveIcon(FileViewItem item)
    {
        if (item.IconOverride is not null)
        {
            return item.IconOverride;
        }

        string? extension = GetExtension(item.FileName);
        string? mediaType = item.Metadata is FileMetadata file ? file.MediaType : null;
        return Owner.EffectiveIconProvider.Resolve(new FileViewIconRequest(item.FullPath, item.IsDirectory, extension, mediaType));
    }

    private static string? GetExtension(string fileName)
    {
        int lastDot = fileName.LastIndexOf('.');
        return lastDot > 0 ? fileName[lastDot..].ToLowerInvariant() : null;
    }

    private static string FormatValue(FileViewColumn column, FileViewItem item)
    {
        IComparable value = column.SortAccessor(item);
        return value switch
        {
            DateTimeOffset timestamp => timestamp.ToString("yyyy-MM-dd HH:mm"),
            long size and < 0 => string.Empty,
            long size => FormatBytes(size),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.#} {units[unitIndex]}";
    }
}
