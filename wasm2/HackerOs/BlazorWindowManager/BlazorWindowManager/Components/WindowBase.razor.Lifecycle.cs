using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorWindowManager.Components;

/// <summary>
/// WindowBase partial class - Component lifecycle methods
/// </summary>
public partial class WindowBase
{
    #region Lifecycle Methods
      protected override async Task OnInitializedAsync()
    {
        // Initialize window bounds
        CurrentBounds.Width = InitialWidth ?? 400;
        CurrentBounds.Height = InitialHeight ?? 300;
        
        // Register with window manager
        _windowInfo = WindowManager.RegisterWindow(this, Id, Title, Name);
        
        // Setup JavaScript interop
        _dotNetRef = DotNetObjectReference.Create(this);
        
        await base.OnInitializedAsync();
    }
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {        if (firstRender)
        {
            // Load JavaScript module for mouse handling
            _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/BlazorWindowManager/js/window-interactions.js");
            
            // Initialize window position and size
            await UpdateWindowDisplay();
            
            // Notify that content is loaded
            await OnContentLoaded.InvokeAsync();
        }
        
        await base.OnAfterRenderAsync(firstRender);
    }
    
    public async ValueTask DisposeAsync()
    {
        // Call the virtual dispose method
        Dispose(true);
        
        // Unregister from window manager
        WindowManager.UnregisterWindow(Id, force: true);
        
        // Dispose JavaScript resources
        _dotNetRef?.Dispose();
        if (_jsModule != null)
        {
            await _jsModule.DisposeAsync();
        }
    }
    
    #endregion
}
