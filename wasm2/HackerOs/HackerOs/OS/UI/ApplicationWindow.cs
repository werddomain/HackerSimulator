using BlazorWindowManager.Models;
using BlazorWindowManager.Services;
using HackerOs.OS.Applications;
using HackerOs.OS.Settings;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;
using System;

namespace HackerOs.OS.UI
{
    /// <summary>
    /// Bridge class that connects an IApplication with a WindowInfo from BlazorWindowManager.
    /// Handles synchronization of state between the application and its window.
    /// </summary>
    public class ApplicationWindow : IDisposable
    {
        private readonly IApplication _application;
        private readonly WindowInfo _windowInfo;
        private readonly WindowManagerService _windowManager;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<ApplicationWindow> _logger;
        private bool _isDisposed = false;
        private bool _isSynchronizing = false;

        /// <summary>
        /// The application instance this window is associated with
        /// </summary>
        public IApplication Application => _application;

        /// <summary>
        /// The window information for this application window
        /// </summary>
        public WindowInfo WindowInfo => _windowInfo;

        /// <summary>
        /// Creates a new application window bridge
        /// </summary>
        /// <param name="application">The application instance</param>
        /// <param name="windowInfo">The window information</param>
        /// <param name="windowManager">The window manager service</param>
        /// <param name="settingsService">The settings service</param>
        /// <param name="logger">Logger for this class</param>
        public ApplicationWindow(
            IApplication application,
            WindowInfo windowInfo,
            WindowManagerService windowManager,
            ISettingsService settingsService,
            ILogger<ApplicationWindow> logger)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _windowInfo = windowInfo ?? throw new ArgumentNullException(nameof(windowInfo));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to window events
            _windowManager.WindowBeforeClose += OnWindowBeforeClose;
            _windowManager.WindowAfterClose += OnWindowAfterClose;
            _windowManager.WindowStateChanged += OnWindowStateChanged;
            _windowManager.WindowBoundsChanged += OnWindowBoundsChanged;

            // Subscribe to application events
            _application.StateChanged += OnApplicationStateChanged;

            // Set initial window properties from application
            UpdateWindowFromApplication();
        }

        /// <summary>
        /// Synchronizes the window state to match the application state
        /// </summary>
        public void SyncApplicationStateToWindow()
        {
            if (_isSynchronizing) return;

            try
            {
                _isSynchronizing = true;
                UpdateWindowFromApplication();
            }
            finally
            {
                _isSynchronizing = false;
            }
        }

        /// <summary>
        /// Synchronizes the application state to match the window state
        /// </summary>
        public void SyncWindowStateToApplication()
        {
            if (_isSynchronizing) return;

            try
            {
                _isSynchronizing = true;
                UpdateApplicationFromWindow();
            }
            finally
            {
                _isSynchronizing = false;
            }
        }

