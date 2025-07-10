namespace HackerOs.OS.Windows;

/// <summary>
/// Interface for a window
/// </summary>
public interface IWindow
{
    /// <summary>
    /// Window ID
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Window title
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// Window icon path
    /// </summary>
    string? IconPath { get; set; }

    /// <summary>
    /// Current window state
    /// </summary>
    WindowState State { get; }

    /// <summary>
    /// Window position
    /// </summary>
    WindowPosition Position { get; }

    /// <summary>
    /// Window size
    /// </summary>
    WindowSize Size { get; }

    /// <summary>
    /// Window style options
    /// </summary>
    WindowStyle Style { get; }

    /// <summary>
    /// Whether the window is active/focused
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Whether the window is visible
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Window content
    /// </summary>
    object? Content { get; }

    /// <summary>
    /// Activates the window
    /// </summary>
    void Activate();

    /// <summary>
    /// Minimizes the window
    /// </summary>
    void Minimize();

    /// <summary>
    /// Maximizes the window
    /// </summary>
    void Maximize();

    /// <summary>
    /// Restores the window
    /// </summary>
    void Restore();

    /// <summary>
    /// Closes the window
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// Shows the window
    /// </summary>
    void Show();

    /// <summary>
    /// Hides the window
    /// </summary>
    void Hide();

    /// <summary>
    /// Sets the window position
    /// </summary>
    void SetPosition(int left, int top);

    /// <summary>
    /// Sets the window size
    /// </summary>
    void SetSize(int width, int height);

    /// <summary>
    /// Centers the window
    /// </summary>
    void Center();

    /// <summary>
    /// Brings window to front
    /// </summary>
    void BringToFront();

    /// <summary>
    /// Sends window to back
    /// </summary>
    void SendToBack();

    /// <summary>
    /// Event raised when window state changes
    /// </summary>
    event EventHandler<WindowStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when window is activated
    /// </summary>
    event EventHandler? Activated;

    /// <summary>
    /// Event raised when window is deactivated
    /// </summary>
    event EventHandler? Deactivated;

    /// <summary>
    /// Event raised when window is closing
    /// </summary>
    event EventHandler<WindowClosingEventArgs>? Closing;

    /// <summary>
    /// Event raised when window is closed
    /// </summary>
    event EventHandler? Closed;
}

/// <summary>
/// Event args for window closing
/// </summary>
public class WindowClosingEventArgs : EventArgs
{
    /// <summary>
    /// Whether to cancel closing
    /// </summary>
    public bool Cancel { get; set; }
}
