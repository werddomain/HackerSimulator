using BlazorWindowManager.Services;
using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorWindowManager.Components;

/// <summary>
/// Task bar component for displaying and managing open windows
/// </summary>
public partial class TaskBarComponent : ComponentBase, IDisposable
{
    [Inject] public WindowManagerService WindowManager { get; set; } = default!;

    /// <summary>
    /// Content to display on the left side of the taskbar (e.g., Start Menu button)
    /// </summary>
    [Parameter] public RenderFragment? LeftContent { get; set; }
    
    /// <summary>
    /// Content to display on the right side of the taskbar (e.g., system tray icons)
    /// </summary>
    [Parameter] public RenderFragment? RightContent { get; set; }
    
    /// <summary>
    /// Whether to group windows by type in the taskbar
    /// </summary>
    [Parameter] public bool GroupedWindows { get; set; } = true;
    
    /// <summary>
    /// Maximum length of window title to display in taskbar button
    /// </summary>
    [Parameter] public int MaxTitleLength { get; set; } = 20;

    private List<WindowInfo> _openWindows = new();
    private Dictionary<string, List<WindowInfo>> _groupedWindows = new();
    private List<string> _groupOrder = new();
    private bool _showContextMenu = false;
    private double _contextMenuX = 0;
    private double _contextMenuY = 0;
    private WindowInfo? _contextMenuWindow = null;
    private Timer? _clockTimer;

    // Grouped-window popup state: shown when a taskbar button represents more
    // than one open window of the same app so the user can pick which one.
    private bool _showGroupPopup = false;
    private string? _groupPopupKey;
    private double _groupPopupX = 0;
    private double _groupPopupY = 0;

    private enum WindowAction
    {
        Restore,
        Minimize,
        Maximize,
        Close
    }