        /// <summary>
        /// Saves the current window state to user settings
        /// </summary>
        public void SaveWindowState()
        {
            if (_application.OwnerSession?.User == null)
            {
                _logger.LogWarning("Cannot save window state for application {AppId}: no owner session", _application.Id);
                return;
            }

            try
            {
                var settings = _settingsService.GetUserSettings(_application.OwnerSession.User);
                settings.SetValue($"app.{_application.Id}.window.x", _windowInfo.Bounds.X);
                settings.SetValue($"app.{_application.Id}.window.y", _windowInfo.Bounds.Y);
                settings.SetValue($"app.{_application.Id}.window.width", _windowInfo.Bounds.Width);
                settings.SetValue($"app.{_application.Id}.window.height", _windowInfo.Bounds.Height);
                settings.SetValue($"app.{_application.Id}.window.state", (int)_windowInfo.State);

                _logger.LogDebug("Saved window state for application {AppId}", _application.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving window state for application {AppId}", _application.Id);
            }
        }

        /// <summary>
        /// Restores the window state from user settings
        /// </summary>
        public void RestoreWindowState()
        {
            if (_application.OwnerSession?.User == null)
            {
                _logger.LogWarning("Cannot restore window state for application {AppId}: no owner session", _application.Id);
                return;
            }

            try
            {
                var settings = _settingsService.GetUserSettings(_application.OwnerSession.User);

                // Get default position (centered) if not found in settings
                double defaultX = (1920 - 800) / 2; // Assume 1920 screen width and 800 window width as defaults
                double defaultY = (1080 - 600) / 2; // Assume 1080 screen height and 600 window height as defaults

                var x = settings.GetValue<double>($"app.{_application.Id}.window.x", defaultX);
                var y = settings.GetValue<double>($"app.{_application.Id}.window.y", defaultY);

                var bounds = new WindowBounds(x, y)
                {
                    Width = settings.GetValue<double>($"app.{_application.Id}.window.width", 800),
                    Height = settings.GetValue<double>($"app.{_application.Id}.window.height", 600)
                };

                var savedState = settings.GetValue<int>($"app.{_application.Id}.window.state", 0);
                var state = (WindowState)savedState;

                _windowManager.UpdateWindowBounds(_windowInfo.Id, bounds);
                _windowManager.UpdateWindowState(_windowInfo.Id, state);

                _logger.LogDebug("Restored window state for application {AppId}", _application.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring window state for application {AppId}", _application.Id);
            }
        }

        /// <summary>
        /// Close the window and terminate the application
        /// </summary>
        /// <param name="force">If true, forces closure without confirmation</param>
        public async void Close(bool force = false)
        {
            await _windowManager.CloseWindowAsync(_windowInfo.Id, force);
        }
        /// <summary>
        /// Close the window and terminate the application
        /// </summary>
        /// <param name="force">If true, forces closure without confirmation</param>
        public Task CloseAsync(bool force = false)
            => _windowManager.CloseWindowAsync(_windowInfo.Id, force);


        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            // Unsubscribe from window events
            _windowManager.WindowBeforeClose -= OnWindowBeforeClose;
            _windowManager.WindowAfterClose -= OnWindowAfterClose;
            _windowManager.WindowStateChanged -= OnWindowStateChanged;
            _windowManager.WindowBoundsChanged -= OnWindowBoundsChanged;

            // Unsubscribe from application events
            _application.StateChanged -= OnApplicationStateChanged;

            _isDisposed = true;
        }

        #region Private Methods

        private void UpdateWindowFromApplication()
        {
            // Update window title
            _windowManager.UpdateWindowTitle(_windowInfo.Id, _application.Name);

            // Update window state based on application state
            switch (_application.State)
            {
                case ApplicationState.Running:
                    _windowManager.UpdateWindowState(_windowInfo.Id, WindowState.Normal);
                    break;
                case ApplicationState.Minimized:
                    _windowManager.UpdateWindowState(_windowInfo.Id, WindowState.Minimized);
                    break;
                case ApplicationState.Maximized:
                    _windowManager.UpdateWindowState(_windowInfo.Id, WindowState.Maximized);
                    break;
                case ApplicationState.Suspended:
                    // Maybe add a special visual indicator for suspended apps?
                    break;
                case ApplicationState.Terminated:
                    // Application is terminating, close the window
                    _windowManager.CloseWindowAsync(_windowInfo.Id, true).Wait();
                    break;
            }
        }

        private void UpdateApplicationFromWindow()
        {
            // Update application state based on window state
            // Application interface does not expose direct state manipulation,
            // so window state changes are not propagated back.
        }

        #endregion

        #region Event Handlers

        private void OnWindowBeforeClose(object? sender, WindowCancelEventArgs e)
        {
            if (e.Window != _windowInfo.ComponentRef) return;

            // No additional checks - always allow close
        }

        private void OnWindowAfterClose(object? sender, WindowInfo e)
        {
            if (e.Id != _windowInfo.Id) return;

            // Window was closed, terminate the application
            if (_application.State != ApplicationState.Terminated)
            {
                _application.TerminateAsync().Wait();
                _logger.LogDebug("Window closed, terminating application {AppId}", _application.Id);
            }

            // Clean up resources
            Dispose();
        }

        private void OnWindowStateChanged(object? sender, WindowStateChangedEventArgs e)
        {
            if (e.WindowId != _windowInfo.Id) return;

            // Sync window state to application
            SyncWindowStateToApplication();

            // Save window state
            SaveWindowState();
        }

        private void OnWindowBoundsChanged(object? sender, WindowBoundsChangedEventArgs e)
        {
            if (e.Window != _windowInfo.ComponentRef)
                return;

            // Save window position and size on change
            SaveWindowState();
        }

        private void OnApplicationStateChanged(object? sender, ApplicationStateChangedEventArgs e)
        {
            // Sync application state to window
            SyncApplicationStateToWindow();
        }


        #endregion
    }
}
