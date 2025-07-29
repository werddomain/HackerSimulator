using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.Security;
using HackerOs.OS.User;
using HackerOs.OS.User.Models;

namespace HackerOs.Components.Authentication
{
    /// <summary>
    /// User profile component for displaying user information and providing session management
    /// </summary>
    public partial class UserProfile : ComponentBase, IDisposable
    {
        [Inject] private ISessionManager SessionManager { get; set; } = null!;
        [Inject] private IAuthenticationService AuthService { get; set; } = null!;
        [Inject] private ILogger<UserProfile> Logger { get; set; } = null!;
        [Inject] private NavigationManager Navigation { get; set; } = null!;

        [Parameter] public EventCallback OnSessionLock { get; set; }
        [Parameter] public EventCallback OnLogout { get; set; }
        [Parameter] public EventCallback<string> OnSessionSwitch { get; set; }
        
        // UI state
        protected bool IsExpanded { get; set; } = false;
        protected bool ShowSessionSwitcher { get; set; } = false;
        
        // User data
        protected HackerOs.OS.User.Models.User? CurrentUser { get; set; }
        protected IUserSession? CurrentSession { get; set; }
        protected int SessionCount { get; set; } = 0;
        
        // Computed properties
        protected string UserInitials => GetUserInitials();
        protected string UserStatusClass => GetUserStatusClass();
        protected string UserStatusText => GetUserStatusText();
        protected string UserType => GetUserType();
        protected string SessionSummary => GetSessionSummary();
        protected string LoginTime => GetLoginTime();
        
        // Timer for session refresh
        private System.Timers.Timer? RefreshTimer;
        
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            
            // Set up refresh timer
            RefreshTimer = new System.Timers.Timer(30000); // 30 seconds
            RefreshTimer.Elapsed += async (sender, e) => await RefreshUserData();
            RefreshTimer.Start();
            
            // Subscribe to session events
            if (SessionManager != null)
            {
                SessionManager.SessionChanged += OnSessionChangedHandler;
            }
            
            // Initial load
            await RefreshUserData();
        }
        
