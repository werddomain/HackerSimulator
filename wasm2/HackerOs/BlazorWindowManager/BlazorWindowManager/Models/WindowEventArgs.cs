using Microsoft.AspNetCore.Components;

namespace BlazorWindowManager.Models;

/// <summary>
/// Event arguments for window-related events that can be cancelled
/// </summary>
public class WindowCancelEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets whether the operation should be cancelled
    /// </summary>
    public bool Cancel { get; set; } = false;
    
    /// <summary>
    /// The window associated with this event
    /// </summary>
    public ComponentBase? Window { get; set; }
    public bool Force { get; internal set; }
}

/// <summary>
/// Event arguments for window state change events
/// </summary>
public class WindowStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// The ID of the window that changed state
    /// </summary>
    public Guid WindowId { get; }
    
    /// <summary>
    /// The window that changed state
    /// </summary>
    public ComponentBase Window { get; }
    
    /// <summary>
    /// The previous state of the window
    /// </summary>
    public WindowState OldState { get; }
    
    /// <summary>
    /// The new state of the window
    /// </summary>
    public WindowState NewState { get; }
    
    public WindowStateChangedEventArgs(Guid windowId, ComponentBase window, WindowState oldState, WindowState newState)
    {
        WindowId = windowId;
        Window = window;
        OldState = oldState;
        NewState = newState;
    }
}

/// <summary>
/// Event arguments for window focus change events
/// </summary>
public class WindowFocusChangedEventArgs : EventArgs
{
    /// <summary>
    /// The window that gained focus (null if no window has focus)
    /// </summary>
    public ComponentBase? NewActiveWindow { get; }
    
    /// <summary>
    /// The window that lost focus (null if no previous window had focus)
    /// </summary>
    public ComponentBase? PreviousActiveWindow { get; }
    
    public WindowFocusChangedEventArgs(ComponentBase? newActiveWindow, ComponentBase? previousActiveWindow)
    {
        NewActiveWindow = newActiveWindow;
        PreviousActiveWindow = previousActiveWindow;
    }
}

/// <summary>
/// Event arguments for window bounds change events (moving/resizing)
/// </summary>
public class WindowBoundsChangedEventArgs : EventArgs
{
    /// <summary>
    /// The window that moved or was resized
    /// </summary>
    public ComponentBase Window { get; }
    
    /// <summary>
    /// The previous bounds of the window
    /// </summary>
    public WindowBounds OldBounds { get; }
    
    /// <summary>
    /// The new bounds of the window
    /// </summary>
    public WindowBounds NewBounds { get; }
    
    public WindowBoundsChangedEventArgs(ComponentBase window, WindowBounds oldBounds, WindowBounds newBounds)
    {
        Window = window;
        OldBounds = oldBounds;
        NewBounds = newBounds;
    }
}

/// <summary>
/// Event arguments for inter-window message events
/// </summary>
public class WindowMessageEventArgs : EventArgs
{
    /// <summary>
    /// The ID of the window that sent the message
    /// </summary>
    public Guid SourceWindowId { get; }
    
    /// <summary>
    /// The message payload
    /// </summary>
    public object Message { get; }
    
    public WindowMessageEventArgs(Guid sourceWindowId, object message)
    {
        SourceWindowId = sourceWindowId;
        Message = message;
    }
}

/// <summary>
/// General event arguments for window events
/// </summary>
public class WindowEventArgs : EventArgs
{
    /// <summary>
    /// The window information associated with this event
    /// </summary>
    public WindowInfo? Window { get; set; }
}

/// <summary>
/// Event arguments for window title change events
/// </summary>
public class WindowTitleChangedEventArgs : EventArgs
{
    /// <summary>
    /// The ID of the window whose title changed
    /// </summary>
    public Guid WindowId { get; }
    
    /// <summary>
    /// The old title of the window
    /// </summary>
    public string OldTitle { get; }
    
    /// <summary>
    /// The new title of the window
    /// </summary>
    public string NewTitle { get; }
    
    public WindowTitleChangedEventArgs(Guid windowId, string oldTitle, string newTitle)
    {
        WindowId = windowId;
        OldTitle = oldTitle;
        NewTitle = newTitle;
    }
}
