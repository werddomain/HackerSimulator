namespace BlazorWindowManager.Models;

/// <summary>
/// Represents the possible states of a window in the window manager system.
/// </summary>
public enum WindowState
{
    /// <summary>
    /// Window is in its normal state - visible and resizable/movable
    /// </summary>
    Normal = 0,
    
    /// <summary>
    /// Window is minimized and not visible in the main desktop area
    /// </summary>
    Minimized = 1,
    
    /// <summary>
    /// Window is maximized to fill the entire available container space
    /// </summary>
    Maximized = 2
}
