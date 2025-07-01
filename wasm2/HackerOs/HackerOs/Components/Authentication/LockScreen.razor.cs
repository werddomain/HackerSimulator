using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;
using System.Timers;
using HackerOs.OS.Security;
using HackerOs.OS.User.Models;

namespace HackerOs.Components.Authentication
{
    /// <summary>
    /// Lock screen component for locked user sessions
    /// </summary>
    public partial class LockScreen : ComponentBase, IDisposable
    {
        [Inject] private ISessionManager SessionManager { get; set; } = null!;
        [Inject] private IAuthenticationService AuthenticationService { get; set; } = null!;
        [Inject] private ILogger<LockScreen> Logger { get; set; } = null!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] private NavigationManager Navigation { get; set; } = null!;

        [Parameter] public EventCallback<bool> OnUnlockAttempt { get; set; }
        [Parameter] public string RedirectUrl { get; set; } = "/desktop";
        [Parameter] public string LogoutUrl { get; set; } = "/login";

        private ElementReference PasswordInput;
        private Timer? InactivityTimer;
        private Timer? SessionCheckTimer;
        
        // Form fields
        protected string Password { get; set; } = "";
        
        // UI state
        protected bool IsVisible { get; set; } = false;
        protected bool IsUnlocking { get; set; } = false;
        protected string ErrorMessage { get; set; } = "";
        protected bool ShowPassword { get; set; } = false;
        protected string LockReason { get; set; } = "Session locked due to inactivity";
        protected DateTime LockTime { get; set; } = DateTime.Now;
        protected UserSession? CurrentSession { get; set; }
        