    protected override void OnInitialized()
    {
        // Subscribe to window manager events
        WindowManager.WindowRegistered += OnWindowRegistered;
        WindowManager.WindowUnregistered += OnWindowUnregistered;
        WindowManager.WindowStateChanged += OnWindowStateChanged;
        WindowManager.WindowTitleChanged += OnWindowTitleChanged;
        WindowManager.WindowFocused += OnWindowFocused;

        // Initialize with existing windows
        _openWindows = WindowManager.GetAllWindows().ToList();
        UpdateGroupedWindows();

        // Start clock timer
        _clockTimer = new Timer(UpdateClock, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void OnWindowRegistered(object? sender, WindowEventArgs e)
    {
        if (e.Window != null && !_openWindows.Any(w => w.Id == e.Window.Id))
        {
            _openWindows.Add(e.Window);
            UpdateGroupedWindows();
            InvokeAsync(StateHasChanged);
        }
    }

    private void OnWindowUnregistered(object? sender, WindowEventArgs e)
    {
        if (e.Window != null)
        {
            _openWindows.RemoveAll(w => w.Id == e.Window.Id);
            UpdateGroupedWindows();
            InvokeAsync(StateHasChanged);
        }
    }

    private void OnWindowStateChanged(object? sender, WindowStateChangedEventArgs e)
    {
        var window = _openWindows.FirstOrDefault(w => w.Id == e.WindowId);
        if (window != null)
        {
            window.State = e.NewState;
            InvokeAsync(StateHasChanged);
        }
    }

    private void OnWindowTitleChanged(object? sender, WindowTitleChangedEventArgs e)
    {
        var window = _openWindows.FirstOrDefault(w => w.Id == e.WindowId);
        if (window != null)
        {
            window.Title = e.NewTitle;
            UpdateGroupedWindows();
            InvokeAsync(StateHasChanged);
        }
    }

    private void OnWindowFocused(object? sender, WindowEventArgs e)
    {
        // Update active window for visual indication
        InvokeAsync(StateHasChanged);
    }

    private void UpdateGroupedWindows()
    {
        _groupedWindows.Clear();
        _groupOrder.Clear();

        foreach (var window in _openWindows)
        {
            // When grouping is disabled every window gets its own unique key
            // so the render loop can treat both modes identically.
            var groupKey = GroupedWindows ? GetGroupKey(window) : window.Id.ToString();

            if (!_groupedWindows.TryGetValue(groupKey, out var group))
            {
                group = new List<WindowInfo>();
                _groupedWindows[groupKey] = group;
                _groupOrder.Add(groupKey);
            }

            group.Add(window);
        }

        // Drop the popup if its group no longer exists (e.g. all instances closed).
        if (_groupPopupKey != null && !_groupedWindows.ContainsKey(_groupPopupKey))
        {
            CloseGroupPopup();
        }
    }

    private string GetGroupKey(WindowInfo window)
    {
        // Group by window name (type) if specified, otherwise by title
        return !string.IsNullOrEmpty(window.Name) ? window.Name : window.Title;
    }

    /// <summary>
    /// Picks which window in a group is shown as the taskbar button's icon/title
    /// (the active one if it belongs to the group, otherwise the most recent).
    /// </summary>
    private WindowInfo GetRepresentativeWindow(List<WindowInfo> group)
    {
        var activeId = WindowManager.GetActiveWindow()?.Id;
        if (activeId.HasValue)
        {
            var active = group.FirstOrDefault(w => w.Id == activeId.Value);
            if (active != null) return active;
        }

        return group[^1];
    }

    private string GetWindowButtonClass(List<WindowInfo> group)
    {
        var classes = new List<string>();

        if (group.All(w => w.State == WindowState.Minimized))
            classes.Add("minimized");

        var activeId = WindowManager.GetActiveWindow()?.Id;
        if (activeId.HasValue && group.Any(w => w.Id == activeId.Value))
            classes.Add("active");

        if (group.Count > 1)
            classes.Add("grouped");

        return string.Join(" ", classes);
    }

    private string GetDisplayTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return "Untitled";
        
        return title.Length > MaxTitleLength 
            ? title.Substring(0, MaxTitleLength - 3) + "..."
            : title;
    }

    private async Task OnWindowButtonClick(MouseEventArgs e, string groupKey)
    {
        CloseContextMenu();

        if (!_groupedWindows.TryGetValue(groupKey, out var group) || group.Count == 0)
        {
            return;
        }

        if (group.Count > 1)
        {
            // Multiple windows share this button; let the user pick which one
            // instead of guessing.
            ToggleGroupPopup(groupKey, e.ClientX, e.ClientY);
            return;
        }

        CloseGroupPopup();
        await FocusOrToggleWindow(group[0]);
    }

    private async Task OnGroupPopupItemClick(WindowInfo window)
    {
        CloseGroupPopup();
        await FocusOrToggleWindow(window);
    }

    private async Task FocusOrToggleWindow(WindowInfo window)
    {
        if (window.State == WindowState.Minimized)
        {
            // Restore minimized window
            await WindowManager.RestoreWindowAsync(window.Id);
        }
        else if (WindowManager.GetActiveWindow()?.Id == window.Id)
        {
            // If clicking on already active window, minimize it
            await WindowManager.MinimizeWindowAsync(window.Id);
        }
        else
        {
            // Focus the window
            await WindowManager.FocusWindowAsync(window.Id);
        }
    }

    private void ToggleGroupPopup(string groupKey, double x, double y)
    {
        if (_showGroupPopup && _groupPopupKey == groupKey)
        {
            CloseGroupPopup();
            return;
        }

        _groupPopupKey = groupKey;
        _groupPopupX = x;
        _groupPopupY = y - 160; // position the popup above the taskbar button
        _showGroupPopup = true;
        StateHasChanged();
    }

    private void CloseGroupPopup()
    {
        _showGroupPopup = false;
        _groupPopupKey = null;
    }

    private void OnWindowButtonRightClick(MouseEventArgs e, WindowInfo window)
    {
        CloseGroupPopup();
        _contextMenuWindow = window;
        _contextMenuX = e.ClientX;
        _contextMenuY = e.ClientY - 120; // Position above taskbar
        _showContextMenu = true;
        StateHasChanged();
    }

    private async Task PerformWindowAction(WindowInfo? window, WindowAction action)
    {
        if (window == null) return;

        CloseContextMenu();

        switch (action)
        {
            case WindowAction.Restore:
                await WindowManager.RestoreWindowAsync(window.Id);
                break;
            case WindowAction.Minimize:
                await WindowManager.MinimizeWindowAsync(window.Id);
                break;
            case WindowAction.Maximize:
                await WindowManager.MaximizeWindowAsync(window.Id);
                break;
            case WindowAction.Close:
                await WindowManager.CloseWindowAsync(window.Id, force: false);
                break;
        }
    }

    private void CloseContextMenu()
    {
        _showContextMenu = false;
        _contextMenuWindow = null;
        StateHasChanged();
    }

    private void UpdateClock(object? state)
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        // Unsubscribe from events
        WindowManager.WindowRegistered -= OnWindowRegistered;
        WindowManager.WindowUnregistered -= OnWindowUnregistered;
        WindowManager.WindowStateChanged -= OnWindowStateChanged;
        WindowManager.WindowTitleChanged -= OnWindowTitleChanged;
        WindowManager.WindowFocused -= OnWindowFocused;

        // Dispose timer
        _clockTimer?.Dispose();
    }
}
