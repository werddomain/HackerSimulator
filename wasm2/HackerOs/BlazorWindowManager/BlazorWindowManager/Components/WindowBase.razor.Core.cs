using BlazorWindowManager.Models;
using BlazorWindowManager.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace BlazorWindowManager.Components;

/// <summary>
/// Base window component that provides core windowing functionality including
/// dragging, resizing, state management, and integration with WindowManagerService.
/// </summary>
public partial class WindowBase : ComponentBase, IWindowMessageReceiver, IAsyncDisposable
{
    #region Dependency Injection
    
    /// <summary>
    /// Window manager service for managing windows
    /// </summary>
    [Inject] protected WindowManagerService WindowManager { get; set; } = null!;
    
    /// <summary>
    /// JavaScript runtime for interop
    /// </summary>
    [Inject] protected IJSRuntime JSRuntime { get; set; } = null!;
    
    /// <summary>
    /// Snapping service for window snapping functionality
    /// </summary>
    [Inject] protected SnappingService SnappingService { get; set; } = null!;
    
    /// <summary>
    /// Service provider for dependency injection
    /// </summary>
    [Inject] protected IServiceProvider ServiceProvider { get; set; } = null!;
    
    #endregion
    
    #region Private Fields
    
    /// <summary>
    /// JavaScript module reference for window interactions
    /// </summary>
    protected IJSObjectReference? _jsModule;
    
    /// <summary>
    /// .NET object reference for JavaScript callbacks
    /// </summary>
    protected DotNetObjectReference<WindowBase>? _dotNetRef;
    
    /// <summary>
    /// Service scope for this window instance
    /// </summary>
    protected IServiceScope? _windowScope;
    
    /// <summary>
    /// Window information tracked by the window manager
    /// </summary>
    protected WindowInfo? _windowInfo;
    
    #endregion
    
    #region Public Properties
    
    /// <summary>
    /// Current bounds (position and size) of the window
    /// </summary>
    public WindowBounds CurrentBounds { get; protected set; } = new();
    
    /// <summary>
    /// Current state of the window (Normal, Minimized, Maximized)
    /// </summary>
    public WindowState CurrentState { get; protected set; } = WindowState.Normal;
    
    /// <summary>
    /// Whether this window is currently active (has focus)
    /// </summary>
    public bool IsActive { get; protected set; } = false;
    
    /// <summary>
    /// Service scope for this window instance
    /// </summary>
    public IServiceCollection WindowContext { get; private set; } = new ServiceCollection();
      #endregion
      #region Helper Methods
    
    /// <summary>
    /// Gets the bounds of the desktop container for snapping calculations
    /// </summary>
    /// <returns>The bounds of the container where windows can be placed</returns>
    protected virtual async Task<WindowBounds> GetContainerBounds()
    {
        // Use JS interop to get the desktop area bounds
        if (_jsModule != null)
        {
            try
            {
                // Call the JS function without parameters to get full window bounds
                var jsResult = await _jsModule.InvokeAsync<dynamic>("getDesktopBounds", (object?)null);
                
                return new WindowBounds
                {
                    Left = jsResult.left ?? 0,
                    Top = jsResult.top ?? 0,
                    Width = jsResult.width ?? 1920,
                    Height = jsResult.height ?? 1040
                };
            }
            catch
            {
                // Fall back to default bounds if JS call fails
            }
        }
        
        // Default fallback - assume full screen minus taskbar
        return new WindowBounds
        {
            Left = 0,
            Top = 0,
            Width = 1920, // Default screen width
            Height = 1040 // Default screen height minus taskbar
        };
    }
    
    #endregion
    
    #region IWindowMessageReceiver Implementation
    
    /// <summary>
    /// Handles messages received from other windows
    /// </summary>
    /// <param name="args">Message event arguments</param>
    public virtual void OnMessageReceived(WindowMessageEventArgs args)
    {
        // Override in derived classes to handle inter-window messages
    }
    
    #endregion
}
