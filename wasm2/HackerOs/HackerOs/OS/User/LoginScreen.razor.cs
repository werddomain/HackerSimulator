using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HackerOs.OS.Security;
using HackerOs.OS.User.Models;

namespace HackerOs.OS.User
{
    /// <summary>
    /// Login screen component for user authentication
    /// </summary>
    public partial class LoginScreen : ComponentBase
    {
        [Inject] private IUserManager UserManager { get; set; } = null!;
        [Inject] private ISessionManager SessionManager { get; set; } = null!;
        [Inject] private ILogger<LoginScreen> Logger { get; set; } = null!;

        [Parameter] public EventCallback<UserSession> OnLoginSuccess { get; set; }
        [Parameter] public EventCallback<string> OnLoginFailed { get; set; }

        private string _username = "";
        private string _password = "";
        private bool _isLoggingIn = false;
        private string _errorMessage = "";
        private bool _showPassword = false;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                // Check if there's already an active session
                var modelSession = await SessionManager.GetActiveSessionAsync();
                if (modelSession is not null)
                {
                    Models.UserSession session = modelSession;
                    if (session.State != SessionState.Locked)
                    {
                        // Convert the Models.UserSession to User.UserSession for the callback
                        var userSession = new UserSession
                        {
                            SessionId = session.SessionId,
                            Token = session.Token,
                            User = session.User,
                            State = session.State,
                            LastActivity = session.LastActivity,
                            StartTime = session.StartTime
                        };

                        await OnLoginSuccess.InvokeAsync(userSession);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking active session");
            }
        }

        private async Task HandleLogin()
        {
            if (_isLoggingIn) return;

            _errorMessage = "";
            _isLoggingIn = true;

            try
            {
                if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
                {
                    _errorMessage = "Please enter both username and password.";
                    return;
                }

                var user = await UserManager.AuthenticateAsync(_username, _password);
                if (user != null)
                {
                    var session = await SessionManager.CreateSessionAsync(user);
                    await SessionManager.SwitchSessionAsync(session.SessionId);
                    
                    Logger.LogInformation("User {Username} logged in successfully", _username);
                    // Convert the Models.UserSession to User.UserSession for the callback
                    var userSession = new UserSession
                    {
                        SessionId = session.SessionId,
                        Token = session.Token,
                        User = session.User,
                        State = session.State,
                        LastActivity = session.LastActivity,
                        StartTime = session.StartTime
                    };
                    await OnLoginSuccess.InvokeAsync(userSession);
                    
                    // Clear form
                    _username = "";
                    _password = "";
                }
                else
                {
                    _errorMessage = "Invalid username or password.";
                    await OnLoginFailed.InvokeAsync(_errorMessage);
                    Logger.LogWarning("Failed login attempt for username: {Username}", _username);
                }
            }
            catch (Exception ex)
            {
                _errorMessage = "An error occurred during login. Please try again.";
                Logger.LogError(ex, "Error during login for username: {Username}", _username);
                await OnLoginFailed.InvokeAsync(_errorMessage);
            }
            finally
            {
                _isLoggingIn = false;
                StateHasChanged();
            }
        }

        private async Task HandleKeyPress(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !_isLoggingIn)
            {
                await HandleLogin();
            }
        }

        private void TogglePasswordVisibility()
        {
            _showPassword = !_showPassword;
        }

        private void ClearError()
        {
            _errorMessage = "";
        }
    }
}
