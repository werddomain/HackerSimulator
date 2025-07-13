using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using BlazorWindowManager.Models;
using BlazorWindowManager.Services;
using HackerOs.OS.Applications.UI.Windows;
using HackerOs.OS.Kernel.Process;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.UI
{
    /// <summary>
    /// Integration service that coordinates between ApplicationManager and WindowManagerService
    /// to ensure proper window application lifecycle management.
    /// </summary>
    public class WindowApplicationManagerIntegration
    {
        private readonly IApplicationManager _applicationManager;
        private readonly WindowManagerService _windowManager;
        private readonly ILogger<WindowApplicationManagerIntegration> _logger;
        
        // Track window applications by process ID and window ID
        private readonly ConcurrentDictionary<int, Guid> _processToWindowMap = new();
        private readonly ConcurrentDictionary<Guid, int> _windowToProcessMap = new();
        
        public WindowApplicationManagerIntegration(
            IApplicationManager applicationManager,
            WindowManagerService windowManager,
            ILogger<WindowApplicationManagerIntegration> logger)
        {
            _applicationManager = applicationManager ?? throw new ArgumentNullException(nameof(applicationManager));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Subscribe to application events
            _applicationManager.ApplicationLaunched += OnApplicationLaunched;
            _applicationManager.ApplicationTerminated += OnApplicationTerminated;
            _applicationManager.ApplicationStateChanged += OnApplicationStateChanged;
            
            // Subscribe to window events
            _windowManager.WindowBeforeClose += OnWindowBeforeClose;
            _windowManager.WindowAfterClose += OnWindowAfterClose;
            _windowManager.WindowStateChanged += OnWindowStateChanged;
            _windowManager.WindowRegistered += OnWindowRegistered;
            _windowManager.WindowUnregistered += OnWindowUnregistered;
            
            _logger.LogInformation("Window application manager integration initialized");
        }
        
        #region Application Event Handlers
        
        /// <summary>
        /// Handles application launched events, tracks window applications
        /// </summary>
        private void OnApplicationLaunched(object? sender, ApplicationLaunchedEventArgs e)
        {
            if (e.Application is WindowBase windowApp)
            {
                _logger.LogDebug("Window application launched: {ApplicationId} (PID: {ProcessId})", 
                    windowApp.Id, windowApp.ProcessId);
                
                // Track the window by process ID (window ID will be mapped when window registers)
                if (windowApp.WindowInfo != null)
                {
                    _processToWindowMap[windowApp.ProcessId] = windowApp.WindowInfo.Id;
                    _windowToProcessMap[windowApp.WindowInfo.Id] = windowApp.ProcessId;
                    
                    _logger.LogDebug("Mapped window {WindowId} to process {ProcessId}", 
                        windowApp.WindowInfo.Id, windowApp.ProcessId);
                }
            }
        }
        
        /// <summary>
        /// Handles application terminated events, closes associated windows
        /// </summary>
        private async void OnApplicationTerminated(object? sender, ApplicationTerminatedEventArgs e)
        {
            if (e.Application is WindowBase)
            {
                int processId = e.Application.ProcessId;
                _logger.LogDebug("Window application terminated: {ApplicationId} (PID: {ProcessId})", 
                    e.Application.Id, processId);
                
                // Find and close the associated window
                if (_processToWindowMap.TryRemove(processId, out var windowId))
                {
                    _windowToProcessMap.TryRemove(windowId, out _);
                    
                    var windowInfo = _windowManager.GetWindow(windowId);
                    if (windowInfo != null)
                    {
                        _logger.LogDebug("Closing window {WindowId} for terminated application {ApplicationId}", 
                            windowId, e.Application.Id);
                        
                        // Close the window without triggering the normal close events
                        // since the application is already terminated
                        await _windowManager.CloseWindowAsync(windowInfo, true);
                    }
                }
            }
        }
        
        /// <summary>
        /// Handles application state changes, updates window state accordingly
        /// </summary>
        private async void OnApplicationStateChanged(object? sender, ApplicationStateChangedEventArgs e)
        {
            if (e.Application is WindowBase)
            {
                _logger.LogDebug("Window application state changed: {ApplicationId} state: {NewState}", 
                    e.Application.Id, e.NewState);
                
                // Find the associated window
                if (_processToWindowMap.TryGetValue(e.Application.ProcessId, out var windowId))
                {
                    var windowInfo = _windowManager.GetWindow(windowId);
                    if (windowInfo != null)
                    {
                        // Update window state based on application state
                        switch (e.NewState)
                        {
                            case ApplicationState.Minimized:
                                if (windowInfo.State != WindowState.Minimized)
                                {
                                    _logger.LogDebug("Minimizing window {WindowId} for application {ApplicationId}",
                                        windowId, e.Application.Id);
                                    await _windowManager.MinimizeWindowAsync(windowInfo);
                                }
                                break;
                                
                            case ApplicationState.Maximized:
                                if (windowInfo.State != WindowState.Maximized)
                                {
                                    _logger.LogDebug("Maximizing window {WindowId} for application {ApplicationId}",
                                        windowId, e.Application.Id);
                                    await _windowManager.MaximizeWindowAsync(windowInfo);
                                }
                                break;
                                
                            case ApplicationState.Running:
                                if (windowInfo.State == WindowState.Minimized || windowInfo.State == WindowState.Maximized)
                                {
                                    _logger.LogDebug("Restoring window {WindowId} for application {ApplicationId}",
                                        windowId, e.Application.Id);
                                    await _windowManager.RestoreWindowAsync(windowInfo);
                                }
                                
                                // Ensure window is visible
                                if (!windowInfo.IsVisible)
                                {
                                    await _windowManager.ShowWindowAsync(windowInfo);
                                }
                                break;
                                
                            case ApplicationState.Stopping:
                            case ApplicationState.Stopped:
                            case ApplicationState.Terminated:
                                if (windowInfo.IsVisible)
                                {
                                    _logger.LogDebug("Hiding window {WindowId} for application {ApplicationId}",
                                        windowId, e.Application.Id);
                                    await _windowManager.HideWindowAsync(windowInfo);
                                }
                                break;
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region Window Event Handlers
        
        /// <summary>
        /// Handles window before close events, attempts graceful application shutdown
        /// </summary>
        private async void OnWindowBeforeClose(object? sender, WindowCancelEventArgs e)
        {
            if (_windowToProcessMap.TryGetValue(e.Window.Id, out var processId))
            {
                _logger.LogDebug("Window {WindowId} is closing, attempting to stop application with PID {ProcessId}",
                    e.Window.Id, processId);
                
                // Find the application by process ID
                var application = _applicationManager.GetRunningApplicationById(processId);
                if (application != null)
                {
                    // Check if application is already stopping/stopped
                    if (application.State == ApplicationState.Stopping || 
                        application.State == ApplicationState.Stopped ||
                        application.State == ApplicationState.Terminated)
                    {
                        // Application already stopping/stopped, allow window close
                        return;
                    }
                    
                    // Attempt graceful shutdown
                    bool stopResult = await application.StopAsync();
                    if (!stopResult)
                    {
                        // If shutdown failed, cancel window close
                        _logger.LogWarning("Failed to gracefully stop application {ApplicationId}, cancelling window close",
                            application.Id);
                        e.Cancel = true;
                    }
                }
            }
        }
        
        /// <summary>
        /// Handles window after close events, cleans up tracking mappings
        /// </summary>
        private void OnWindowAfterClose(object? sender, WindowInfo e)
        {
            if (_windowToProcessMap.TryRemove(e.Id, out var processId))
            {
                _processToWindowMap.TryRemove(processId, out _);
                _logger.LogDebug("Window {WindowId} closed, removed from tracking", e.Id);
            }
        }
        
        /// <summary>
        /// Handles window state changed events, updates application state accordingly
        /// </summary>
        private async void OnWindowStateChanged(object? sender, WindowStateChangedEventArgs e)
        {
            if (_windowToProcessMap.TryGetValue(e.Window.Id, out var processId))
            {
                _logger.LogDebug("Window {WindowId} state changed to {NewState}, updating application with PID {ProcessId}",
                    e.Window.Id, e.NewState, processId);
                
                // Find the application by process ID
                var application = _applicationManager.GetRunningApplicationById(processId);
                if (application != null)
                {
                    // Update application state based on window state
                    switch (e.NewState)
                    {
                        case WindowState.Minimized:
                            if (application.State != ApplicationState.Minimized)
                            {
                                await application.SetStateAsync(ApplicationState.Minimized);
                            }
                            break;
                            
                        case WindowState.Maximized:
                            if (application.State != ApplicationState.Maximized)
                            {
                                await application.SetStateAsync(ApplicationState.Maximized);
                            }
                            break;
                            
                        case WindowState.Normal:
                            if (application.State != ApplicationState.Running)
                            {
                                await application.SetStateAsync(ApplicationState.Running);
                            }
                            break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Handles window registered events, maps window to application process
        /// </summary>
        private void OnWindowRegistered(object? sender, WindowEventArgs e)
        {
            // When a window registers, try to find its related application component
            if (e.Window.ComponentRef is WindowBase windowBase && windowBase.ProcessId > 0)
            {
                _processToWindowMap[windowBase.ProcessId] = e.Window.Id;
                _windowToProcessMap[e.Window.Id] = windowBase.ProcessId;
                
                _logger.LogDebug("Window {WindowId} registered and mapped to process {ProcessId}",
                    e.Window.Id, windowBase.ProcessId);
            }
        }
        
        /// <summary>
        /// Handles window unregistered events, cleans up tracking mappings
        /// </summary>
        private void OnWindowUnregistered(object? sender, WindowEventArgs e)
        {
            if (_windowToProcessMap.TryRemove(e.Window.Id, out var processId))
            {
                _processToWindowMap.TryRemove(processId, out _);
                _logger.LogDebug("Window {WindowId} unregistered, removed from tracking", e.Window.Id);
            }
        }
        
        #endregion
        
        /// <summary>
        /// Disposes the integration service and unsubscribes from events
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe from application events
            _applicationManager.ApplicationLaunched -= OnApplicationLaunched;
            _applicationManager.ApplicationTerminated -= OnApplicationTerminated;
            _applicationManager.ApplicationStateChanged -= OnApplicationStateChanged;
            
            // Unsubscribe from window events
            _windowManager.WindowBeforeClose -= OnWindowBeforeClose;
            _windowManager.WindowAfterClose -= OnWindowAfterClose;
            _windowManager.WindowStateChanged -= OnWindowStateChanged;
            _windowManager.WindowRegistered -= OnWindowRegistered;
            _windowManager.WindowUnregistered -= OnWindowUnregistered;
            
            _logger.LogInformation("Window application manager integration disposed");
        }
    }
}
