using BlazorWindowManager;
using BlazorWindowManager.Components;
using BlazorWindowManager.Services;
using HackerOs.OS.Applications.Lifecycle;
using HackerOs.OS.Applications.Registry;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace HackerOs.OS.Applications.UI;

/// <summary>
/// Base class for window-based applications
/// </summary>
public abstract class WindowApplicationBase : ApplicationBase
{
    /// <summary>
    /// Service provider for dependency injection
    /// </summary>
    [Inject] protected new IServiceProvider ServiceProvider { get; set; } = null!;
    
    /// <summary>
    /// Window manager service
    /// </summary>
    [Inject] protected new WindowManagerService WindowManager { get; set; } = null!;
    
    /// <summary>
    /// The window for this application
    /// </summary>
    protected WindowBase? Window { get; private set; }
    
    
    
    /// <summary>
    /// Get the icon for the application window
    /// </summary>
    protected virtual RenderFragment? WindowIcon => null;
    
    
    
    /// <summary>
    /// Initialize the window
    /// </summary>
    protected virtual void InitializeWindow(WindowBase window)
    {
        Window = window;

    }
    
   

    /// <inheritdoc />
    protected override async Task<bool> OnBeforeCloseAsync()
    {
        // Close the window
        if (Window != null)
        {
            await Window.CloseWindow();
            Window = null;

        }
        return await base.OnBeforeCloseAsync();
    }
   
    
   
}
