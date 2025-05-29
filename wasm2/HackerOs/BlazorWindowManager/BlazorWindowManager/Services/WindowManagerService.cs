using BlazorWindowManager.Components;
using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorWindowManager.Services;

/// <summary>
/// Core service for managing windows in the Blazor Window Manager system.
/// Handles window registration, z-index management, and global events.
/// </summary>
public class WindowManagerService
{
    private readonly List<WindowInfo> _windows = new();
    private int _nextZIndex = 1000;
    private WindowInfo? _activeWindow;
    private IServiceProvider serviceProvider;
    private readonly object _lock = new();
    public WindowManagerService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }
    #region Events

    /// <summary>
    /// Raised when a new window is created and registered
    /// </summary>
    public event EventHandler<WindowInfo>? WindowCreated;
    
    /// <summary>
    /// Raised before a window is closed (cancellable)
    /// </summary>
    public event EventHandler<WindowCancelEventArgs>? WindowBeforeClose;
    
    /// <summary>
    /// Raised when a window close operation is cancelled
    /// </summary>
    public event EventHandler<WindowInfo>? WindowCloseCancelled;
    
    /// <summary>
    /// Raised after a window is closed and removed
    /// </summary>
    public event EventHandler<WindowInfo>? WindowAfterClose;
    
    /// <summary>
    /// Raised when a window's state changes
    /// </summary>
    public event EventHandler<WindowStateChangedEventArgs>? WindowStateChanged;
    
    /// <summary>
    /// Raised when the active window changes
    /// </summary>
    public event EventHandler<WindowFocusChangedEventArgs>? WindowActiveChanged;
    
    /// <summary>
    /// Raised when a window's bounds change (move/resize)
    /// </summary>
    public event EventHandler<WindowBoundsChangedEventArgs>? WindowBoundsChanged;
    
    /// <summary>
    /// Raised when a window is registered with the manager
    /// </summary>
    public event EventHandler<WindowEventArgs>? WindowRegistered;
    
    /// <summary>
    /// Raised when a window is unregistered from the manager
    /// </summary>
    public event EventHandler<WindowEventArgs>? WindowUnregistered;
    
    /// <summary>
    /// Raised when a window gains focus
    /// </summary>
    public event EventHandler<WindowEventArgs>? WindowFocused;
      /// <summary>
    /// Raised when a window's title changes
    /// </summary>
    public event EventHandler<WindowTitleChangedEventArgs>? WindowTitleChanged;
    
    /// <summary>
    /// Raised when a window is opened
    /// </summary>
    public event EventHandler<WindowInfo>? WindowOpened;
    
    /// <summary>
    /// Raised when a window is closed
    /// </summary>
    public event EventHandler<WindowInfo>? WindowClosed;
    
    #endregion

    #region Window Registration
    
    /// <summary>
    /// Registers a new window with the window manager
    /// </summary>
    /// <param name="window">The window component to register</param>
    /// <param name="id">Unique identifier for the window</param>
    /// <param name="title">Initial title of the window</param>
    /// <param name="name">Optional name for the window</param>
    /// <returns>WindowInfo object containing the window's information</returns>
    public WindowInfo RegisterWindow(ComponentBase window, Guid id, string title, string? name = null)
    {
        lock (_lock)
        {
            var windowInfo = new WindowInfo
            {
                Id = id,
                Name = name,
                Title = title,
                ComponentRef = window,
                ComponentType = window.GetType().Name,
                ZIndex = _nextZIndex++,
                Bounds = CalculateInitialPosition()
            };
            
            _windows.Add(windowInfo);
            
            // If this is the first window or no window is currently active, make it active
            if (_activeWindow == null)
            {
                SetActiveWindow(windowInfo);
            }
              WindowCreated?.Invoke(this, windowInfo);
            WindowRegistered?.Invoke(this, new WindowEventArgs { Window = windowInfo });
            return windowInfo;
        }
    }
    
    /// <summary>
    /// Unregisters a window from the window manager
    /// </summary>
    /// <param name="windowId">ID of the window to unregister</param>
    /// <param name="force">Whether to force close without checking for cancellation</param>
    /// <returns>True if the window was successfully unregistered</returns>
    public bool UnregisterWindow(Guid windowId, bool force = false)
    {
        lock (_lock)
        {
            var window = _windows.FirstOrDefault(w => w.Id == windowId);
            if (window == null) return false;
            
            // Check if close should be cancelled (unless forced)
            if (!force)
            {
                var cancelArgs = new WindowCancelEventArgs { Window = window.ComponentRef };
                WindowBeforeClose?.Invoke(this, cancelArgs);
                
                if (cancelArgs.Cancel)
                {
                    WindowCloseCancelled?.Invoke(this, window);
                    return false;
                }
            }
            
            _windows.Remove(window);
            
            // If this was the active window, find a new active window
            if (_activeWindow?.Id == windowId)
            {
                var newActiveWindow = _windows.LastOrDefault();
                SetActiveWindow(newActiveWindow);            }
              WindowAfterClose?.Invoke(this, window);
            WindowClosed?.Invoke(this, window);
            WindowUnregistered?.Invoke(this, new WindowEventArgs { Window = window });
            return true;
        }
    }
    
    #endregion
    
    #region Window Management
    
    /// <summary>
    /// Gets information about a specific window
    /// </summary>
    /// <param name="windowId">ID of the window to get</param>
    /// <returns>WindowInfo if found, null otherwise</returns>
    public WindowInfo? GetWindow(Guid windowId)
    {
        lock (_lock)
        {
            return _windows.FirstOrDefault(w => w.Id == windowId);
        }
    }
    
    /// <summary>
    /// Gets a list of all currently registered windows
    /// </summary>
    /// <returns>Read-only list of WindowInfo objects</returns>
    public IReadOnlyList<WindowInfo> GetAllWindows()
    {
        lock (_lock)
        {
            return _windows.ToList().AsReadOnly();
        }
    }
    
    /// <summary>
    /// Gets the currently active window
    /// </summary>
    /// <returns>WindowInfo of the active window, or null if no window is active</returns>
    public WindowInfo? GetActiveWindow()
    {
        lock (_lock)
        {
            return _activeWindow;
        }
    }
    
    /// <summary>
    /// Brings a window to the front and makes it active
    /// </summary>
    /// <param name="windowId">ID of the window to bring to front</param>
    /// <returns>True if successful</returns>
    public bool BringToFront(Guid windowId)
    {
        lock (_lock)
        {
            var window = _windows.FirstOrDefault(w => w.Id == windowId);
            if (window == null) return false;
            
            window.ZIndex = _nextZIndex++;
            SetActiveWindow(window);
            return true;
        }
    }
    
    /// <summary>
    /// Updates the state of a window
    /// </summary>
    /// <param name="windowId">ID of the window to update</param>
    /// <param name="newState">New state for the window</param>
    /// <returns>True if successful</returns>
    public bool UpdateWindowState(Guid windowId, WindowState newState)
    {
        lock (_lock)
        {
            var window = _windows.FirstOrDefault(w => w.Id == windowId);
            if (window == null) return false;
            
            var oldState = window.State;
            if (oldState == newState) return true;
            
            // Store restore bounds when maximizing
            if (newState == WindowState.Maximized && oldState == WindowState.Normal)
            {
                window.RestoreBounds = window.Bounds.Clone();
            }
            
            window.State = newState;
            
            var args = new WindowStateChangedEventArgs(window.Id, window.ComponentRef!, oldState, newState);
            WindowStateChanged?.Invoke(this, args);
            return true;
        }
    }
    
    /// <summary>
    /// Updates the bounds of a window
    /// </summary>
    /// <param name="windowId">ID of the window to update</param>
    /// <param name="newBounds">New bounds for the window</param>
    /// <returns>True if successful</returns>
    public bool UpdateWindowBounds(Guid windowId, WindowBounds newBounds)
    {
        lock (_lock)
        {
            var window = _windows.FirstOrDefault(w => w.Id == windowId);
            if (window == null) return false;
            
            var oldBounds = window.Bounds.Clone();
            window.Bounds = newBounds;
            
            var args = new WindowBoundsChangedEventArgs(window.ComponentRef!, oldBounds, newBounds);
            WindowBoundsChanged?.Invoke(this, args);
            return true;
        }
    }
    
    /// <summary>
    /// Updates the title of a window
    /// </summary>
    /// <param name="windowId">ID of the window to update</param>    /// <param name="newTitle">New title for the window</param>
    /// <returns>True if successful</returns>
    public bool UpdateWindowTitle(Guid windowId, string newTitle)
    {
        lock (_lock)
        {
            var window = _windows.FirstOrDefault(w => w.Id == windowId);
            if (window == null) return false;
            
            var oldTitle = window.Title;
            window.Title = newTitle;
            
            // Fire the title changed event
            var args = new WindowTitleChangedEventArgs(windowId, oldTitle, newTitle);
            WindowTitleChanged?.Invoke(this, args);
            return true;
        }
    }
    
    #endregion
    
    #region Inter-Window Communication
    
    /// <summary>
    /// Sends a message from one window to another
    /// </summary>
    /// <param name="sourceWindowId">ID of the sending window</param>
    /// <param name="targetWindowId">ID of the receiving window</param>
    /// <param name="message">Message payload to send</param>
    /// <returns>True if the message was delivered</returns>
    public bool SendMessage(Guid sourceWindowId, Guid targetWindowId, object message)
    {
        lock (_lock)
        {
            var targetWindow = _windows.FirstOrDefault(w => w.Id == targetWindowId);
            if (targetWindow?.ComponentRef == null) return false;
            
            // For now, we'll implement a simple message delivery
            // In a real implementation, you might want to use reflection or interfaces
            // to call methods on the target component
            
            var args = new WindowMessageEventArgs(sourceWindowId, message);
            
            // If the target component implements IWindowMessageReceiver, call it
            if (targetWindow.ComponentRef is IWindowMessageReceiver receiver)
            {
                receiver.OnMessageReceived(args);
                return true;
            }
            
            return false;
        }
    }
    
    #endregion
    
    #region Private Methods
    
    /// <summary>
    /// Sets the active window and raises appropriate events
    /// </summary>
    private void SetActiveWindow(WindowInfo? newActiveWindow)
    {
        var previousActive = _activeWindow;
        
        // Update active state
        if (_activeWindow != null)
        {
            _activeWindow.IsActive = false;
        }
        
        _activeWindow = newActiveWindow;
        
        if (_activeWindow != null)
        {
            _activeWindow.IsActive = true;
        }
          // Raise event
        var args = new WindowFocusChangedEventArgs(
            _activeWindow?.ComponentRef, 
            previousActive?.ComponentRef
        );
        WindowActiveChanged?.Invoke(this, args);
        
        // Fire focused event for TaskBar
        if (_activeWindow != null)
        {
            WindowFocused?.Invoke(this, new WindowEventArgs { Window = _activeWindow });
        }
    }
    
    /// <summary>
    /// Calculates the initial position for a new window using cascading logic
    /// </summary>
    private WindowBounds CalculateInitialPosition()
    {
        const double defaultWidth = 600;
        const double defaultHeight = 400;
        const double titleBarHeight = 32;
        const double cascadeOffset = titleBarHeight * 2;
        
        // If no windows exist, center the first window
        if (_windows.Count == 0)
        {
            return new WindowBounds(
                left: 100, // We'll adjust this based on container size later
                top: 100,
                width: defaultWidth,
                height: defaultHeight
            );
        }
        
        // Use cascading positioning for subsequent windows
        var lastWindow = _windows.LastOrDefault();
        if (lastWindow != null)
        {
            var newLeft = lastWindow.Bounds.Left + cascadeOffset;
            var newTop = lastWindow.Bounds.Top + titleBarHeight;
            
            // Reset to top-left if we've cascaded too far
            if (newLeft > 800 || newTop > 600) // These would be adjusted based on container
            {
                newLeft = 50;
                newTop = 50;
            }
            
            return new WindowBounds(newLeft, newTop, defaultWidth, defaultHeight);
        }
        
        return new WindowBounds(100, 100, defaultWidth, defaultHeight);
    }
    
    #endregion
      #region Async Window Operations
    
    /// <summary>
    /// Asynchronously opens a new window
    /// </summary>
    /// <param name="window">The window component to register</param>
    /// <param name="id">Unique identifier for the window</param>
    /// <param name="title">Initial title of the window</param>
    /// <param name="name">Optional name for the window</param>
    /// <returns>Task that completes with WindowInfo when the operation is done</returns>
    public async Task<WindowInfo> OpenWindowAsync(ComponentBase window, Guid id, string title, string? name = null)
    {
        return await Task.Run(() => {
            var windowInfo = RegisterWindow(window, id, title, name);
            WindowOpened?.Invoke(this, windowInfo);
            return windowInfo;
        });
    }

    /// <summary>
    /// Asynchronously opens a new window
    /// </summary>
    /// <param name="id">Unique identifier for the window</param>
    /// <param name="title">Initial title of the window</param>
    /// <param name="name">Optional name for the window</param>
    /// <returns>Task that completes with WindowInfo when the operation is done</returns>
    public async Task<(WindowInfo info, WindowBase window)> OpenWindowAsync<T>() where T : WindowBase
    {
        var window = ActivatorUtilities.CreateInstance<T>(serviceProvider);
        return (await OpenWindowAsync(window, Guid.NewGuid(), window.Title, window.Name), window);
    }

    /// <summary>
    /// Asynchronously closes a window
    /// </summary>
    /// <param name="windowId">ID of the window to close</param>
    /// <param name="force">Whether to force close without checking for cancellation</param>
    /// <returns>Task that completes when the operation is done</returns>
    public async Task<bool> CloseWindowAsync(Guid windowId, bool force = false)
    {
        return await Task.Run(() => UnregisterWindow(windowId, force));
    }
    
    /// <summary>
    /// Asynchronously minimizes a window
    /// </summary>
    /// <param name="windowId">ID of the window to minimize</param>
    /// <returns>Task that completes when the operation is done</returns>
    public async Task<bool> MinimizeWindowAsync(Guid windowId)
    {
        return await Task.Run(() => UpdateWindowState(windowId, WindowState.Minimized));
    }
    
    /// <summary>
    /// Asynchronously maximizes a window
    /// </summary>
    /// <param name="windowId">ID of the window to maximize</param>
    /// <returns>Task that completes when the operation is done</returns>
    public async Task<bool> MaximizeWindowAsync(Guid windowId)
    {
        return await Task.Run(() => UpdateWindowState(windowId, WindowState.Maximized));
    }
    
    /// <summary>
    /// Asynchronously restores a window to normal state
    /// </summary>
    /// <param name="windowId">ID of the window to restore</param>
    /// <returns>Task that completes when the operation is done</returns>
    public async Task<bool> RestoreWindowAsync(Guid windowId)
    {
        return await Task.Run(() => UpdateWindowState(windowId, WindowState.Normal));
    }
    
    /// <summary>
    /// Asynchronously focuses a window
    /// </summary>
    /// <param name="windowId">ID of the window to focus</param>
    /// <returns>Task that completes when the operation is done</returns>
    public async Task<bool> FocusWindowAsync(Guid windowId)
    {
        return await Task.Run(() => 
        {
            var window = GetWindow(windowId);
            if (window == null) return false;
            
            SetActiveWindow(window);
            return true;
        });
    }
    
    #endregion
}

/// <summary>
/// Interface for components that can receive inter-window messages
/// </summary>
public interface IWindowMessageReceiver
{
    /// <summary>
    /// Called when a message is received from another window
    /// </summary>
    /// <param name="args">Message event arguments</param>
    void OnMessageReceived(WindowMessageEventArgs args);
}
