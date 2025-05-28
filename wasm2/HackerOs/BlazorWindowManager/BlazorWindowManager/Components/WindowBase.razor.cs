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
    #region Parameters
    
    /// <summary>
    /// Unique identifier for this window instance
    /// </summary>
    [Parameter] public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Optional user-defined name for the window
    /// </summary>
    [Parameter] public string? Name { get; set; }
    
    /// <summary>
    /// Title displayed in the window's title bar
    /// </summary>
    [Parameter] public string Title { get; set; } = "Window";
    
    /// <summary>
    /// Optional icon content for the title bar
    /// </summary>
    [Parameter] public RenderFragment? Icon { get; set; }
    
    /// <summary>
    /// Content to be displayed in the window
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
    
    /// <summary>
    /// Whether the window can be resized
    /// </summary>
    [Parameter] public bool Resizable { get; set; } = true;
    
    /// <summary>
    /// Minimum width constraint in pixels
    /// </summary>
    [Parameter] public double? MinWidth { get; set; } = 200;
    
    /// <summary>
    /// Minimum height constraint in pixels
    /// </summary>
    [Parameter] public double? MinHeight { get; set; } = 150;
    
    /// <summary>
    /// Maximum width constraint in pixels
    /// </summary>
    [Parameter] public double? MaxWidth { get; set; }
    
    /// <summary>
    /// Maximum height constraint in pixels
    /// </summary>
    [Parameter] public double? MaxHeight { get; set; }
    
    /// <summary>
    /// Initial width of the window in pixels
    /// </summary>
    [Parameter] public double InitialWidth { get; set; } = 600;
    
    /// <summary>
    /// Initial height of the window in pixels
    /// </summary>
    [Parameter] public double InitialHeight { get; set; } = 400;
    
    /// <summary>
    /// CSS classes to apply to the window container
    /// </summary>
    [Parameter] public string? CssClass { get; set; }
    
    /// <summary>
    /// Whether to show the close button in the window title bar
    /// </summary>
    [Parameter] public bool ShowCloseButton { get; set; } = true;
    
    #endregion
    
    #region Events
    
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
    
    #region Properties
    
    /// <summary>
    /// Current state of the window
    /// </summary>
    public WindowState CurrentState { get; private set; } = WindowState.Normal;
      /// <summary>
    /// Current bounds of the window
    /// </summary>
    public WindowBounds CurrentBounds { get; private set; } = new();
      /// <summary>
    /// Current width of the window in pixels
    /// </summary>
    public double Width
    {
        get => CurrentBounds.Width;
        set
        {
            CurrentBounds.Width = value;
            InvokeAsync(UpdateWindowDisplay);
        }
    }
    
    /// <summary>
    /// Current height of the window in pixels
    /// </summary>
    public double Height
    {
        get => CurrentBounds.Height;
        set
        {
            CurrentBounds.Height = value;
            InvokeAsync(UpdateWindowDisplay);
        }
    }
    
    /// <summary>
    /// Whether this window is currently active (has focus)
    /// </summary>
    public bool IsActive { get; private set; } = false;
    
    /// <summary>
    /// Window-scoped service collection for dependency injection
    /// </summary>
    protected IServiceCollection WindowContext => _windowContext ??= new ServiceCollection();
    
    #endregion
    
    #region Lifecycle
    
    protected override async Task OnInitializedAsync()
    {
        // Initialize window bounds
        CurrentBounds.Width = InitialWidth;
        CurrentBounds.Height = InitialHeight;
        
        // Register with window manager
        _windowInfo = WindowManager.RegisterWindow(this, Id, Title, Name);
        
        // Setup JavaScript interop
        _dotNetRef = DotNetObjectReference.Create(this);
        
        await base.OnInitializedAsync();
    }
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load JavaScript module for mouse handling
            _jsModule = await JS.InvokeAsync<IJSObjectReference>(
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
    
    #region IWindowMessageReceiver
    
    /// <summary>
    /// Handles messages received from other windows
    /// </summary>
    /// <param name="args">Message event arguments</param>
    public virtual void OnMessageReceived(WindowMessageEventArgs args)
    {
        // Override in derived classes to handle inter-window messages
    }
    
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
        
        // TODO: Add boundary constraints here
        
        CurrentBounds = newBounds;
        WindowManager.UpdateWindowBounds(Id, newBounds);
        
        await OnMoving.InvokeAsync(newBounds);
        await UpdateWindowDisplay();
    }
    
    /// <summary>
    /// Called from JavaScript when dragging ends
    /// </summary>
    [JSInvokable]
    public async Task OnDragEnd()
    {
        if (_isDragging)
        {
            _isDragging = false;
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
    
    #region Private Methods
    
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
