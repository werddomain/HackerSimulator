using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BlazorWindowManager.Models;
using BlazorWindowManager.Components;

namespace BlazorWindowManager.Services;

/// <summary>
/// Service for managing keyboard navigation and accessibility features with configurable shortcuts
/// </summary>
public class KeyboardNavigationService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly WindowManagerService _windowManager;
    private readonly KeyboardShortcutConfig _shortcutConfig;
    private IJSObjectReference? _jsModule;
    private bool _isInitialized = false;
    private ComponentBase? _currentFocusedWindow;
    private bool _isWindowSwitcherActive = false;
    private List<ComponentBase> _switcherWindowOrder = new();
    private int _switcherCurrentIndex = 0;

    /// <summary>
    /// Event fired when the window switcher is activated
    /// </summary>
    public event EventHandler<WindowSwitcherEventArgs>? WindowSwitcherActivated;

    /// <summary>
    /// Event fired when the window switcher is deactivated
    /// </summary>
    public event EventHandler? WindowSwitcherDeactivated;

    /// <summary>
    /// Event fired when a keyboard shortcut is triggered
    /// </summary>
    public event EventHandler<KeyboardShortcutEventArgs>? ShortcutTriggered;

    /// <summary>
    /// Gets the keyboard shortcut configuration
    /// </summary>
    public KeyboardShortcutConfig ShortcutConfig => _shortcutConfig;

    /// <summary>
    /// Gets whether the window switcher is currently active
    /// </summary>
    public bool IsWindowSwitcherActive => _isWindowSwitcherActive;

    /// <summary>
    /// Gets the current window order for the window switcher
    /// </summary>
    public IReadOnlyList<ComponentBase> SwitcherWindowOrder => _switcherWindowOrder.AsReadOnly();    /// <summary>
    /// Gets the current index in the window switcher
    /// </summary>
    public int SwitcherCurrentIndex => _switcherCurrentIndex;

    /// <summary>
    /// Configuration object for JavaScript interop
    /// </summary>
    public object Config => new
    {
        EnableWindowSwitcher,
        EnableArrowKeyMovement,
        EnableKeyboardShortcuts,
        ShowVisualFeedback,
        SmallMovementStep = MovementStepSize,
        LargeMovementStep = LargeMovementStepSize,
        SmallResizeStep = 20,
        LargeResizeStep = 100
    };

    // Configuration options
    public bool EnableWindowSwitcher { get; set; } = true;
    public bool EnableArrowKeyMovement { get; set; } = true;
    public bool EnableKeyboardShortcuts { get; set; } = true;
    public bool ShowVisualFeedback { get; set; } = true;
    public int MovementStepSize { get; set; } = 10;
    public int LargeMovementStepSize { get; set; } = 50;public KeyboardNavigationService(IJSRuntime jsRuntime, WindowManagerService windowManager, KeyboardShortcutConfig? shortcutConfig = null)
    {
        _jsRuntime = jsRuntime;
        _windowManager = windowManager;
        _shortcutConfig = shortcutConfig ?? KeyboardShortcutConfig.CreateDefault();        _windowManager.WindowActiveChanged += OnWindowActiveChanged;
        _windowManager.WindowAfterClose += OnWindowClosed;
    }

    /// <summary>
    /// Initializes the keyboard navigation service
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            _jsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/BlazorWindowManager/js/keyboardNavigation.js");

            // Set up global keyboard listeners
            await _jsModule.InvokeVoidAsync("initialize", 
                DotNetObjectReference.Create(this),
                Config);

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize keyboard navigation: {ex.Message}");
        }
    }    /// <summary>
    /// Activates the window switcher (Alt-Tab style)
    /// </summary>
    [JSInvokable]
    public async Task ActivateWindowSwitcher()
    {
        if (!EnableWindowSwitcher || _isWindowSwitcherActive) return;

        _isWindowSwitcherActive = true;
        var allWindows = _windowManager.GetAllWindows()
            .Where(w => w.State != WindowState.Minimized)
            .OrderByDescending(w => w.ZIndex)
            .Select(w => w.ComponentRef)
            .Where(c => c != null)
            .Cast<ComponentBase>()
            .ToList();

        _switcherWindowOrder = allWindows;
        _switcherCurrentIndex = _switcherWindowOrder.Count > 1 ? 1 : 0;

        WindowSwitcherActivated?.Invoke(this, new WindowSwitcherEventArgs
        {
            Windows = _switcherWindowOrder,
            CurrentIndex = _switcherCurrentIndex
        });

        // Highlight the next window in sequence
        if (_switcherWindowOrder.Count > 1)
        {
            await HighlightWindow(_switcherWindowOrder[_switcherCurrentIndex]);
        }
    }

    /// <summary>
    /// Cycles to the next window in the switcher
    /// </summary>
    [JSInvokable]
    public async Task SwitcherNext()
    {
        if (!_isWindowSwitcherActive || _switcherWindowOrder.Count <= 1) return;

        _switcherCurrentIndex = (_switcherCurrentIndex + 1) % _switcherWindowOrder.Count;
        await HighlightWindow(_switcherWindowOrder[_switcherCurrentIndex]);
    }

    /// <summary>
    /// Cycles to the previous window in the switcher
    /// </summary>
    [JSInvokable]
    public async Task SwitcherPrevious()
    {
        if (!_isWindowSwitcherActive || _switcherWindowOrder.Count <= 1) return;

        _switcherCurrentIndex = _switcherCurrentIndex == 0 
            ? _switcherWindowOrder.Count - 1 
            : _switcherCurrentIndex - 1;
        await HighlightWindow(_switcherWindowOrder[_switcherCurrentIndex]);
    }

    /// <summary>
    /// Confirms the window selection and deactivates the switcher
    /// </summary>
    [JSInvokable]
    public async Task ConfirmWindowSwitcher()
    {
        if (!_isWindowSwitcherActive) return;        if (_switcherWindowOrder.Count > 0 && _switcherCurrentIndex < _switcherWindowOrder.Count)        {
            var selectedWindow = _switcherWindowOrder[_switcherCurrentIndex];
            if (selectedWindow is WindowBase windowBase)
            {
                _windowManager.BringToFront(windowBase.Id);
                await FocusWindow(selectedWindow);
            }
        }

        await DeactivateWindowSwitcher();
    }

    /// <summary>
    /// Cancels the window switcher without changing focus
    /// </summary>
    [JSInvokable]
    public async Task CancelWindowSwitcher()
    {
        await DeactivateWindowSwitcher();
    }

    /// <summary>
    /// Deactivates the window switcher
    /// </summary>
    public async Task DeactivateWindowSwitcher()
    {
        if (!_isWindowSwitcherActive) return;

        _isWindowSwitcherActive = false;
        _switcherWindowOrder.Clear();
        _switcherCurrentIndex = 0;

        // Clear any highlighting
        if (_jsModule != null)
        {
            await _jsModule.InvokeVoidAsync("clearWindowHighlighting");
        }

        WindowSwitcherDeactivated?.Invoke(this, EventArgs.Empty);
    }    /// <summary>
    /// Handles keyboard shortcuts for window management using predicate-based configuration
    /// </summary>
    [JSInvokable]
    public async Task HandleKeyboardEvent(string key, bool ctrlKey, bool altKey, bool shiftKey, bool metaKey)
    {
        var eventArgs = new KeyboardEventArgs(key, ctrlKey, altKey, shiftKey, metaKey);
        
        // Check each shortcut using the predicate functions
        if (_shortcutConfig.IsWindowSwitcherShortcut(eventArgs))
        {
            await ActivateWindowSwitcher();
        }
        else if (_shortcutConfig.IsCloseWindowShortcut(eventArgs))
        {
            await CloseActiveWindow();
        }
        else if (_shortcutConfig.IsMaximizeWindowShortcut(eventArgs))
        {
            await ToggleMaximizeActiveWindow();
        }
        else if (_shortcutConfig.IsMinimizeWindowShortcut(eventArgs))
        {
            await MinimizeActiveWindow();
        }
        else if (_shortcutConfig.IsMoveWindowShortcut(eventArgs))
        {
            await MoveWindowKeyboard(GetDirectionFromArrowKey(key), shiftKey);
        }
        else if (_shortcutConfig.IsResizeWindowShortcut(eventArgs))
        {
            await ResizeWindowKeyboard(GetDirectionFromArrowKey(key), shiftKey);
        }
        else if (_shortcutConfig.IsCycleWindowsNextShortcut(eventArgs))
        {
            await CycleToNextWindow();
        }
        else if (_shortcutConfig.IsCycleWindowsPreviousShortcut(eventArgs))
        {
            await CycleToPreviousWindow();
        }
        else if (_shortcutConfig.IsWindowContextMenuShortcut(eventArgs))
        {
            await ShowWindowContextMenu();
        }
        
        // Fire the shortcut triggered event for custom handling
        ShortcutTriggered?.Invoke(this, new KeyboardShortcutEventArgs 
        { 
            EventArgs = eventArgs,
            Handled = false 
        });
    }

    /// <summary>
    /// Registers a window with the keyboard navigation system
    /// </summary>
    public async Task RegisterWindowAsync(Guid windowId)
    {
        // Implementation for window registration
        await Task.CompletedTask;
    }

    /// <summary>
    /// Unregisters a window from the keyboard navigation system
    /// </summary>
    public async Task UnregisterWindowAsync(Guid windowId)
    {
        // Implementation for window unregistration
        await Task.CompletedTask;
    }    /// <summary>
    /// Moves the focused window using keyboard
    /// </summary>
    [JSInvokable]    public async Task MoveWindowKeyboard(string direction, bool isLargeStep = false)
    {
        if (_currentFocusedWindow is not WindowBase windowBase) return;

        var step = isLargeStep ? LargeMovementStepSize : MovementStepSize;
        var windowInfo = _windowManager.GetWindow(windowBase.Id);
        if (windowInfo == null) return;

        var newBounds = windowInfo.Bounds;

        switch (direction.ToLower())
        {
            case "up":
                newBounds.Top = Math.Max(0, newBounds.Top - step);
                break;
            case "down":
                newBounds.Top += step;
                break;
            case "left":
                newBounds.Left = Math.Max(0, newBounds.Left - step);
                break;            case "right":
                newBounds.Left += step;
                break;
        }

        _windowManager.UpdateWindowBounds(windowBase.Id, newBounds);
    }    /// <summary>
    /// Resizes the focused window using keyboard
    /// </summary>    [JSInvokable]
    public async Task ResizeWindowKeyboard(string direction, bool isLargeStep = false)
    {
        if (_currentFocusedWindow is not WindowBase windowBase) return;

        var step = isLargeStep ? 100 : 20; // Large resize step : Small resize step
        var windowInfo = _windowManager.GetWindow(windowBase.Id);
        if (windowInfo == null) return;

        var newBounds = windowInfo.Bounds;

        switch (direction.ToLower())
        {
            case "up":
                newBounds.Height = Math.Max(100, newBounds.Height - step);
                break;
            case "down":
                newBounds.Height += step;
                break;
            case "left":
                newBounds.Width = Math.Max(150, newBounds.Width - step);
                break;            case "right":
                newBounds.Width += step;
                break;
        }

        _windowManager.UpdateWindowBounds(windowBase.Id, newBounds);
    }    /// <summary>
    /// Sets focus to a specific window
    /// </summary>
    public async Task FocusWindow(ComponentBase window)
    {
        _currentFocusedWindow = window;
        if (window is WindowBase windowBase)
        {
            _windowManager.BringToFront(windowBase.Id);

            if (_jsModule != null)
            {
                await _jsModule.InvokeVoidAsync("focusWindow", windowBase.Id.ToString());
            }
        }
    }

    /// <summary>
    /// Gets the currently focused window
    /// </summary>
    public ComponentBase? GetFocusedWindow() => _currentFocusedWindow;    private async Task HighlightWindow(ComponentBase window)
    {
        if (_jsModule != null && window is WindowBase windowBase)
        {
            await _jsModule.InvokeVoidAsync("highlightWindow", windowBase.Id.ToString());
        }}    /// <summary>
    /// Closes the currently active window
    /// </summary>
    private async Task CloseActiveWindow()
    {
        if (_currentFocusedWindow is not WindowBase windowBase) return;
        _windowManager.UnregisterWindow(windowBase.Id);
        await Task.CompletedTask;
    }    /// <summary>
    /// Toggles maximize state of the currently active window
    /// </summary>
    private async Task ToggleMaximizeActiveWindow()
    {
        if (_currentFocusedWindow is not WindowBase windowBase) return;
        var windowInfo = _windowManager.GetWindow(windowBase.Id);
        if (windowInfo == null) return;        if (windowInfo.State == WindowState.Maximized)
        {
            await _windowManager.RestoreWindowAsync(windowBase.Id);
        }
        else
        {
            await _windowManager.MaximizeWindowAsync(windowBase.Id);
        }
    }    /// <summary>
    /// Minimizes the currently active window
    /// </summary>
    private async Task MinimizeActiveWindow()
    {
        if (_currentFocusedWindow is not WindowBase windowBase) return;
        await _windowManager.MinimizeWindowAsync(windowBase.Id);
    }    /// <summary>
    /// Cycles to the next window in the window order
    /// </summary>
    private async Task CycleToNextWindow()
    {
        var windows = _windowManager.GetAllWindows().Where(w => w.State != WindowState.Minimized).ToList();
        if (windows.Count <= 1) return;

        var currentIndex = _currentFocusedWindow is WindowBase windowBase ? windows.FindIndex(w => w.Id == windowBase.Id) : -1;
        var nextIndex = (currentIndex + 1) % windows.Count;
        
        if (windows[nextIndex].ComponentRef != null)
        {
            await FocusWindow(windows[nextIndex].ComponentRef);
        }
    }    /// <summary>
    /// Cycles to the previous window in the window order
    /// </summary>
    private async Task CycleToPreviousWindow()
    {
        var windows = _windowManager.GetAllWindows().Where(w => w.State != WindowState.Minimized).ToList();
        if (windows.Count <= 1) return;

        var currentIndex = _currentFocusedWindow is WindowBase windowBase ? windows.FindIndex(w => w.Id == windowBase.Id) : -1;
        var previousIndex = currentIndex <= 0 ? windows.Count - 1 : currentIndex - 1;
        
        if (windows[previousIndex].ComponentRef != null)
        {
            await FocusWindow(windows[previousIndex].ComponentRef);
        }
    }

    /// <summary>
    /// Shows the context menu for the currently active window
    /// </summary>
    private async Task ShowWindowContextMenu()
    {
        if (_currentFocusedWindow == null) return;
        // Trigger a context menu event - this would be handled by the window itself
        // For now, we'll just raise an event that components can listen to
        ShortcutTriggered?.Invoke(this, new KeyboardShortcutEventArgs 
        { 
            Shortcut = new KeyboardShortcut { Key = "ContextMenu", Modifiers = new List<string> { "Ctrl", "Shift" } },
            Handled = false 
        });
    }

    /// <summary>
    /// Gets direction from arrow key string
    /// </summary>
    private string GetDirectionFromArrowKey(string key)
    {
        return key switch
        {
            "ArrowUp" => "up",
            "ArrowDown" => "down",
            "ArrowLeft" => "left",
            "ArrowRight" => "right",
            _ => "unknown"
        };
    }

    private void OnWindowActiveChanged(object? sender, WindowFocusChangedEventArgs e)
    {
        _currentFocusedWindow = e.NewActiveWindow;
    }    private void OnWindowClosed(object? sender, WindowInfo windowInfo)
    {
        if (_currentFocusedWindow is WindowBase windowBase && windowBase.Id == windowInfo.Id)
        {
            _currentFocusedWindow = null;
        }

        // Remove from switcher if active
        if (_isWindowSwitcherActive)
        {
            var windowToRemove = _switcherWindowOrder.FirstOrDefault(w => w is WindowBase wb && wb.Id == windowInfo.Id);
            if (windowToRemove != null)
            {
                var removedIndex = _switcherWindowOrder.IndexOf(windowToRemove);
                _switcherWindowOrder.Remove(windowToRemove);

                if (_switcherWindowOrder.Count == 0)
                {
                    _ = DeactivateWindowSwitcher();
                }
                else if (removedIndex <= _switcherCurrentIndex && _switcherCurrentIndex > 0)
                {
                    _switcherCurrentIndex--;
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _windowManager.WindowActiveChanged -= OnWindowActiveChanged;
        _windowManager.WindowAfterClose -= OnWindowClosed;

        if (_jsModule != null)
        {
            await _jsModule.InvokeVoidAsync("dispose");
            await _jsModule.DisposeAsync();
        }
    }
}

/// <summary>
/// Configuration options for keyboard navigation
/// </summary>
public class KeyboardNavigationConfig
{
    /// <summary>
    /// Whether the Alt-Tab window switcher is enabled
    /// </summary>
    public bool EnableWindowSwitcher { get; set; } = true;

    /// <summary>
    /// Whether keyboard shortcuts for window management are enabled
    /// </summary>
    public bool EnableKeyboardShortcuts { get; set; } = true;

    /// <summary>
    /// Whether arrow key window movement is enabled
    /// </summary>
    public bool EnableArrowKeyMovement { get; set; } = true;

    /// <summary>
    /// Small movement step for arrow keys (pixels)
    /// </summary>
    public int SmallMovementStep { get; set; } = 10;

    /// <summary>
    /// Large movement step for arrow keys with modifier (pixels)
    /// </summary>
    public int LargeMovementStep { get; set; } = 50;

    /// <summary>
    /// Small resize step for arrow keys (pixels)
    /// </summary>
    public int SmallResizeStep { get; set; } = 10;

    /// <summary>
    /// Large resize step for arrow keys with modifier (pixels)
    /// </summary>
    public int LargeResizeStep { get; set; } = 50;

    /// <summary>
    /// Whether to show visual feedback during keyboard navigation
    /// </summary>
    public bool ShowVisualFeedback { get; set; } = true;
}

/// <summary>
/// Represents a keyboard shortcut
/// </summary>
public class KeyboardShortcut
{
    public string Key { get; set; } = string.Empty;
    public List<string> Modifiers { get; set; } = new();

    public override string ToString()
    {
        var parts = new List<string>(Modifiers) { Key };
        return string.Join("+", parts);
    }
}

/// <summary>
/// Event arguments for window switcher events
/// </summary>
public class WindowSwitcherEventArgs : EventArgs
{
    public List<ComponentBase> Windows { get; set; } = new();
    public int CurrentIndex { get; set; }
}

/// <summary>
/// Event arguments for keyboard shortcut events
/// </summary>
public class KeyboardShortcutEventArgs : EventArgs
{
    public KeyboardEventArgs EventArgs { get; set; } = new();
    public KeyboardShortcut? Shortcut { get; set; }
    public bool Handled { get; set; } = false;
}
