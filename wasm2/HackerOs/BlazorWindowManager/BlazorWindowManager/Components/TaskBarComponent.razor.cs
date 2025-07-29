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
    private bool _showContextMenu = false;
    private double _contextMenuX = 0;
    private double _contextMenuY = 0;
    private WindowInfo? _contextMenuWindow = null;
    private Timer? _clockTimer;

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
        if (!GroupedWindows) return;

        _groupedWindows.Clear();
        foreach (var window in _openWindows)
        {
            var groupKey = GetGroupKey(window);
            if (!_groupedWindows.ContainsKey(groupKey))
            {
                _groupedWindows[groupKey] = new List<WindowInfo>();
            }
            _groupedWindows[groupKey].Add(window);
        }
    }

    private string GetGroupKey(WindowInfo window)
    {
        // Group by window name (type) if specified, otherwise by title
        return !string.IsNullOrEmpty(window.Name) ? window.Name : window.Title;
    }

    private string GetWindowButtonClass(WindowInfo window)
    {
        var classes = new List<string>();
        
        if (window.State == WindowState.Minimized)
            classes.Add("minimized");
        
        if (WindowManager.GetActiveWindow()?.Id == window.Id)
            classes.Add("active");
        
        return string.Join(" ", classes);
    }

    private string GetDisplayTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return "Untitled";
        
        return title.Length > MaxTitleLength 
            ? title.Substring(0, MaxTitleLength - 3) + "..."
            : title;
    }

    private async Task OnWindowButtonClick(WindowInfo window)
    {
        CloseContextMenu();
        
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

    private void OnWindowButtonRightClick(MouseEventArgs e, WindowInfo window)
    {
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