        // Security settings
        protected int UnlockAttempts { get; set; } = 0;
        protected int MaxUnlockAttempts { get; set; } = 5;
        protected TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(5);
        protected DateTime? LockoutEndTime { get; set; } = null;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            
            // Set up session check timer
            SessionCheckTimer = new Timer(1000); // Check every second
            SessionCheckTimer.Elapsed += async (sender, e) => await CheckSessionState();
            SessionCheckTimer.Start();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Subscribe to session changed events
                if (SessionManager is ISessionManager manager)
                {
                    // Use dynamic event subscription to avoid null reference during initialization
                    try
                    {
                        await JSRuntime.InvokeVoidAsync(
                            "eval",
                            "window.addEventListener('sessionlocked', function() { " +
                            "   window.dispatchEvent(new CustomEvent('sessionlockedinternal')); " +
                            "});"
                        );
                        
                        await JSRuntime.InvokeVoidAsync(
                            "eval",
                            "window.addEventListener('sessionlockedinternal', function() { " +
                            "   DotNet.invokeMethodAsync('HackerOs', 'OnSessionLocked'); " +
                            "});"
                        );
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to set up session lock event listeners");
                    }
                }
            }

            // Focus password input when lock screen is visible
            if (IsVisible)
            {
                try
                {
                    await JSRuntime.InvokeVoidAsync("eval", $"document.getElementById('unlock-password').focus()");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to focus password input");
                }
            }
        }

        /// <summary>
        /// Shows the lock screen and sets the lock time
        /// </summary>
        public async Task ShowLockScreen(string reason = "")
        {
            // Get the current session
            CurrentSession = await SessionManager.GetActiveSessionAsync();
            
            // Set the lock reason and time
            LockReason = !string.IsNullOrEmpty(reason) ? reason : "Session locked due to inactivity";
            LockTime = DateTime.Now;
            
            // Reset UI state
            Password = "";
            ErrorMessage = "";
            UnlockAttempts = 0;
            IsUnlocking = false;
            IsVisible = true;
            
            // Force UI update
            StateHasChanged();
        }

        /// <summary>
        /// Hides the lock screen
        /// </summary>
        public void HideLockScreen()
        {
            IsVisible = false;
            StateHasChanged();
        }

        /// <summary>
        /// Handles the unlock button click
        /// </summary>
        protected async Task HandleUnlock()
        {
            if (IsUnlocking) return;
            if (CurrentSession == null) return;

            // Check if user is in lockout period
            if (LockoutEndTime.HasValue && DateTime.Now < LockoutEndTime.Value)
            {
                var remainingMinutes = Math.Ceiling((LockoutEndTime.Value - DateTime.Now).TotalMinutes);
                ErrorMessage = $"Too many failed attempts. Try again in {remainingMinutes} minute(s).";
                return;
            }

            ErrorMessage = "";
            IsUnlocking = true;
            StateHasChanged();

            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(Password))
                {
                    ErrorMessage = "Please enter your password.";
                    IsUnlocking = false;
                    StateHasChanged();
                    return;
                }

                // Attempt to unlock
                var unlockSuccess = await SessionManager.UnlockSessionAsync(Password);
                
                if (unlockSuccess)
                {
                    Logger.LogInformation("Session unlocked successfully");
                    
                    // Call the success callback if provided
                    await OnUnlockAttempt.InvokeAsync(true);
                    
                    // Hide the lock screen
                    HideLockScreen();
                    
                    // Navigate to the specified URL
                    Navigation.NavigateTo(RedirectUrl);
                    
                    // Clear form
                    Password = "";
                }
                else
                {
                    UnlockAttempts++;
                    
                    // Check if max attempts reached
                    if (UnlockAttempts >= MaxUnlockAttempts)
                    {
                        LockoutEndTime = DateTime.Now.Add(LockoutDuration);
                        ErrorMessage = $"Too many failed attempts. Try again in {LockoutDuration.TotalMinutes} minutes.";
                        Logger.LogWarning("Max unlock attempts reached. Session locked out until {LockoutEnd}", LockoutEndTime);
                    }
                    else
                    {
                        ErrorMessage = $"Invalid password. Attempts remaining: {MaxUnlockAttempts - UnlockAttempts}";
                    }
                    
                    await OnUnlockAttempt.InvokeAsync(false);
                    Logger.LogWarning("Failed unlock attempt. Attempts: {UnlockAttempts}/{MaxUnlockAttempts}", UnlockAttempts, MaxUnlockAttempts);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred while unlocking. Please try again.";
                Logger.LogError(ex, "Error during session unlock");
                await OnUnlockAttempt.InvokeAsync(false);
            }
            finally
            {
                IsUnlocking = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Handles the logout button click
        /// </summary>
        protected async Task HandleLogout()
        {
            try
            {
                await AuthenticationService.LogoutAsync();
                Navigation.NavigateTo(LogoutUrl);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to log out. Please try again.";
                Logger.LogError(ex, "Error during logout from lock screen");
            }
        }

        /// <summary>
        /// Handles key press events in the password field
        /// </summary>
        protected async Task HandleKeyPress(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !IsUnlocking)
            {
                await HandleUnlock();
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
        /// Clears the error message
        /// </summary>
        protected void ClearError()
        {
            ErrorMessage = "";
        }

        /// <summary>
        /// Gets the user's initials for the avatar
        /// </summary>
        protected string GetUserInitials()
        {
            if (CurrentSession?.User == null) return "?";
            
            var username = CurrentSession.User.Username;
            if (string.IsNullOrEmpty(username)) return "?";
            
            return username.Substring(0, 1).ToUpper();
        }

        /// <summary>
        /// Formats the lock time
        /// </summary>
        protected string FormatLockTime()
        {
            return LockTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// Checks the current session state
        /// </summary>
        private async Task CheckSessionState()
        {
            // Execute on UI thread to avoid threading issues
            await InvokeAsync(async () =>
            {
                try
                {
                    var session = await SessionManager.GetActiveSessionAsync();
                    
                    // Check if session is locked
                    if (session != null && session.State == SessionState.Locked && !IsVisible)
                    {
                        // Show lock screen
                        await ShowLockScreen();
                    }
                    // Check if session is not locked but lock screen is visible
                    else if ((session == null || session.State != SessionState.Locked) && IsVisible)
                    {
                        // Hide lock screen
                        HideLockScreen();
                    }
                    
                    // Update current session
                    CurrentSession = session;
                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error checking session state");
                }
            });
        }

        public void Dispose()
        {
            // Clean up timers
            InactivityTimer?.Dispose();
            SessionCheckTimer?.Dispose();
        }
    }
}
