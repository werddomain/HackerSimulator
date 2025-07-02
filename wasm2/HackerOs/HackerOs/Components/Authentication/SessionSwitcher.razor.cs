using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.Security;
using HackerOs.OS.User.Models;

namespace HackerOs.Components.Authentication
{
    /// <summary>
    /// Component for switching between active user sessions
    /// </summary>
    public partial class SessionSwitcher : ComponentBase, IDisposable
    {
        [Inject] private ISessionManager SessionManager { get; set; } = null!;
        [Inject] private IAuthenticationService AuthService { get; set; } = null!;
        [Inject] private ILogger<SessionSwitcher> Logger { get; set; } = null!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] private NavigationManager Navigation { get; set; } = null!;

        [Parameter] public EventCallback<string> OnSessionSwitch { get; set; }
        [Parameter] public EventCallback<string> OnSessionEnd { get; set; }
        [Parameter] public EventCallback OnSessionsUpdated { get; set; }
        [Parameter] public bool IsVisible { get; set; } = false;
        
        // Data properties
        protected List<HackerOs.OS.User.Models.UserSession> ActiveSessions { get; set; } = new List<HackerOs.OS.User.Models.UserSession>();
        protected string ActiveSessionId { get; set; } = string.Empty;
        protected string ErrorMessage { get; set; } = string.Empty;
        
        // New session form
        protected bool IsCreatingNewSession { get; set; } = false;
        protected string NewSessionUsername { get; set; } = string.Empty;
        protected string NewSessionPassword { get; set; } = string.Empty;
        protected bool ShowPassword { get; set; } = false;
        protected bool IsLoading { get; set; } = false;

        // Timer for session refresh
        private System.Timers.Timer? RefreshTimer;
        
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            
            // Set up refresh timer to periodically update sessions
            RefreshTimer = new System.Timers.Timer(10000); // 10 seconds
            RefreshTimer.Elapsed += async (sender, e) => await RefreshSessions();
            RefreshTimer.Start();
            
            // Subscribe to session events
            if (SessionManager != null)
            {
                SessionManager.SessionChanged += OnSessionChangedHandler;
            }
            
