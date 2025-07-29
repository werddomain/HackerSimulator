using Microsoft.AspNetCore.Components;

namespace HackerOs.OS.Windows;

/// <summary>
/// Interface for managing windows
/// </summary>
public interface IWindowManager
{
    /// <summary>
    /// Opens a new window
    /// </summary>
    Task<IWindow> OpenWindowAsync(WindowOptions options);

    /// <summary>
    /// Gets a window by ID
    /// </summary>
    IWindow? GetWindow(string windowId);

    /// <summary>
    /// Closes a window
    /// </summary>
    Task CloseWindowAsync(string windowId);

    /// <summary>
    /// Gets all open windows
    /// </summary>
    IEnumerable<IWindow> GetWindows();

    /// <summary>
    /// Activates (brings to front) a window
    /// </summary>
    void ActivateWindow(string windowId);

    /// <summary>
    /// Minimizes a window
    /// </summary>
    void MinimizeWindow(string windowId);

    /// <summary>
    /// Maximizes a window
    /// </summary>
    void MaximizeWindow(string windowId);

    /// <summary>
    /// Restores a minimized/maximized window
    /// </summary>
    void RestoreWindow(string windowId);

    /// <summary>
    /// Event raised when a window is opened
    /// </summary>
    event EventHandler<WindowEventArgs>? WindowOpened;

    /// <summary>
    /// Event raised when a window is closed
    /// </summary>
    event EventHandler<WindowEventArgs>? WindowClosed;

    /// <summary>
    /// Event raised when a window is activated
    /// </summary>
    event EventHandler<WindowEventArgs>? WindowActivated;

    /// <summary>
    /// Event raised when a window state changes
    /// </summary>
    event EventHandler<WindowStateChangedEventArgs>? WindowStateChanged;
}

/// <summary>
/// Options for creating a window
/// </summary>
public class WindowOptions
{
    /// <summary>
    /// Window title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Window icon path
    /// </summary>
    public string? IconPath { get; set; }

    /// <summary>
    /// Window content component
    /// </summary>
    public Type? ContentType { get; set; }

    /// <summary>
    /// Window parameters to pass to content
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Initial window position
    /// </summary>
    public WindowPosition Position { get; set; } = WindowPosition.Default;

    /// <summary>
    /// Initial window size
    /// </summary>
    public WindowSize Size { get; set; } = WindowSize.Default;

    /// <summary>
    /// Initial window state
    /// </summary>
    public WindowState State { get; set; } = WindowState.Normal;

    /// <summary>
    /// Window style options
    /// </summary>
    public WindowStyle Style { get; set; } = WindowStyle.Default;
}

/// <summary>
/// Window position
/// </summary>
public class WindowPosition
{
    public static WindowPosition Default => new() { Left = 100, Top = 100 };
    public static WindowPosition Center => new() { IsCentered = true };

    public int Left { get; set; }
    public int Top { get; set; }
    public bool IsCentered { get; set; }
}

/// <summary>
/// Window size
/// </summary>
public class WindowSize
{
    public static WindowSize Default => new() { Width = 800, Height = 600 };
    public static WindowSize Maximized => new() { IsMaximized = true };

    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsMaximized { get; set; }
}

/// <summary>
/// Window state
/// </summary>
public enum WindowState
{
    Normal,
    Minimized,
    Maximized
}

/// <summary>
/// Window style options
/// </summary>
public class WindowStyle
{
    public static WindowStyle Default => new();

    /// <summary>
    /// Whether the window can be resized
    /// </summary>
    public bool CanResize { get; set; } = true;

    /// <summary>
    /// Whether the window can be minimized
    /// </summary>
    public bool CanMinimize { get; set; } = true;

    /// <summary>
    /// Whether the window can be maximized
    /// </summary>
    public bool CanMaximize { get; set; } = true;

    /// <summary>
    /// Whether the window should show in taskbar
    /// </summary>
    public bool ShowInTaskbar { get; set; } = true;

    /// <summary>
    /// Whether the window is always on top
    /// </summary>
    public bool AlwaysOnTop { get; set; }

    /// <summary>
    /// Window border style
    /// </summary>
    public WindowBorderStyle BorderStyle { get; set; } = WindowBorderStyle.Sizable;
}

/// <summary>
/// Window border style
/// </summary>
public enum WindowBorderStyle
{
    None,
    Fixed,
    Sizable
}

/// <summary>
/// Event args for window events
/// </summary>
public class WindowEventArgs : EventArgs
{
    public IWindow Window { get; }

    public WindowEventArgs(IWindow window)
    {
        Window = window;
    }
}

/// <summary>
/// Event args for window state changes
/// </summary>
public class WindowStateChangedEventArgs : WindowEventArgs
{
    public WindowState OldState { get; }
    public WindowState NewState { get; }

    public WindowStateChangedEventArgs(IWindow window, WindowState oldState, WindowState newState)
        : base(window)
    {
        OldState = oldState;
        NewState = newState;
    }
}
