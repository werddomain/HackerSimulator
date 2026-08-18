using HackerOs.AppSdk.FileView;

namespace HackerOs.Apps.FileExplorer;

/// <summary>
/// Navigation history (back/forward/up) and the active view mode for File Explorer (`P2-FILE-002`).
/// Sorting, selection, and directory listing are no longer this class's concern — <see cref="FileView"/>
/// owns all three since the Phase 4 migration (`INT-001`); duplicating them here would be exactly what
/// `ADR 0037` commits to not doing. Only navigation history remains, since <see cref="FileView"/> itself
/// has no concept of "back"/"forward" — only the current directory and one-way <c>NavigateAsync</c>.
/// </summary>
public sealed class FileExplorerState
{
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();

    public FileExplorerState(string initialPath = "/home/user")
    {
        CurrentPath = NormalizePath(initialPath);
    }

    public string CurrentPath { get; private set; }

    public bool CanNavigateBack => _backStack.Count > 0;
    public bool CanNavigateForward => _forwardStack.Count > 0;
    public bool CanNavigateUp => CurrentPath != "/";

    public FileViewMode ViewMode { get; set; } = FileViewMode.Details;
    public string SearchQuery { get; set; } = string.Empty;

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