            // Initial load
            await RefreshSessions();
        }

        protected override async Task OnParametersSetAsync()
        {
            await base.OnParametersSetAsync();
            
            // Refresh sessions when component becomes visible
            if (IsVisible)
            {
                await RefreshSessions();
            }
        }

        /// <summary>
        /// Refreshes the list of active sessions
        /// </summary>
        protected async Task RefreshSessions()
        {
            try
            {
                // Execute on UI thread
                await InvokeAsync(async () =>
                {
                    // Get active sessions
                    var sessions = await SessionManager.GetAllSessionsAsync();
                    ActiveSessions = sessions.ToList();
                    
                    // Get active session ID
                    var activeSession = await SessionManager.GetActiveSessionAsync();
                    ActiveSessionId = activeSession?.SessionId ?? string.Empty;
                    
                    // Update UI
                    StateHasChanged();
                    
                    // Notify parent
                    await OnSessionsUpdated.InvokeAsync();
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error refreshing sessions");
                ErrorMessage = "Failed to refresh sessions. Please try again.";
            }
        }

        /// <summary>
        /// Handles the session changed event
        /// </summary>
        protected async void OnSessionChangedHandler(object? sender, SessionChangedEventArgs e)
        {
            await RefreshSessions();
        }

        /// <summary>
        /// Switches to another session
        /// </summary>
        protected async Task SwitchToSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId) || sessionId == ActiveSessionId)
                return;
                
            try
            {
                // Switch session
                var result = await SessionManager.SwitchSessionAsync(sessionId);
                
                if (result)
                {
                    // Update active session ID
                    ActiveSessionId = sessionId;
                    
                    // Clear error message
                    ErrorMessage = string.Empty;
                    
                    // Notify parent
                    await OnSessionSwitch.InvokeAsync(sessionId);
                    
                    // Navigate to desktop
                    Navigation.NavigateTo("/desktop");
                    
                    // Hide session switcher
                    Close();
                }
                else
                {
                    ErrorMessage = "Failed to switch session. The session may be invalid or expired.";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error switching to session {SessionId}", sessionId);
                ErrorMessage = "An error occurred while switching sessions. Please try again.";
            }
        }

        /// <summary>
        /// Ends a session
        /// </summary>
        protected async Task EndSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return;
                
            try
            {
                // If ending the active session, handle differently
                if (sessionId == ActiveSessionId)
                {
                    // Log out instead of just ending the session
                    await LogoutActiveSession();
                    return;
                }
                
                // End session
                await SessionManager.EndSessionAsync(sessionId);
                
                // Remove from local list
                ActiveSessions.RemoveAll(s => s.SessionId == sessionId);
                
                // Clear error message
                ErrorMessage = string.Empty;
                
                // Notify parent
                await OnSessionEnd.InvokeAsync(sessionId);
                
                // Refresh sessions
                await RefreshSessions();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error ending session {SessionId}", sessionId);
                ErrorMessage = "An error occurred while ending the session. Please try again.";
            }
        }

        /// <summary>
        /// Locks the active session
        /// </summary>
        protected async Task LockSession()
        {
            try
            {
                await SessionManager.LockSessionAsync();
                
                // Hide session switcher
                Close();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error locking active session");
                ErrorMessage = "An error occurred while locking the session. Please try again.";
            }
        }

        /// <summary>
        /// Logs out the active session
        /// </summary>
        protected async Task LogoutActiveSession()
        {
            try
            {
                await AuthService.LogoutAsync();
                
                // Navigate to login
                Navigation.NavigateTo("/login");
                
                // Hide session switcher
                Close();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error logging out active session");
                ErrorMessage = "An error occurred while logging out. Please try again.";
            }
        }

        /// <summary>
        /// Logs out all sessions
        /// </summary>
        protected async Task LogoutAllSessions()
        {
            try
            {
                // Confirm with user
                bool confirmed = await JSRuntime.InvokeAsync<bool>("confirm", "Are you sure you want to end all sessions? This will log you out immediately.");
                
                if (!confirmed)
                    return;
                
                // End all non-active sessions first
                foreach (var session in ActiveSessions.Where(s => s.SessionId != ActiveSessionId))
                {
                    await SessionManager.EndSessionAsync(session.SessionId);
                }
                
                // Finally logout the active session
                await LogoutActiveSession();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error logging out all sessions");
                ErrorMessage = "An error occurred while logging out all sessions. Please try again.";
            }
        }

        /// <summary>
        /// Opens the new session dialog
        /// </summary>
        protected void CreateNewSession()
        {
            // Reset form
            NewSessionUsername = string.Empty;
            NewSessionPassword = string.Empty;
            ShowPassword = false;
            IsLoading = false;
            
            // Show dialog
            IsCreatingNewSession = true;
        }

        /// <summary>
        /// Handles the creation of a new session
        /// </summary>
        protected async Task HandleCreateNewSession()
        {
            if (IsLoading)
                return;
                
            // Validate input
            if (string.IsNullOrWhiteSpace(NewSessionUsername) || string.IsNullOrWhiteSpace(NewSessionPassword))
            {
                ErrorMessage = "Please enter both username and password.";
                return;
            }
            
            IsLoading = true;
            ErrorMessage = string.Empty;
            StateHasChanged();
            
            try
            {
                // Login with new credentials
                var result = await AuthService.LoginAsync(NewSessionUsername, NewSessionPassword, true);
                
                if (result.IsAuthenticated)
                {
                    // Clear form
                    NewSessionUsername = string.Empty;
                    NewSessionPassword = string.Empty;
                    IsCreatingNewSession = false;
                    
                    // Refresh sessions
                    await RefreshSessions();
                    
                    // Navigate to desktop
                    Navigation.NavigateTo("/desktop");
                    
                    // Hide session switcher
                    Close();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage ?? "Invalid username or password.";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error creating new session");
                ErrorMessage = "An error occurred while creating a new session. Please try again.";
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Toggles password visibility
        /// </summary>
        protected void TogglePasswordVisibility()
        {
            ShowPassword = !ShowPassword;
        }

        /// <summary>
        /// Closes the session switcher
        /// </summary>
        protected void Close()
        {
            IsVisible = false;
            StateHasChanged();
        }

        /// <summary>
        /// Gets user initials for avatar
        /// </summary>
        protected string GetUserInitials(IUserSession session)
        {
            if (session?.User == null || string.IsNullOrEmpty(session.User.Username))
                return "?";
                
            return session.User.Username.Substring(0, 1).ToUpper();
        }

        /// <summary>
        /// Formats time for display
        /// </summary>
        protected string FormatTime(DateTime time)
        {
            // If today, show only time
            if (time.Date == DateTime.Today)
            {
                return time.ToString("HH:mm:ss");
            }
            
            // Otherwise show date and time
            return time.ToString("yyyy-MM-dd HH:mm");
        }

        public void Dispose()
        {
            // Unsubscribe from events
            if (SessionManager != null)
            {
                SessionManager.SessionChanged -= OnSessionChangedHandler;
            }
            
            // Dispose timer
            RefreshTimer?.Dispose();
        }
    }
}
