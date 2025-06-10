using BlazorWindowManager.Models;
using BlazorWindowManager.Services;
using HackerOs.OS.Applications;
using HackerOs.OS.Settings;
using HackerOs.OS.User;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace HackerOs.OS.UI
{
    /// <summary>
    /// Service that manages application windows, handling the integration between
    /// the HackerOS application framework and the BlazorWindowManager system.
    /// </summary>
    public class ApplicationWindowManager
    {
        private readonly WindowManagerService _windowManager;
        private readonly IApplicationManager _applicationManager;
        private readonly ISettingsService _settingsService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ApplicationWindowManager> _logger;
        
        // Mapping of application ID to application window
        private readonly ConcurrentDictionary<string, ApplicationWindow> _applicationWindows = new();
        
        // Mapping of window ID to application window
        private readonly ConcurrentDictionary<Guid, ApplicationWindow> _windowApplications = new();

        /// <summary>
        /// Event raised when an application window is created
        /// </summary>
        public event EventHandler<ApplicationWindow>? ApplicationWindowCreated;
        
        /// <summary>
        /// Event raised when an application window is closed
        /// </summary>
        public event EventHandler<ApplicationWindow>? ApplicationWindowClosed;

        /// <summary>
        /// Creates a new instance of the ApplicationWindowManager
        /// </summary>
        public ApplicationWindowManager(
            WindowManagerService windowManager,
            IApplicationManager applicationManager,
            ISettingsService settingsService,
            ILoggerFactory loggerFactory)
        {
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _applicationManager = applicationManager ?? throw new ArgumentNullException(nameof(applicationManager));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<ApplicationWindowManager>();
            
            // Subscribe to application manager events
            _applicationManager.ApplicationLaunched += OnApplicationLaunched;
            _applicationManager.ApplicationTerminated += OnApplicationTerminated;
            
            // Subscribe to window manager events
            _windowManager.WindowAfterClose += OnWindowAfterClose;
        }

        /// <summary>
        /// Creates a window for the specified application
        /// </summary>
        /// <param name="application">The application to create a window for</param>
        /// <returns>The created application window</returns>
        public ApplicationWindow CreateWindowForApplication(IApplication application)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            
            if (_applicationWindows.TryGetValue(application.Id, out var existingWindow))
            {
                _logger.LogWarning("Window already exists for application {AppId}", application.Id);
                return existingWindow;
            }
            
            // Create window info
            var windowInfo = new WindowInfo
            {
                Id = Guid.NewGuid(),
                Title = application.Name,
                Name = application.Id,
                Icon = application.Manifest.Icon,
                ComponentType = application.GetType().Name,
                Bounds = new WindowBounds(100, 100, 800, 600), // Default bounds
            };
            
            // Add parameters for window content
            windowInfo.Parameters["ApplicationId"] = application.Id;
            
            // Register window with window manager
            _windowManager.RegisterWindow(windowInfo);
            
            // Create application window bridge
            var logger = _loggerFactory.CreateLogger<ApplicationWindow>();
            var appWindow = new ApplicationWindow(application, windowInfo, _windowManager, _settingsService, logger);
            
            // Track window in dictionaries
            _applicationWindows[application.Id] = appWindow;
            _windowApplications[windowInfo.Id] = appWindow;
            
            // Restore saved window state
            appWindow.RestoreWindowState();
            
            // Raise event
            ApplicationWindowCreated?.Invoke(this, appWindow);
            
            _logger.LogInformation("Created window for application {AppId}", application.Id);
            
            return appWindow;
        }

        /// <summary>
        /// Closes the window for the specified application
        /// </summary>
        /// <param name="applicationId">The ID of the application</param>
        /// <param name="force">If true, forces the window to close without confirmation</param>
        /// <returns>True if the window was found and close was initiated</returns>
        public bool CloseApplicationWindow(string applicationId, bool force = false)
        {
            if (string.IsNullOrEmpty(applicationId)) throw new ArgumentNullException(nameof(applicationId));
            
            if (_applicationWindows.TryGetValue(applicationId, out var appWindow))
            {
                appWindow.Close(force);
                return true;
            }
            
            _logger.LogWarning("No window found for application {AppId}", applicationId);
            return false;
        }

        /// <summary>
        /// Gets the application window for the specified application
        /// </summary>
        /// <param name="applicationId">The ID of the application</param>
        /// <returns>The application window, or null if not found</returns>
        public ApplicationWindow? GetApplicationWindow(string applicationId)
        {
            if (string.IsNullOrEmpty(applicationId)) throw new ArgumentNullException(nameof(applicationId));
            
            if (_applicationWindows.TryGetValue(applicationId, out var appWindow))
            {
                return appWindow;
            }
            
            return null;
        }

        /// <summary>
        /// Gets the application window for the specified window ID
        /// </summary>
        /// <param name="windowId">The ID of the window</param>
        /// <returns>The application window, or null if not found</returns>
        public ApplicationWindow? GetApplicationWindowByWindowId(Guid windowId)
        {
            if (_windowApplications.TryGetValue(windowId, out var appWindow))
            {
                return appWindow;
            }
            
            return null;
        }

        /// <summary>
        /// Gets all application windows
        /// </summary>
        /// <returns>Collection of all application windows</returns>
        public IReadOnlyCollection<ApplicationWindow> GetAllApplicationWindows()
        {
            return _applicationWindows.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets all windows associated with a specific application.
        /// </summary>
        public IReadOnlyCollection<ApplicationWindow> GetWindowsForApplication(string applicationId)
        {
            return _applicationWindows.Values
                .Where(w => w.Application.Id == applicationId)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Brings the application's window to the front.
        /// </summary>
        public void BringToFront(string applicationId)
        {
            var window = GetApplicationWindow(applicationId);
            if (window != null)
            {
                _windowManager.BringToFront(window.WindowInfo.Id);
            }
        }

        /// <summary>
        /// Minimizes the application's window.
        /// </summary>
        public async Task MinimizeApplication(string applicationId)
        {
            var window = GetApplicationWindow(applicationId);
            if (window != null)
            {
                await _windowManager.MinimizeWindowAsync(window.WindowInfo.Id);
            }
        }

        /// <summary>
        /// Restores a minimized application window.
        /// </summary>
        public async Task RestoreApplication(string applicationId)
        {
            var window = GetApplicationWindow(applicationId);
            if (window != null)
            {
                await _windowManager.RestoreWindowAsync(window.WindowInfo.Id);
                BringToFront(applicationId);
            }
        }

        /// <summary>
        /// Gets all application windows for the specified user session
        /// </summary>
        /// <param name="userSession">The user session</param>
        /// <returns>Collection of application windows for the user</returns>
        public IReadOnlyCollection<ApplicationWindow> GetUserApplicationWindows(UserSession userSession)
        {
            if (userSession == null) throw new ArgumentNullException(nameof(userSession));
            
            return _applicationWindows.Values
                .Where(w => w.Application.OwnerSession?.SessionId == userSession.SessionId)
                .ToList()
                .AsReadOnly();
        }

        #region Event Handlers

        private void OnApplicationLaunched(object? sender, ApplicationLaunchedEventArgs e)
        {
            // If the application is windowed, create a window for it
            if (e.Application.Type == ApplicationType.WindowedApplication && !_applicationWindows.ContainsKey(e.Application.Id))
            {
                CreateWindowForApplication(e.Application);
            }
        }

        private void OnApplicationTerminated(object? sender, ApplicationTerminatedEventArgs e)
        {
            // If the application has a window, close it
            if (_applicationWindows.TryGetValue(e.Application.Id, out var appWindow))
            {
                // Remove from dictionaries
                _applicationWindows.TryRemove(e.Application.Id, out _);
                _windowApplications.TryRemove(appWindow.WindowInfo.Id, out _);
                
                // Close window if not already closed
                appWindow.Close(true);
                
                // Raise event
                ApplicationWindowClosed?.Invoke(this, appWindow);
                
                // Dispose window
                appWindow.Dispose();
                
                _logger.LogInformation("Closed window for terminated application {AppId}", e.Application.Id);
            }
        }

        private void OnWindowAfterClose(object? sender, WindowInfo e)
        {
            // If this window belongs to an application, clean up
            if (_windowApplications.TryRemove(e.Id, out var appWindow))
            {
                // Remove from application dictionary
                _applicationWindows.TryRemove(appWindow.Application.Id, out _);
                
                // Raise event
                ApplicationWindowClosed?.Invoke(this, appWindow);
                
                _logger.LogInformation("Window closed for application {AppId}", appWindow.Application.Id);
            }
        }

        #endregion
    }
}
