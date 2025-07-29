using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BlazorWindowManager.Models;
using BlazorWindowManager.Services;
using BlazorWindowManager.Components;

namespace BlazorWindowManager.Test.Pages;

/// <summary>
/// Code-behind for the Keyboard Navigation Demo page
/// </summary>
public partial class KeyboardNavigationDemo : ComponentBase, IAsyncDisposable
{
    [Inject] private KeyboardNavigationService KeyboardNav { get; set; } = default!;
    [Inject] private WindowManagerService WindowManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // Demo state properties
    private int totalWindows = 0;
    private string activeWindowTitle = "None";
    private string focusedWindowInfo = "None";
    private List<WindowInfo> allWindows = new();
    private List<LogEntry> eventLog = new();
    private Timer? _refreshTimer;
    private int _testWindowCounter = 1;

    // Event handling
    private bool _isInitialized = false;

    protected override async Task OnInitializedAsync()
    {
        // Subscribe to keyboard navigation events
        KeyboardNav.WindowSwitcherActivated += OnWindowSwitcherActivated;
        KeyboardNav.WindowSwitcherDeactivated += OnWindowSwitcherDeactivated;
        KeyboardNav.ShortcutTriggered += OnShortcutTriggered;        // Subscribe to window manager events
        WindowManager.WindowActiveChanged += OnWindowActiveChanged;
        WindowManager.WindowAfterClose += OnWindowClosed;
        WindowManager.WindowCreated += OnWindowCreated;
        WindowManager.WindowStateChanged += OnWindowStateChanged;

        LogEvent("Demo initialized - keyboard navigation ready");
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isInitialized = true;
            
            // Start a timer to refresh window information periodically
            _refreshTimer = new Timer(RefreshWindowInfo, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
            
            // Ensure keyboard navigation is initialized
            await KeyboardNav.InitializeAsync();
            
            LogEvent("Demo page rendered and timer started");
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    #region Demo Action Methods

    /// <summary>
    /// Creates a single test window
    /// </summary>
    private async Task CreateTestWindow()
    {
        try
        {            var windowId = Guid.NewGuid();
            var testWindow = new TestWindow($"Test Window {_testWindowCounter}");
            _testWindowCounter++;

            WindowManager.RegisterWindow(testWindow, windowId, testWindow.Title);
            LogEvent($"Created test window: {testWindow.Title}");
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            LogEvent($"Error creating test window: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates multiple test windows for demonstration
    /// </summary>
    private async Task CreateMultipleWindows()
    {
        try
        {
            var windowNames = new[] { "Calculator", "Notepad", "Browser", "File Manager", "Settings" };
              for (int i = 0; i < windowNames.Length; i++)
            {
                var windowId = Guid.NewGuid();
                var testWindow = new TestWindow($"{windowNames[i]} - Demo");

                WindowManager.RegisterWindow(testWindow, windowId, testWindow.Title);
                await Task.Delay(200); // Small delay for visual effect
            }

            LogEvent($"Created {windowNames.Length} demo windows");
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            LogEvent($"Error creating multiple windows: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests the window switcher functionality
    /// </summary>
    private async Task TestWindowSwitcher()
    {
        try
        {
            if (allWindows.Count < 2)
            {
                LogEvent("Creating windows for switcher test...");
                await CreateMultipleWindows();
                await Task.Delay(1000); // Allow windows to be created
            }

            LogEvent("Activating window switcher (you can also press Ctrl+`)");
            await KeyboardNav.ActivateWindowSwitcher();
        }
        catch (Exception ex)
        {
            LogEvent($"Error testing window switcher: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows keyboard shortcut help
    /// </summary>
    private async Task ShowShortcutHelp()
    {
        try
        {
            var shortcuts = new[]
            {
                "Ctrl + ` = Window Switcher",
                "Ctrl + Shift + W = Close Window",
                "Ctrl + Shift + M = Maximize/Restore",
                "Ctrl + Shift + N = Minimize",
                "Ctrl + Arrow Keys = Move Window",
                "Ctrl + Shift + Arrow Keys = Resize Window",
                "Ctrl + Tab = Cycle Next Window",
                "Ctrl + Shift + Tab = Cycle Previous Window",
                "Ctrl + Shift + Space = Window Context Menu"
            };

            foreach (var shortcut in shortcuts)
            {
                LogEvent($"Shortcut: {shortcut}");
                await Task.Delay(100);
            }

            LogEvent("All keyboard shortcuts listed above");
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            LogEvent($"Error showing shortcut help: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all demo windows
    /// </summary>
    private async Task ClearAllWindows()
    {
        try
        {
            var windowsToClose = allWindows.ToList();
            foreach (var window in windowsToClose)
            {
                WindowManager.UnregisterWindow(window.Id);
            }

            _testWindowCounter = 1;
            LogEvent($"Cleared all {windowsToClose.Count} windows");
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            LogEvent($"Error clearing windows: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests accessibility features
    /// </summary>
    private async Task TestAccessibility()
    {
        try
        {
            LogEvent("Testing accessibility features...");
            
            // Test ARIA announcements
            await JSRuntime.InvokeVoidAsync("console.log", "Testing screen reader announcements");
            LogEvent("Screen reader announcement: Keyboard navigation demo active");

            // Test tab order
            LogEvent("Testing keyboard tab order - try tabbing through elements");
            
            // Test keyboard focus
            if (allWindows.Any())
            {
                var firstWindow = allWindows.First();
                LogEvent($"Setting focus to: {firstWindow.Title}");
                if (firstWindow.ComponentRef != null)
                {
                    await KeyboardNav.FocusWindow(firstWindow.ComponentRef);
                }
            }
            else
            {
                LogEvent("No windows available for focus testing - creating a test window");
                await CreateTestWindow();
            }

            LogEvent("Accessibility test completed");
        }
        catch (Exception ex)
        {
            LogEvent($"Error during accessibility test: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the event log
    /// </summary>
    private void ClearEventLog()
    {
        eventLog.Clear();
        LogEvent("Event log cleared");
    }

    #endregion

    #region Event Handlers

    private void OnWindowSwitcherActivated(object? sender, WindowSwitcherEventArgs e)
    {
        LogEvent($"Window switcher activated - {e.Windows.Count} windows available");
        InvokeAsync(StateHasChanged);
    }

    private void OnWindowSwitcherDeactivated(object? sender, EventArgs e)
    {
        LogEvent("Window switcher deactivated");
        InvokeAsync(StateHasChanged);
    }

    private void OnShortcutTriggered(object? sender, KeyboardShortcutEventArgs e)
    {
        LogEvent($"Keyboard shortcut triggered: {e.EventArgs}");
        InvokeAsync(StateHasChanged);
    }    private void OnWindowActiveChanged(object? sender, WindowFocusChangedEventArgs e)
    {
        // Extract window information from the ComponentBase
        if (e.NewActiveWindow is WindowBase windowBase)
        {
            activeWindowTitle = windowBase.Title ?? "Untitled";
        }
        else
        {
            activeWindowTitle = "None";
        }
        LogEvent($"Active window changed to: {activeWindowTitle}");
        InvokeAsync(StateHasChanged);
    }    private void OnWindowClosed(object? sender, WindowInfo windowInfo)
    {
        LogEvent($"Window closed: {windowInfo.Title}");
        InvokeAsync(StateHasChanged);
    }    private void OnWindowCreated(object? sender, WindowInfo windowInfo)
    {
        LogEvent($"Window created: {windowInfo.Title}");
        InvokeAsync(StateHasChanged);
    }    private void OnWindowStateChanged(object? sender, WindowStateChangedEventArgs e)
    {
        var windowTitle = WindowManager.GetWindow(e.WindowId)?.Title ?? "Unknown";
        var stateText = e.NewState switch
        {
            WindowState.Minimized => "minimized",
            WindowState.Maximized => "maximized",
            WindowState.Normal => "restored",
            _ => e.NewState.ToString().ToLower()
        };
        LogEvent($"Window {stateText}: {windowTitle}");
        InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Refreshes window information display
    /// </summary>
    private void RefreshWindowInfo(object? state)
    {
        if (!_isInitialized) return;

        try
        {
            allWindows = WindowManager.GetAllWindows().ToList();
            totalWindows = allWindows.Count;

            var focusedWindow = KeyboardNav.GetFocusedWindow();
            if (focusedWindow is WindowBase windowBase)
            {
                var windowInfo = WindowManager.GetWindow(windowBase.Id);
                focusedWindowInfo = windowInfo?.Title ?? "Unknown";
            }
            else
            {
                focusedWindowInfo = "None";
            }

            // Update active window if it has changed
            var activeWindow = allWindows.FirstOrDefault(w => w.IsActive);
            activeWindowTitle = activeWindow?.Title ?? "None";

            InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            // Silent error handling for timer callback
            Console.WriteLine($"Error refreshing window info: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs an event to the event log
    /// </summary>
    private void LogEvent(string message)
    {
        eventLog.Add(new LogEntry
        {
            Time = DateTime.Now,
            Message = message
        });

        // Keep only the last 50 entries to prevent memory issues
        if (eventLog.Count > 50)
        {
            eventLog.RemoveAt(0);
        }
    }

    #endregion

    #region Disposal

    public async ValueTask DisposeAsync()
    {
        // Dispose timer
        _refreshTimer?.Dispose();

        // Unsubscribe from events
        if (KeyboardNav != null)
        {
            KeyboardNav.WindowSwitcherActivated -= OnWindowSwitcherActivated;
            KeyboardNav.WindowSwitcherDeactivated -= OnWindowSwitcherDeactivated;
            KeyboardNav.ShortcutTriggered -= OnShortcutTriggered;
        }

        if (WindowManager != null)
        {            WindowManager.WindowActiveChanged -= OnWindowActiveChanged;
            WindowManager.WindowAfterClose -= OnWindowClosed;
            WindowManager.WindowCreated -= OnWindowCreated;
            WindowManager.WindowStateChanged -= OnWindowStateChanged;
        }

        await Task.CompletedTask;
    }

    #endregion
}

/// <summary>
/// Simple test window component for demonstration
/// </summary>
public class TestWindow : WindowBase
{
    public TestWindow(string title)
    {
        Title = title;
    }

    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "test-window-content");
        builder.AddAttribute(2, "style", "padding: 20px; height: 100%; background: #f5f5f5;");
        
        builder.OpenElement(3, "h3");
        builder.AddContent(4, Title);
        builder.CloseElement();
        
        builder.OpenElement(5, "p");
        builder.AddContent(6, "This is a test window for demonstrating keyboard navigation features.");
        builder.CloseElement();
        
        builder.OpenElement(7, "p");
        builder.AddContent(8, "Try using keyboard shortcuts to interact with this window:");
        builder.CloseElement();
        
        builder.OpenElement(9, "ul");
        
        builder.OpenElement(10, "li");
        builder.AddContent(11, "Ctrl+Shift+W to close");
        builder.CloseElement();
        
        builder.OpenElement(12, "li");
        builder.AddContent(13, "Ctrl+Shift+M to maximize/restore");
        builder.CloseElement();
        
        builder.OpenElement(14, "li");
        builder.AddContent(15, "Ctrl+Arrow keys to move");
        builder.CloseElement();
        
        builder.OpenElement(16, "li");
        builder.AddContent(17, "Ctrl+Shift+Arrow keys to resize");
        builder.CloseElement();
        
        builder.CloseElement(); // ul
        
        builder.CloseElement(); // div
    }
}

/// <summary>
/// Represents a log entry in the demo
/// </summary>
public class LogEntry
{
    public DateTime Time { get; set; }
    public string Message { get; set; } = string.Empty;
}
