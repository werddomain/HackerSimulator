using BlazorWindowManager.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorWindowManager.Components;

/// <summary>
/// WindowBase partial class - Public API methods
/// </summary>
public partial class WindowBase
{
    #region Public Methods
    
    /// <summary>
    /// Closes the window, optionally forcing the close without cancellation check
    /// </summary>
    /// <param name="force">Whether to force close without checking for cancellation</param>
    /// <returns>True if the window was closed</returns>
    public async Task<bool> Close(bool force = false)
    {
        if (!force)
        {
            // Call the virtual OnBeforeCloseAsync method
            var canClose = await OnBeforeCloseAsync();
            if (!canClose)
            {
                return false;
            }
            
            var cancelArgs = new WindowCancelEventArgs { Window = this };
            await OnBeforeClose.InvokeAsync(cancelArgs);
            
            if (cancelArgs.Cancel)
            {
                return false;
            }
        }
        
        var success = WindowManager.UnregisterWindow(Id, force);
        if (success)
        {
            await OnAfterClose.InvokeAsync();
            
            // Call the virtual OnAfterCloseAsync method
            await OnAfterCloseAsync();
        }
        
        return success;
    }
    
    /// <summary>
    /// Minimizes the window
    /// </summary>
    public async Task Minimize()
    {
        await SetWindowState(WindowState.Minimized);
    }
    
    /// <summary>
    /// Maximizes the window
    /// </summary>
    public async Task Maximize()
    {
        await SetWindowState(WindowState.Maximized);
    }
    
    /// <summary>
    /// Restores the window to normal state
    /// </summary>
    public async Task Restore()
    {
        await SetWindowState(WindowState.Normal);
    }
    
    /// <summary>
    /// Brings this window to the front and gives it focus
    /// </summary>
    public void BringToFront()
    {
        WindowManager.BringToFront(Id);
    }
    
    /// <summary>
    /// Gets a service from the window's scoped service collection
    /// </summary>
    /// <typeparam name="T">Type of service to retrieve</typeparam>
    /// <returns>The requested service</returns>
    public T GetWindowService<T>() where T : class
    {
        var serviceProvider = WindowContext.BuildServiceProvider();
        return serviceProvider.GetRequiredService<T>();
    }
    
    /// <summary>
    /// Updates the window title
    /// </summary>
    /// <param name="newTitle">New title for the window</param>
    public async Task SetTitle(string newTitle)
    {
        if (Title != newTitle)
        {
            Title = newTitle;
            WindowManager.UpdateWindowTitle(Id, newTitle);
            await OnTitleChanged.InvokeAsync(newTitle);
            StateHasChanged();
        }
    }
    
    #endregion
}
