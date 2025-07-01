using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;
using HackerOs.OS.Security;
using Microsoft.AspNetCore.Components.Web.Virtualization;

namespace HackerOs.Components.Authentication
{
    /// <summary>
    /// Login screen component for user authentication
    /// </summary>
    public partial class LoginScreen : ComponentBase
    {
        [Inject] private IAuthenticationService AuthenticationService { get; set; } = null!;
        [Inject] private ILogger<LoginScreen> Logger { get; set; } = null!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] private NavigationManager Navigation { get; set; } = null!;

        [Parameter] public EventCallback<AuthenticationResult> OnLoginSuccess { get; set; }
        [Parameter] public EventCallback<string> OnLoginFailed { get; set; }
        [Parameter] public string RedirectUrl { get; set; } = "/desktop";

        private ElementReference UsernameInput;
        
        // Form fields
        protected string Username { get; set; } = "";
        protected string Password { get; set; } = "";
        protected bool RememberMe { get; set; } = false;
        
        // UI state
        protected bool IsLoggingIn { get; set; } = false;
        protected string ErrorMessage { get; set; } = "";
        protected bool ShowPassword { get; set; } = false;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Focus the username input field
                try
                {
                    await JSRuntime.InvokeVoidAsync("eval", $"document.getElementById('username').focus()");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to focus username input");
                }
            }
        }

        protected async Task HandleLogin()
        {
            if (IsLoggingIn) return;

            ErrorMessage = "";
            IsLoggingIn = true;
            StateHasChanged();

            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
                {
                    ErrorMessage = "Please enter both username and password.";
                    return;
                }

                // Attempt login
                var result = await AuthenticationService.LoginAsync(Username, Password, RememberMe);
                
                if (result.Success)
                {
                    Logger.LogInformation("User {Username} logged in successfully", Username);
                    
                    // Call the success callback if provided
                    await OnLoginSuccess.InvokeAsync(result);
                    
                    // Navigate to the specified URL
                    Navigation.NavigateTo(RedirectUrl);
                    
                    // Clear form
                    Username = "";
                    Password = "";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage ?? "Invalid username or password.";
                    await OnLoginFailed.InvokeAsync(ErrorMessage);
                    Logger.LogWarning("Failed login attempt for username: {Username}", Username);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred during login. Please try again.";
                Logger.LogError(ex, "Error during login for username: {Username}", Username);
                await OnLoginFailed.InvokeAsync(ErrorMessage);
            }
            finally
            {
                IsLoggingIn = false;
                StateHasChanged();
            }
        }

        protected async Task HandleKeyPress(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !IsLoggingIn)
            {
                await HandleLogin();
            }
        }

        protected void TogglePasswordVisibility()
        {
            ShowPassword = !ShowPassword;
        }

        protected void ClearError()
        {
            ErrorMessage = "";
        }
    }
}
