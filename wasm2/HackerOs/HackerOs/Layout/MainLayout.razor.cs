using BlazorWindowManager.Components;
using HackerOs.OS.Theme;
using HackerOs.OS.UI.Components;
using HackerOs.OS.UI.Models;
using HackerOs.OS.UI.Services;
using HackerOs.OS.User;
using HackerOs.OS.Applications;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.Layout
{
    /// <summary>
    /// Main layout component for the HackerOS application
    /// </summary>
    public partial class MainLayout
    {
        [Inject] protected ILogger<MainLayout> Logger { get; set; } = null!;
        [Inject] protected NotificationService NotificationService { get; set; } = null!;

        /// <summary>
        /// Reference to the main layout div for keyboard focus
        /// </summary>
        private ElementReference _mainLayoutDiv;

        /// <summary>
        /// Reference to the desktop area component from BlazorWindowManager
        /// </summary>
        private DesktopArea _desktopArea = null!;

        /// <summary>
        /// Reference to the desktop component
        /// </summary>
        private Desktop _desktop = null!;

        /// <summary>
        /// Reference to the taskbar component
        /// </summary>
        private Taskbar _taskbar = null!;

        /// <summary>
        /// Reference to the notification toast component
        /// </summary>
        private NotificationToast _notificationToast = null!;

        /// <summary>
        /// Reference to the notification center component
        /// </summary>
        private NotificationCenter _notificationCenter = null!;

        /// <summary>
        /// Reference to the start menu component
        /// </summary>
        private StartMenu _startMenu = null!;

        /// <summary>
        /// The current user session
        /// </summary>
        private UserSession? _currentUserSession;

        /// <summary>
        /// Whether the component has been initialized
        /// </summary>
        private bool _initialized = false;

        /// <summary>
        /// Whether the notification center is open
        /// </summary>
        private bool _isNotificationCenterOpen = false;

        /// <summary>
        /// Whether the start menu is open
        /// </summary>
        private bool _isStartMenuOpen = false;

        /// <summary>
        /// Initializes the component
        /// </summary>
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            try
            {
                // Subscribe to user manager events
                UserManager.UserLoggedIn += OnUserLoggedIn;
                UserManager.UserLoggedOut += OnUserLoggedOut;

                // Check for existing user session
                var currentUser = await UserManager.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    _currentUserSession = await UserManager.CreateSessionAsync(currentUser);
                    Logger.LogInformation("Created session for user: {Username}", currentUser.Username);
                }
                else
                {
                    Logger.LogInformation("No current user, showing login screen");
                }

                // Subscribe to theme changes
                ThemeManager.ThemeChanged += OnThemeChanged;

                // Apply current theme
                await ThemeManager.ApplyCurrentThemeAsync();

                _initialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing MainLayout");
            }
        }

        /// <summary>
        /// Called when the component is rendered
        /// </summary>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (firstRender)
            {
                // Initialize window manager once the desktop area is rendered
                if (_desktopArea != null)
                {
                    // Any additional desktop area initialization
                    await _desktopArea.InitializeAsync();
                }
                
                // Set keyboard focus to capture global shortcuts
                await SetKeyboardFocus();
            }
        }

        /// <summary>
        /// Called when a user logs in
        /// </summary>
        private async void OnUserLoggedIn(object? sender, UserEventArgs e)
        {
            try
            {
                _currentUserSession = await UserManager.CreateSessionAsync(e.User);
                await InvokeAsync(StateHasChanged);
                Logger.LogInformation("User logged in: {Username}", e.User.Username);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling user login");
            }
        }

        /// <summary>
        /// Called when a user logs out
        /// </summary>
        private async void OnUserLoggedOut(object? sender, UserEventArgs e)
        {
            try
            {
                _currentUserSession = null;
                await InvokeAsync(StateHasChanged);
                Logger.LogInformation("User logged out: {Username}", e.User.Username);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling user logout");
            }
        }

        /// <summary>
        /// Called when the theme changes
        /// </summary>
        private async void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            try
            {
                await InvokeAsync(StateHasChanged);
                Logger.LogInformation("Theme changed to: {ThemeName}", e.Theme.Name);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling theme change");
            }
        }

        /// <summary>
        /// Cleans up resources
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe from events
            UserManager.UserLoggedIn -= OnUserLoggedIn;
            UserManager.UserLoggedOut -= OnUserLoggedOut;
            ThemeManager.ThemeChanged -= OnThemeChanged;
        }

        /// <summary>
        /// Handle keyboard input events for global shortcuts
        /// </summary>
        /// <param name="e">Keyboard event arguments</param>
        /// <returns>Task for async operation</returns>
        private async Task HandleKeyDown(KeyboardEventArgs e)
        {
            try
            {
                Logger.LogDebug("Key pressed: {Key}, Alt: {Alt}, Ctrl: {Ctrl}, Shift: {Shift}", 
                    e.Key, e.AltKey, e.CtrlKey, e.ShiftKey);
                
                // If taskbar is available, try task switching first
                if (_taskbar != null)
                {
                    bool handled = await _taskbar.HandleTaskSwitchingKeys(e);
                    if (handled)
                    {
                        return;
                    }
                }
                
                // Add more global keyboard shortcuts here
                // Examples:
                // - Win+E: Open file explorer
                // - Win+R: Run dialog
                // - Ctrl+Alt+Del: System dialog
                
                // For demonstration purposes, let's add Win+D to show desktop
                if (e.CtrlKey && e.Key.ToLower() == "d")
                {
                    Logger.LogInformation("Show desktop shortcut triggered");
                    // Minimize all windows - this would be implemented in WindowManager
                    // await WindowManager.MinimizeAllWindows();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling keyboard event");
            }
        }
        
        /// <summary>
        /// Set focus to the main layout div to capture keyboard events
        /// </summary>
        private Task SetKeyboardFocus()
        {
            try
            {
                // Use JS interop to set focus to the main layout div
                // In a real implementation, you would use IJSRuntime to call element.focus()
                Logger.LogDebug("Setting keyboard focus to main layout");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error setting keyboard focus");
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle notification click from toast
        /// </summary>
        /// <param name="notification">The clicked notification</param>
        private async Task HandleNotificationClick(NotificationModel notification)
        {
            try
            {
                Logger.LogInformation("Notification clicked: {NotificationTitle}", notification.Title);
                
                // If the notification has a target application, launch it
                if (!string.IsNullOrEmpty(notification.TargetApplicationId))
                {
                    await LaunchApplicationFromNotification(notification);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling notification click");
            }
        }
        
        /// <summary>
        /// Handle notification center open state changed
        /// </summary>
        /// <param name="isOpen">Whether the notification center is open</param>
        private void HandleNotificationCenterOpenChanged(bool isOpen)
        {
            _isNotificationCenterOpen = isOpen;
            StateHasChanged();
        }
        
        /// <summary>
        /// Toggle the notification center
        /// </summary>
        private void ToggleNotificationCenter()
        {
            if (_notificationCenter != null)
            {
                _notificationCenter.ToggleNotificationCenter();
            }
        }
        
        /// <summary>
        /// Launch an application from a notification
        /// </summary>
        /// <param name="notification">The notification with application information</param>
        private async Task LaunchApplicationFromNotification(NotificationModel notification)
        {
            try
            {
                // Get the application
                var app = ApplicationManager.GetApplication(notification.TargetApplicationId!);
                if (app != null)
                {
                    Logger.LogInformation("Launching application from notification: {AppName}", app.Name);
                    
                    // Use simple context for testing
                    var context = SimpleApplicationLaunchContext.CreateSimpleContext();
                    
                    // Launch the application
                    await ApplicationManager.LaunchApplicationAsync(app.Id, context);
                }
                else
                {
                    Logger.LogWarning("Application not found: {AppId}", notification.TargetApplicationId);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error launching application from notification");
            }
        }
        
        /// <summary>
        /// Toggle the start menu
        /// </summary>
        private void ToggleStartMenu()
        {
            _isStartMenuOpen = !_isStartMenuOpen;
            
            // If start menu is opened, close notification center
            if (_isStartMenuOpen && _isNotificationCenterOpen)
            {
                _isNotificationCenterOpen = false;
                _notificationCenter.CloseNotificationCenter();
            }
            
            StateHasChanged();
            Logger.LogDebug("Start menu {State}", _isStartMenuOpen ? "opened" : "closed");
        }
        
        /// <summary>
        /// Handle the start menu open state changed
        /// </summary>
        /// <param name="isOpen">Whether the start menu is open</param>
        private void HandleStartMenuOpenChanged(bool isOpen)
        {
            _isStartMenuOpen = isOpen;
            StateHasChanged();
        }
        
        /// <summary>
        /// Handle an application being launched from the start menu
        /// </summary>
        /// <param name="applicationId">ID of the launched application</param>
        private Task HandleStartMenuAppLaunched(string applicationId)
        {
            Logger.LogInformation("Application launched from start menu: {AppId}", applicationId);
            
            // Close the start menu when an app is launched
            _isStartMenuOpen = false;
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Handle sign out action from start menu
        /// </summary>
        private Task HandleSignOut()
        {
            try
            {
                Logger.LogInformation("User signing out from start menu");
                
                if (_currentUserSession != null)
                {
                    // In a real implementation, this would sign out the user
                    // await UserManager.SignOutAsync(_currentUserSession.User.Username);
                    
                    // For now, just clear the current user session
                    _currentUserSession = null;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling sign out from start menu");
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Handle settings button click from start menu
        /// </summary>
        private async Task HandleSettingsClicked()
        {
            try
            {
                Logger.LogInformation("Opening settings from start menu");
                
                // Use simple context for testing
                var context = SimpleApplicationLaunchContext.CreateSimpleContext();
                
                // Launch the settings application
                await ApplicationManager.LaunchApplicationAsync("settings", context);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error opening settings from start menu");
            }
        }
        
        /// <summary>
        /// Handle shutdown action from start menu
        /// </summary>
        private async Task HandleShutdown()
        {
            try
            {
                Logger.LogInformation("System shutdown requested from start menu");
                
                // Show shutdown confirmation dialog
                // This would normally use a dialog service
                Logger.LogInformation("Showing shutdown confirmation dialog");
                
                // For demonstration, simulate shutdown
                // In a real implementation, this would trigger a proper OS shutdown sequence
                await HandleSignOut();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling shutdown from start menu");
            }
        }
        
        /// <summary>
        /// Handle restart action from start menu
        /// </summary>
        private async Task HandleRestart()
        {
            try
            {
                Logger.LogInformation("System restart requested from start menu");
                
                // Show restart confirmation dialog
                // This would normally use a dialog service
                Logger.LogInformation("Showing restart confirmation dialog");
                
                // For demonstration, simulate restart
                // In a real implementation, this would trigger a proper OS restart sequence
                await HandleSignOut();
                
                // After a short delay, log back in with default user
                // This simulates a restart for demonstration purposes
                await Task.Delay(1000);
                
                // Get default user (this would be implemented in a real user manager)
                // var defaultUser = await UserManager.GetDefaultUserAsync();
                // _currentUserSession = await UserManager.CreateSessionAsync(defaultUser);
                // StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling restart from start menu");
            }
        }
        
        /// <summary>
        /// Handle lock action from start menu
        /// </summary>
        private async Task HandleLock()
        {
            try
            {
                Logger.LogInformation("System lock requested from start menu");
                
                // In a real implementation, this would show a lock screen
                // but keep the user session active
                Logger.LogInformation("Showing lock screen");
                
                // For demonstration, just log out
                await HandleSignOut();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling lock from start menu");
            }
        }
    }
}
