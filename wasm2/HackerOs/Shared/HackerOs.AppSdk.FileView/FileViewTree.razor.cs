using Microsoft.AspNetCore.Components;

namespace HackerOs.AppSdk.FileView;

/// <summary>
/// Code-behind for the Tree-mode root renderer (<c>FV-004</c>): builds/caches root
/// <see cref="FileViewTreeNodeState"/> instances from <see cref="FileView.Items"/>, keyed by path so
/// expansion state survives the owner rebuilding its <see cref="FileViewItem"/> instances on refresh.
/// </summary>
public partial class FileViewTree
{
    private readonly Dictionary<string, FileViewTreeNodeState> _nodeCache = new(StringComparer.Ordinal);

    /// <summary>The owning <see cref="FileView"/> whose items this renderer displays.</summary>
    [Parameter, EditorRequired]
    public FileView Owner { get; set; } = null!;

    internal IReadOnlyList<FileViewTreeNodeState> RootNodes
    {
        get
        {
            Dictionary<string, FileViewTreeNodeState> next = new(StringComparer.Ordinal);
            List<FileViewTreeNodeState> nodes = new(Owner.Items.Count);
            foreach (FileViewItem item in Owner.Items)
            {
                if (!_nodeCache.TryGetValue(item.FullPath.Value, out FileViewTreeNodeState? node))
                {
                    node = new FileViewTreeNodeState();
                }

                node.Item = item;
                next[item.FullPath.Value] = node;
                nodes.Add(node);
            }

            _nodeCache.Clear();
            foreach (KeyValuePair<string, FileViewTreeNodeState> pair in next)
            {
                _nodeCache[pair.Key] = pair.Value;
            }

            return nodes;
        }
    }

    /// <summary>
    /// Every currently visible node (root nodes plus the children of any expanded node, recursively) in
    /// display order — used for <c>FV-011</c>'s ArrowUp/ArrowDown roving-tabindex navigation, since a
    /// <see cref="FileViewTreeNode"/> only knows its own subtree and needs the full flattened order to find
    /// its predecessor/successor.
    /// </summary>
    internal IReadOnlyList<FileViewTreeNodeState> GetVisibleNodesInOrder()
    {
        List<FileViewTreeNodeState> result = [];
        Flatten(RootNodes, result);
        return result;
    }

    private static void Flatten(IReadOnlyList<FileViewTreeNodeState> nodes, List<FileViewTreeNodeState> result)
    {
        foreach (FileViewTreeNodeState node in nodes)
        {
            result.Add(node);
            if (node.Item.IsDirectory && node.IsExpanded && node.Children is not null)
            {
                Flatten(node.Children, result);
            }
        }
    }
}
