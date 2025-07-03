using BlazorWindowManager.Components;
using HackerOs.OS.Theme;
using HackerOs.OS.UI.Components;
using HackerOs.OS.User;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace HackerOs.Layout
{
    /// <summary>
    /// Main layout component for the HackerOS application
    /// </summary>
    public partial class MainLayout
    {
        [Inject] protected ILogger<MainLayout> Logger { get; set; } = null!;

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
        /// The current user session
        /// </summary>
        private UserSession? _currentUserSession;

        /// <summary>
        /// Whether the component has been initialized
        /// </summary>
        private bool _initialized = false;

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
        private async Task SetKeyboardFocus()
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
        }
    }
}
