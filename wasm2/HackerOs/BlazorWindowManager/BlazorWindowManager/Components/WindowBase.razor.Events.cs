using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorWindowManager.Components;

/// <summary>
/// WindowBase partial class - Event parameters and handling
/// </summary>
public partial class WindowBase
{
    #region Event Parameters
    
    /// <summary>
    /// Raised before the window is closed (cancellable)
    /// </summary>
    [Parameter] public EventCallback<WindowCancelEventArgs> OnBeforeClose { get; set; }
    
    /// <summary>
    /// Raised after the window is closed
    /// </summary>
    [Parameter] public EventCallback OnAfterClose { get; set; }
    
    /// <summary>
    /// Raised when the window gains focus
    /// </summary>
    [Parameter] public EventCallback OnFocus { get; set; }
    
    /// <summary>
    /// Raised when the window loses focus
    /// </summary>
    [Parameter] public EventCallback OnBlur { get; set; }
    
    /// <summary>
    /// Raised when the window is moved
    /// </summary>
    [Parameter] public EventCallback<WindowBounds> OnMoved { get; set; }
    
    /// <summary>
    /// Raised while the window is being moved
    /// </summary>
    [Parameter] public EventCallback<WindowBounds> OnMoving { get; set; }
    
    /// <summary>
    /// Raised when the window is resized
    /// </summary>
    [Parameter] public EventCallback<WindowBounds> OnResized { get; set; }
    
    /// <summary>
    /// Raised while the window is being resized
    /// </summary>
    [Parameter] public EventCallback<WindowBounds> OnResizing { get; set; }
    
    /// <summary>
    /// Raised when the window state changes
    /// </summary>
    [Parameter] public EventCallback<WindowState> OnStateChanged { get; set; }
    
    /// <summary>
    /// Raised when the window title changes
    /// </summary>
    [Parameter] public EventCallback<string> OnTitleChanged { get; set; }
    
    /// <summary>
    /// Raised when the window content is loaded
    /// </summary>
    [Parameter] public EventCallback OnContentLoaded { get; set; }
    
    #endregion
}
