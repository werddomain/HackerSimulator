using BlazorWindowManager.Models;
using BlazorWindowManager.Services;
using Microsoft.AspNetCore.Components;

namespace BlazorWindowManager.Components;

/// <summary>
/// Represents a desktop area that can contain windows and other desktop elements
/// </summary>
public partial class DesktopArea : ComponentBase
{
    private ElementReference desktopElement;
    
    [Inject] public WindowManagerService WindowManager { get; set; } = default!;
    
    /// <summary>
    /// Content to be rendered within the desktop area
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
    
    /// <summary>
    /// CSS classes to apply to the desktop area
    /// </summary>
    [Parameter] public string? CssClass { get; set; }
    
    /// <summary>
    /// Width of the desktop area (defaults to 100%)
    /// </summary>
    [Parameter] public string Width { get; set; } = "100%";
    
    /// <summary>
    /// Height of the desktop area (defaults to 100vh)
    /// </summary>
    [Parameter] public string Height { get; set; } = "100vh";
    
    /// <summary>
    /// Background color or pattern for the desktop
    /// </summary>
    [Parameter] public string? Background { get; set; }
    
    /// <summary>
    /// Whether to show a grid pattern on the desktop
    /// </summary>
    [Parameter] public bool ShowGrid { get; set; } = false;
    
    /// <summary>
    /// Size of grid cells if ShowGrid is enabled
    /// </summary>
    [Parameter] public int GridSize { get; set; } = 20;
    
    protected override void OnInitialized()
    {
        // Subscribe to window manager events if needed
        // This could be used for managing window constraints within the desktop area
        base.OnInitialized();
    }
    
    private string GetDesktopStyle()
    {
        var styles = new List<string>
        {
            $"width: {Width}",
            $"height: {Height}"
        };
        
        if (!string.IsNullOrEmpty(Background))
        {
            styles.Add($"background: {Background}");
        }
        
        if (ShowGrid)
        {
            styles.Add($"background-image: " +
                      $"linear-gradient(rgba(0, 255, 0, 0.1) 1px, transparent 1px), " +
                      $"linear-gradient(90deg, rgba(0, 255, 0, 0.1) 1px, transparent 1px)");
            styles.Add($"background-size: {GridSize}px {GridSize}px");
        }
        
        return string.Join("; ", styles);
    }
    
    /// <summary>
    /// Gets the bounds of the desktop area for window constraint calculations
    /// </summary>
    /// <returns>WindowBounds representing the desktop area</returns>
    public async Task<WindowBounds> GetDesktopBounds()
    {
        // This could be enhanced to get actual DOM bounds
        return new WindowBounds(0, 0, 1920, 1080); // Default values
    }
}