        /// <summary>
        /// Refreshes user data
        /// </summary>
        protected async Task RefreshUserData()
        {
            try
            {
                // Execute on UI thread
                await InvokeAsync(async () =>
                {
                    // Get current session
                    CurrentSession = await SessionManager.GetActiveSessionAsync();
                    
                    // Get user from session
                    if (CurrentSession != null)
                    {
                        // We need to convert the OS.User.User to OS.User.Models.User
                        var user = CurrentSession.User;
                        CurrentUser = new HackerOs.OS.User.Models.User
                        {
                            UserId = user.UserId.ToString(),
                            Username = user.Username,
                            FullName = user.FullName ?? user.Username,
                            HomeDirectory = user.HomeDirectory ?? $"/home/{user.Username}",
                            IsActive = user.IsActive,
                            LastLogin = DateTime.UtcNow, // Default to now if not available
                            Uid = int.TryParse(user.UserId.ToString(), out int uid) ? uid : 1000,
                            Gid = 100, // Default to 'users' group
                            SecondaryGroups = new List<int>() // Initialize secondary groups
                        };
                        
                        // Add some default groups based on username (simplified example)
                        if (user.Username.Equals("root", StringComparison.OrdinalIgnoreCase))
                        {
                            CurrentUser.Uid = 0;
                            CurrentUser.Gid = 0; // root group
                        }
                        else if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
                        {
                            CurrentUser.SecondaryGroups.Add(27); // sudo group
                            CurrentUser.SecondaryGroups.Add(4);  // adm group
                        }
                    }
                    
                    // Get session count
                    var sessions = await SessionManager.GetAllSessionsAsync();
                    SessionCount = sessions.Count();
                    
                    // Update UI
                    StateHasChanged();
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error refreshing user data");
            }
        }
        
        /// <summary>
        /// Handles session changed event
        /// </summary>
        protected async void OnSessionChangedHandler(object? sender, SessionChangedEventArgs e)
        {
            await RefreshUserData();
        }
        
        /// <summary>
        /// Toggles the expanded state
        /// </summary>
        protected void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }
        
        /// <summary>
        /// Opens the session switcher
        /// </summary>
        protected void OpenSessionSwitcher()
        {
            ShowSessionSwitcher = true;
            IsExpanded = false;
        }
        
        /// <summary>
        /// Opens user preferences
        /// </summary>
        protected void OpenUserPreferences()
        {
            // Navigate to preferences page
            Navigation.NavigateTo("/preferences");
            IsExpanded = false;
        }
        
        /// <summary>
        /// Locks the current session
        /// </summary>
        protected async Task LockSession()
        {
            try
            {
                await SessionManager.LockSessionAsync();
                IsExpanded = false;
                await OnSessionLock.InvokeAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error locking session");
            }
        }
        
        /// <summary>
        /// Logs out the current user
        /// </summary>
        protected async Task Logout()
        {
            try
            {
                await AuthService.LogoutAsync();
                IsExpanded = false;
                await OnLogout.InvokeAsync();
                Navigation.NavigateTo("/login");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error logging out");
            }
        }
        
        /// <summary>
        /// Handles session switch event from session switcher
        /// </summary>
        protected async Task HandleSessionSwitch(string sessionId)
        {
            ShowSessionSwitcher = false;
            await RefreshUserData();
            await OnSessionSwitch.InvokeAsync(sessionId);
        }
        
        /// <summary>
        /// Handles session end event from session switcher
        /// </summary>
        protected async Task HandleSessionEnd(string sessionId)
        {
            await RefreshUserData();
        }
        
        /// <summary>
        /// Handles sessions updated event from session switcher
        /// </summary>
        protected async Task HandleSessionsUpdated()
        {
            await RefreshUserData();
        }
        
        /// <summary>
        /// Gets user initials for avatar
        /// </summary>
        protected string GetUserInitials()
        {
            if (CurrentUser == null || string.IsNullOrEmpty(CurrentUser.Username))
                return "?";
                
            return CurrentUser.Username.Substring(0, 1).ToUpper();
        }
        
        /// <summary>
        /// Gets user status CSS class
        /// </summary>
        protected string GetUserStatusClass()
        {
            if (CurrentSession == null)
                return "offline";
                
            return CurrentSession.State switch
            {
                SessionState.Active => "active",
                SessionState.Locked => "locked",
                SessionState.Expired => "expired",
                SessionState.Terminated => "terminated",
                _ => "unknown"
            };
        }
        
        /// <summary>
        /// Gets user status text
        /// </summary>
        protected string GetUserStatusText()
        {
            if (CurrentSession == null)
                return "Offline";
                
            return CurrentSession.State switch
            {
                SessionState.Active => "Active",
                SessionState.Locked => "Locked",
                SessionState.Expired => "Expired",
                SessionState.Terminated => "Terminated",
                _ => "Unknown"
            };
        }
        
    /// <summary>
    /// Gets user type (admin, regular, etc.)
    /// </summary>
    protected string GetUserType()
    {
        if (CurrentUser == null)
            return "Unknown";
        
        // Use the IsAdmin property directly
        if (CurrentUser.IsAdmin)
        {
            return "Administrator";
        }
        
        // Get all group names the user belongs to
        var groupNames = UserModelExtensions.GetGroupNames(CurrentUser);
        
        // Check if user is in wheel or sudo groups (power user)
        if (groupNames.Any(g => g.Equals("wheel", StringComparison.OrdinalIgnoreCase) || 
                               g.Equals("sudo", StringComparison.OrdinalIgnoreCase)))
        {
            return "Power User";
        }
        
        return "Regular User";
    }
        
        /// <summary>
        /// Gets session summary
        /// </summary>
        protected string GetSessionSummary()
        {
            if (CurrentSession == null)
                return "No active session";
                
            // Truncate session ID for display
            var sessionId = CurrentSession.SessionId;
            if (sessionId.Length > 8)
            {
                sessionId = sessionId.Substring(0, 8) + "...";
            }
            
            return sessionId;
        }
        
        /// <summary>
        /// Gets formatted login time
        /// </summary>
        protected string GetLoginTime()
        {
            if (CurrentSession == null)
                return "N/A";
                
            // Format start time
            if (CurrentSession.StartTime.Date == DateTime.Today)
            {
                return "Today " + CurrentSession.StartTime.ToString("HH:mm:ss");
            }
            
            return CurrentSession.StartTime.ToString("yyyy-MM-dd HH:mm");
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
