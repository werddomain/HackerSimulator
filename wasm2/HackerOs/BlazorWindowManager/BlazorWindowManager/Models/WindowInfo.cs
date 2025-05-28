using Microsoft.AspNetCore.Components;

namespace BlazorWindowManager.Models;

/// <summary>
/// Contains information about a window managed by the WindowManagerService
/// </summary>
public class WindowInfo
{
    /// <summary>
    /// Unique identifier for the window
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// User-defined name for the window (optional)
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Display title of the window
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Icon render fragment for the window (used in title bar and taskbar)
    /// </summary>
    public RenderFragment? Icon { get; set; }
    
    /// <summary>
    /// Current state of the window
    /// </summary>
    public WindowState State { get; set; } = WindowState.Normal;
    
    /// <summary>
    /// Current bounds of the window
    /// </summary>
    public WindowBounds Bounds { get; set; } = new();
    
    /// <summary>
    /// Bounds before the window was maximized (for restore functionality)
    /// </summary>
    public WindowBounds? RestoreBounds { get; set; }
    
    /// <summary>
    /// Current z-index of the window
    /// </summary>
    public int ZIndex { get; set; } = 1;
    
    /// <summary>
    /// Whether this window is currently the active (focused) window
    /// </summary>
    public bool IsActive { get; set; } = false;
    
    /// <summary>
    /// Whether the window can be resized
    /// </summary>
    public bool Resizable { get; set; } = true;
    
    /// <summary>
    /// Minimum width constraint
    /// </summary>
    public double? MinWidth { get; set; }
    
    /// <summary>
    /// Minimum height constraint
    /// </summary>
    public double? MinHeight { get; set; }
    
    /// <summary>
    /// Maximum width constraint
    /// </summary>
    public double? MaxWidth { get; set; }
    
    /// <summary>
    /// Maximum height constraint
    /// </summary>
    public double? MaxHeight { get; set; }
    
    /// <summary>
    /// Reference to the actual window component
    /// </summary>
    public ComponentBase? ComponentRef { get; set; }
    
    /// <summary>
    /// When the window was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Type name of the window component (useful for grouping)
    /// </summary>
    public string ComponentType { get; set; } = string.Empty;
}
