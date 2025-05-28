using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorWindowManager.Components;

/// <summary>
/// WindowBase partial class - User interactions and JavaScript interop
/// </summary>
public partial class WindowBase
{
    #region Private Interaction Fields
    
    // Dragging state
    private bool _isDragging = false;
    private double _dragStartX;
    private double _dragStartY;
    private WindowBounds _dragStartBounds = new();
    
    // Resizing state
    private bool _isResizing = false;
    private string _resizeDirection = "";
    
    #endregion
      #region IWindowMessageReceiver
    
    // OnMessageReceived is implemented in WindowBase.razor.Core.cs
    
    #endregion

    #region Event Handlers
    
    private async Task OnWindowMouseDown(Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
    {
        // Bring window to front when clicked
        BringToFront();
        
        if (!IsActive)
        {
            IsActive = true;
            await OnFocus.InvokeAsync();
        }
    }
    
    private async Task OnTitleBarMouseDown(Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
    {
        if (CurrentState == WindowState.Maximized) return;
        
        _isDragging = true;
        _dragStartX = e.ClientX;
        _dragStartY = e.ClientY;
        _dragStartBounds = CurrentBounds.Clone();
        
        // Setup global mouse events through JavaScript
        if (_jsModule != null && _dotNetRef != null)
        {
            await _jsModule.InvokeVoidAsync("startDragging", _dotNetRef);
        }
    }
    
    private async Task CloseWindow()
    {
        await Close();
    }
    
    private async Task MinimizeWindow()
    {
        await Minimize();
    }
    
    private async Task ToggleMaximize()
    {
        if (CurrentState == WindowState.Maximized)
        {
            await Restore();
        }
        else
        {
            await Maximize();
        }
    }
    
    private async Task StartResize(Microsoft.AspNetCore.Components.Web.MouseEventArgs e, string direction)
    {
        if (!Resizable || CurrentState == WindowState.Maximized) return;
        
        _isResizing = true;
        _resizeDirection = direction;
        _dragStartX = e.ClientX;
        _dragStartY = e.ClientY;
        _dragStartBounds = CurrentBounds.Clone();
        
        // Setup global mouse events through JavaScript
        if (_jsModule != null && _dotNetRef != null)
        {
            await _jsModule.InvokeVoidAsync("startResizing", _dotNetRef, direction);
        }
    }
    
    #endregion
    
    #region JavaScript Callbacks
      /// <summary>
    /// Called from JavaScript during window dragging
    /// </summary>
    [JSInvokable]
    public async Task OnDragMove(double clientX, double clientY)
    {
        if (!_isDragging) return;
          var deltaX = clientX - _dragStartX;
        var deltaY = clientY - _dragStartY;
        
        var newBounds = _dragStartBounds.Clone();
        newBounds.Left += deltaX;
        newBounds.Top += deltaY;
        
        // Get container bounds for snapping calculations
        var containerBounds = await GetContainerBounds();
        
        // Calculate snap targets
        var snapTarget = SnappingService.CalculateSnapTarget(Id, newBounds, containerBounds);
        
        // Show snap preview if a target is found
        SnappingService.ShowSnapPreview(snapTarget);
        
        // Update window position (snap will be applied on drag end)
        CurrentBounds = newBounds;
        WindowManager.UpdateWindowBounds(Id, newBounds);
        
        await OnMoving.InvokeAsync(newBounds);
    await UpdateWindowDisplay();
    }
    
    /// <summary>
    /// Called from JavaScript when window dragging ends
    /// </summary>
    [JSInvokable]
    public async Task OnDragEnd()
    {
        if (_isDragging)
        {
            _isDragging = false;
            
            // Apply snapping if enabled
            var containerBounds = await GetContainerBounds();
            var snappedBounds = SnappingService.ApplySnapping(Id, CurrentBounds, containerBounds);
            
            // If snapping occurred, update the window position
            if (snappedBounds.Left != CurrentBounds.Left || snappedBounds.Top != CurrentBounds.Top ||
                snappedBounds.Width != CurrentBounds.Width || snappedBounds.Height != CurrentBounds.Height)
            {
                CurrentBounds = snappedBounds;
                WindowManager.UpdateWindowBounds(Id, snappedBounds);
                StateHasChanged(); // Trigger UI update to reflect snapped position
            }
            
            // Hide snap preview
            SnappingService.HideSnapPreview();
            
            await OnMoved.InvokeAsync(CurrentBounds);
        }
    }
    
    /// <summary>
    /// Called from JavaScript during window resizing
    /// </summary>
    [JSInvokable]
    public async Task OnResizeMove(double clientX, double clientY)
    {
        if (!_isResizing) return;
        
        var deltaX = clientX - _dragStartX;
        var deltaY = clientY - _dragStartY;
        
        var newBounds = CalculateNewBounds(_dragStartBounds, deltaX, deltaY, _resizeDirection);
        
        // Apply size constraints
        ApplySizeConstraints(newBounds);
        
        CurrentBounds = newBounds;
        WindowManager.UpdateWindowBounds(Id, newBounds);
        
        await OnResizing.InvokeAsync(newBounds);
        await UpdateWindowDisplay();
    }
    
    /// <summary>
    /// Called from JavaScript when resizing ends
    /// </summary>
    [JSInvokable]
    public async Task OnResizeEnd()
    {
        if (_isResizing)
        {
            _isResizing = false;
            await OnResized.InvokeAsync(CurrentBounds);
        }
    }
    
    #endregion
}
