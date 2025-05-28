using BlazorWindowManager.Models;

namespace BlazorWindowManager.Components;

/// <summary>
/// WindowBase partial class - Utility methods and virtual methods
/// </summary>
public partial class WindowBase
{
    #region Private Helper Methods
    
    private async Task SetWindowState(WindowState newState)
    {
        if (CurrentState == newState) return;
        
        var oldState = CurrentState;
        CurrentState = newState;
        
        WindowManager.UpdateWindowState(Id, newState);
        
        await OnStateChanged.InvokeAsync(newState);
        await UpdateWindowDisplay();
        StateHasChanged();
    }
    
    private string GetWindowClasses()
    {
        var classes = new List<string> { "window" };
        
        classes.Add($"window-state-{CurrentState.ToString().ToLower()}");
        
        if (IsActive)
            classes.Add("window-active");
        
        if (!Resizable)
            classes.Add("window-not-resizable");
        
        if (!string.IsNullOrEmpty(CssClass))
            classes.Add(CssClass);
        
        return string.Join(" ", classes);
    }
    
    private string GetWindowStyle()
    {
        var styles = new List<string>();
        
        if (CurrentState != WindowState.Minimized)
        {
            styles.Add($"left: {CurrentBounds.Left}px");
            styles.Add($"top: {CurrentBounds.Top}px");
            styles.Add($"width: {CurrentBounds.Width}px");
            styles.Add($"height: {CurrentBounds.Height}px");
        }
        
        styles.Add($"z-index: {_windowInfo?.ZIndex ?? 1000}");
        
        if (CurrentState == WindowState.Minimized)
        {
            styles.Add("display: none");
        }
        
        return string.Join("; ", styles);
    }
    
    private async Task UpdateWindowDisplay()
    {
        // Force re-render to update styles
        StateHasChanged();
        await Task.Yield();
    }
    
    private WindowBounds CalculateNewBounds(WindowBounds originalBounds, double deltaX, double deltaY, string direction)
    {
        var newBounds = originalBounds.Clone();
        
        switch (direction.ToLower())
        {
            case "n":
                newBounds.Top += deltaY;
                newBounds.Height -= deltaY;
                break;
            case "s":
                newBounds.Height += deltaY;
                break;
            case "e":
                newBounds.Width += deltaX;
                break;
            case "w":
                newBounds.Left += deltaX;
                newBounds.Width -= deltaX;
                break;
            case "ne":
                newBounds.Top += deltaY;
                newBounds.Height -= deltaY;
                newBounds.Width += deltaX;
                break;
            case "nw":
                newBounds.Top += deltaY;
                newBounds.Height -= deltaY;
                newBounds.Left += deltaX;
                newBounds.Width -= deltaX;
                break;
            case "se":
                newBounds.Height += deltaY;
                newBounds.Width += deltaX;
                break;
            case "sw":
                newBounds.Height += deltaY;
                newBounds.Left += deltaX;
                newBounds.Width -= deltaX;
                break;
        }
        
        return newBounds;
    }
    
    private void ApplySizeConstraints(WindowBounds bounds)
    {
        // Apply minimum size constraints
        if (MinWidth.HasValue && bounds.Width < MinWidth.Value)
            bounds.Width = MinWidth.Value;
        
        if (MinHeight.HasValue && bounds.Height < MinHeight.Value)
            bounds.Height = MinHeight.Value;
        
        // Apply maximum size constraints
        if (MaxWidth.HasValue && bounds.Width > MaxWidth.Value)
            bounds.Width = MaxWidth.Value;
        
        if (MaxHeight.HasValue && bounds.Height > MaxHeight.Value)
            bounds.Height = MaxHeight.Value;
    }
    
    #endregion
    
    #region Virtual Methods
    
    /// <summary>
    /// Called before the window is closed. Override to provide custom close validation.
    /// </summary>
    /// <returns>True if the window can be closed, false to cancel the close operation</returns>
    protected virtual async Task<bool> OnBeforeCloseAsync()
    {
        return await Task.FromResult(true);
    }
    
    /// <summary>
    /// Called after the window has been closed. Override to provide custom cleanup logic.
    /// </summary>
    protected virtual async Task OnAfterCloseAsync()
    {
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Override in derived classes to dispose managed resources
        }
    }
    
    #endregion
}
