using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorWindowManager.Components;

/// <summary>
/// WindowBase partial class - Component lifecycle methods
/// </summary>
public partial class WindowBase
{
    #region Lifecycle Methods
    
    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        // Initialize window bounds
        CurrentBounds.Width = InitialWidth ?? 400;
        CurrentBounds.Height = InitialHeight ?? 300;
        Context.Window = this;
        // Try to register with existing window info first (for dynamically created windows)
        _windowInfo = WindowManager.RegisterWindowComponent(this, Id);
        
        // If not found, register as a new window (for manually created windows)
        if (_windowInfo == null)
        {
            _windowInfo = WindowManager.RegisterWindow(this, Id, Title, Name);
        }
        else
        {
            // Sync title from WindowInfo (which was set from parameters during CreateWindow)
            if (!string.IsNullOrEmpty(_windowInfo.Title) && _windowInfo.Title != "Window")
            {
                title = _windowInfo.Title;
            }
        }
        
        // Setup JavaScript interop
        _dotNetRef = DotNetObjectReference.Create(this);
        
        await base.OnInitializedAsync();
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {        if (firstRender)
        {
            // Load JavaScript module for mouse handling
            _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/BlazorWindowManager/js/window-interactions.js");
              // Initialize window position and size
            await UpdateWindowDisplay();
            
            // Register with keyboard navigation service
            await KeyboardNavigation.RegisterWindowAsync(Id);
            
            // Notify that content is loaded
            await OnContentLoaded.InvokeAsync();
        }
        
        await base.OnAfterRenderAsync(firstRender);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        // Call the virtual dispose method
        Dispose(true);
          // Unregister from window manager
        WindowManager.UnregisterWindow(Id, force: true);
        
        // Unregister from keyboard navigation service
        await KeyboardNavigation.UnregisterWindowAsync(Id);
        Context.serviceScope.Dispose();
        Context.Disposed = true;
        // Dispose JavaScript resources
        _dotNetRef?.Dispose();
        if (_jsModule != null)
        {
            await _jsModule.DisposeAsync();
        }
    }
    
    #endregion
}
