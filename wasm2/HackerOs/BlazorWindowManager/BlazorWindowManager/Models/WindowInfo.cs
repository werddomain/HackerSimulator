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
    public Guid Id { get; internal set; }
    
    /// <summary>
    /// User-defined name for the window (optional)
    /// </summary>
    public string? Name { get; internal set; }
    
    /// <summary>
    /// Display title of the window
    /// </summary>
    public string Title { get; internal set; } = string.Empty;
    
    /// <summary>
    /// Icon render fragment for the window (used in title bar and taskbar)
    /// </summary>
    public RenderFragment? Icon { get; internal set; }
    
    /// <summary>
    /// Current state of the window
    /// </summary>
    public WindowState State { get; internal set; } = WindowState.Normal;
    
    /// <summary>
    /// Current bounds of the window
    /// </summary>
    public WindowBounds Bounds { get; internal set; } = new();
    
    /// <summary>
    /// Bounds before the window was maximized (for restore functionality)
    /// </summary>
    public WindowBounds? RestoreBounds { get; internal set; }
    
    /// <summary>
    /// Current z-index of the window
    /// </summary>
    public int ZIndex { get; internal set; } = 1;
    
    /// <summary>
    /// Whether this window is currently the active (focused) window
    /// </summary>
    public bool IsActive { get; internal set; } = false;
    
    /// <summary>
    /// Whether the window can be resized
    /// </summary>
    public bool Resizable { get; internal set; } = true;
    
    /// <summary>
    /// Minimum width constraint
    /// </summary>
    public double? MinWidth { get; internal set; }
    
    /// <summary>
    /// Minimum height constraint
    /// </summary>
    public double? MinHeight { get; internal set; }
    
    /// <summary>
    /// Maximum width constraint
    /// </summary>
    public double? MaxWidth { get; internal set; }
    
    /// <summary>
    /// Maximum height constraint
    /// </summary>
    public double? MaxHeight { get; internal set; }
    
    /// <summary>
    /// Reference to the actual window component
    /// </summary>
    public ComponentBase? ComponentRef { get; internal set; }
    
    /// <summary>
    /// When the window was created
    /// </summary>
    public DateTime CreatedAt { get; internal set; } = DateTime.UtcNow;
      /// <summary>
    /// Type name of the window component (useful for grouping)
    /// </summary>
    public string ComponentType { get; internal set; } = string.Empty;
    
    /// <summary>
    /// Parameters to pass to the window component when creating it dynamically
    /// </summary>
    public Dictionary<string, object> Parameters { get; internal set; } = new();
}
