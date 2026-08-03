namespace HackerOs.Apps.FileExplorer;

public enum FileExplorerSortColumn
{
    Name,
    Kind,
    Size,
    ModifiedDate
}

public enum FileExplorerViewMode
{
    Details,
    Grid
}

/// <summary>
/// Encapsulates state, navigation history, sorting, filtering, and item selection for File Explorer (`P2-FILE-002`, `P2-FILE-003`).
/// </summary>
public sealed class FileExplorerState
{
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private readonly HashSet<string> _selectedItemNames = new(StringComparer.Ordinal);

    public FileExplorerState(string initialPath = "/home/user")
    {
        CurrentPath = NormalizePath(initialPath);
    }

    public string CurrentPath { get; private set; }

    public bool CanNavigateBack => _backStack.Count > 0;
    public bool CanNavigateForward => _forwardStack.Count > 0;
    public bool CanNavigateUp => CurrentPath != "/";

    public FileExplorerSortColumn SortColumn { get; set; } = FileExplorerSortColumn.Name;
    public bool SortAscending { get; set; } = true;
    public FileExplorerViewMode ViewMode { get; set; } = FileExplorerViewMode.Details;
    public string SearchQuery { get; set; } = string.Empty;

    public IReadOnlySet<string> SelectedItemNames => _selectedItemNames;

    public event Action? StateChanged;

    public void NavigateTo(string newPath)
    {
        string normalized = NormalizePath(newPath);
        if (normalized == CurrentPath)
        {
            return;
        }

        _backStack.Push(CurrentPath);
        _forwardStack.Clear();
        CurrentPath = normalized;
        _selectedItemNames.Clear();
        NotifyStateChanged();
    }

    public void NavigateBack()
    {
        if (_backStack.Count == 0)
        {
            return;
        }

        _forwardStack.Push(CurrentPath);
        CurrentPath = _backStack.Pop();
        _selectedItemNames.Clear();
        NotifyStateChanged();
    }

    public void NavigateForward()
    {
        if (_forwardStack.Count == 0)
        {
            return;
        }

        _backStack.Push(CurrentPath);
        CurrentPath = _forwardStack.Pop();
        _selectedItemNames.Clear();
        NotifyStateChanged();
    }

    public void NavigateUp()
    {
        if (!CanNavigateUp)
        {
            return;
        }

        int lastSlash = CurrentPath.LastIndexOf('/');
        string parentPath = lastSlash > 0 ? CurrentPath[..lastSlash] : "/";
        NavigateTo(parentPath);
    }

    public void SetSortColumn(FileExplorerSortColumn column)
    {
        if (SortColumn == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            SortAscending = true;
        }
        NotifyStateChanged();
    }

    public void ToggleSelection(string itemName)
    {
        if (!_selectedItemNames.Remove(itemName))
        {
            _selectedItemNames.Add(itemName);
        }
        NotifyStateChanged();
    }

    public void SelectSingle(string itemName)
    {
        _selectedItemNames.Clear();
        _selectedItemNames.Add(itemName);
        NotifyStateChanged();
    }

    public void ClearSelection()
    {
        if (_selectedItemNames.Count > 0)
        {
            _selectedItemNames.Clear();
            NotifyStateChanged();
        }
    }

    public void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private static string NormalizePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return "/";
        }

        string[] segments = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Stack<string> stack = new();

        foreach (string segment in segments)
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (stack.Count > 0) stack.Pop();
            }
            else
            {
                stack.Push(segment);
            }
        }

        return "/" + string.Join('/', stack.ToArray().Reverse());
    }
}
