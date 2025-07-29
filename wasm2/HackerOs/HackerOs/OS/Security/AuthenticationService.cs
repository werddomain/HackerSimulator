using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using HackerOs.OS.User; // Add this using statement

namespace HackerOs.OS.Security
{
    /// <summary>
    /// Implementation of the IAuthenticationService interface for user authentication.
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly ISessionManager _sessionManager;
        private readonly ITokenService _tokenService;
        private readonly IJSRuntime _jsRuntime;
        private readonly IUserService _userService;

        // Cache for the current authentication state
        private bool _isAuthenticated;
        private string? _currentToken;
        private string? _currentUsername;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationService"/> class.
        /// </summary>
        /// <param name="sessionManager">The session manager.</param>
        /// <param name="tokenService">The token service.</param>
        /// <param name="jsRuntime">The JavaScript runtime for browser storage access.</param>
        /// <param name="userService">The user service for user validation.</param>
        public AuthenticationService(
            ISessionManager sessionManager,
            ITokenService tokenService,
            IJSRuntime jsRuntime,
            IUserService userService)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            
            // Initialize state
            _isAuthenticated = false;
            _currentToken = null;
            _currentUsername = null;

            // Subscribe to session manager events
            _sessionManager.SessionChanged += OnSessionChanged;
        }

        /// <summary>
        /// Event triggered when the authentication state changes.
        /// </summary>
        public event EventHandler<AuthenticationStateChangedEventArgs>? AuthenticationStateChanged;

        /// <summary>
        /// Initializes the authentication service by checking for existing sessions.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InitializeAsync()
        {
            // Get the active session
            var activeSession = await _sessionManager.GetActiveSessionAsync();
            if (activeSession != null && activeSession.State == SessionState.Active)
            {
                // Validate the token
                if (_tokenService.ValidateToken(activeSession.Token))
                {
                    // Set authentication state
                    _isAuthenticated = true;
                    _currentToken = activeSession.Token;
                    _currentUsername = activeSession.User.Username;

                    // Raise authentication state changed event
                    OnAuthenticationStateChanged(true, _currentUsername);
                }
                else
                {
                    // Token is invalid, clear authentication state
                    _isAuthenticated = false;
                    _currentToken = null;
                    _currentUsername = null;

                    // End the session
                    await _sessionManager.EndSessionAsync(activeSession.SessionId);

                    // Raise authentication state changed event
                    OnAuthenticationStateChanged(false);
                }
            }
            else
            {
                // No active session, clear authentication state
                _isAuthenticated = false;
                _currentToken = null;
                _currentUsername = null;

                // Raise authentication state changed event
                OnAuthenticationStateChanged(false);
            }
        }

        /// <summary>
        /// Attempts to authenticate a user with the provided credentials.
        /// </summary>
        /// <param name="username">The username of the user attempting to log in.</param>
        /// <param name="password">The password of the user attempting to log in.</param>
        /// <param name="rememberMe">Whether to remember the user's login.</param>
        /// <returns>An AuthenticationResult containing the result of the login attempt.</returns>
        public async Task<AuthenticationResult> LoginAsync(string username, string password, bool? rememberMe)
        {
            if (string.IsNullOrEmpty(username))
            {
                return AuthenticationResult.Failure("Username cannot be empty.");
            }

            if (string.IsNullOrEmpty(password))
            {
                return AuthenticationResult.Failure("Password cannot be empty.");
            }

            try
            {
                // Validate user credentials
                HackerOs.OS.User.User? user = await _userService.AuthenticateAsync(username, password);
                if (user == null)
                {
                    return AuthenticationResult.Failure("Invalid username or password.");
                }

                // Create a new session for the user
                var session = await _sessionManager.CreateSessionAsync(user);

                // Set authentication state
                _isAuthenticated = true;
                _currentToken = session.Token;
                _currentUsername = user.Username;

                // Save authentication preference if "remember me" is selected
                // This would typically be done by setting a longer expiration on the token
                // and storing it in localStorage

                // Raise authentication state changed event
                OnAuthenticationStateChanged(true, _currentUsername);

                // Update user's last login time
                user.LastLogin = DateTime.UtcNow;
                await _userService.UpdateUserAsync(user);

                return AuthenticationResult.Success(_currentUsername, _currentToken);
            }
            catch (Exception ex)
            {
                return AuthenticationResult.Failure($"Login failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs out the current user, ending their session.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task LogoutAsync()
        {
            // Get the active session
            var activeSession = await _sessionManager.GetActiveSessionAsync();
            if (activeSession != null)
            {
                // End the session
                await _sessionManager.EndSessionAsync(activeSession.SessionId);
            }

            // Clear authentication state
            _isAuthenticated = false;
            _currentToken = null;
            _currentUsername = null;

            // Clear token from localStorage
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "hackerOs.token");

            // Raise authentication state changed event
            OnAuthenticationStateChanged(false);
        }

        /// <summary>
        /// Validates whether a session token is valid and not expired.
        /// </summary>
        /// <param name="token">The session token to validate.</param>
        /// <returns>True if the token is valid, false otherwise.</returns>
        public Task<bool> ValidateSessionAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return Task.FromResult(false);
            }

            bool isValid = _tokenService.ValidateToken(token);
            return Task.FromResult(isValid);
        }

        /// <summary>
        /// Refreshes an existing token, extending its lifetime.
        /// </summary>
        /// <param name="token">The token to refresh.</param>
        /// <returns>A new token with extended lifetime, or null if the token is invalid.</returns>
        public async Task<string> RefreshTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("Token cannot be null or empty.", nameof(token));
            }

            string? newToken = _tokenService.RefreshToken(token);
            if (newToken == null)
            {
                throw new InvalidOperationException("Token refresh failed.");
            }

            // If this is the current token, update the authentication state
            if (token == _currentToken)
            {
                _currentToken = newToken;

                // Update the token in the active session
                var activeSession = await _sessionManager.GetActiveSessionAsync();
                if (activeSession != null)
                {
                    activeSession.Token = newToken;
                    activeSession.UpdateActivity();
                }
            }

            return newToken;
        }

        /// <summary>
        /// Checks if the current user is authenticated.
        /// </summary>
        /// <returns>True if the user is authenticated, false otherwise.</returns>
        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(_isAuthenticated);
        }

        /// <summary>
        /// Handler for session state changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void OnSessionChanged(object? sender, SessionChangedEventArgs e)
        {
            // Update authentication state based on session changes
            if (e.State == SessionState.Terminated)
            {
                // Session ended, clear authentication state if it was the active session
                var activeSession = _sessionManager.GetActiveSessionAsync().Result;
                if (activeSession == null)
                {
                    _isAuthenticated = false;
                    _currentToken = null;
                    _currentUsername = null;
                    OnAuthenticationStateChanged(false);
                }
            }
            else if (e.State == SessionState.Active)
            {
                // Session activated, update authentication state
                _isAuthenticated = true;
                _currentUsername = e.User?.Username;
                var activeSession = _sessionManager.GetActiveSessionAsync().Result;
                if (activeSession != null)
                {
                    _currentToken = activeSession.Token;
                }
                OnAuthenticationStateChanged(true, _currentUsername);
            }
            // No need to update authentication state for Locked sessions
        }

        /// <summary>
        /// Raises the AuthenticationStateChanged event.
        /// </summary>
        /// <param name="isAuthenticated">Whether the user is authenticated.</param>
        /// <param name="username">The username of the authenticated user.</param>
        private void OnAuthenticationStateChanged(bool isAuthenticated, string? username = null)
        {
            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs(isAuthenticated, username));
        }
    }

    /// <summary>
    /// Interface for user-related operations.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Authenticates a user with the provided credentials.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>The authenticated user or null if authentication fails.</returns>
        Task<User.User?> AuthenticateAsync(string username, string password);

        /// <summary>
        /// Gets a user by username.
        /// </summary>
        /// <param name="username">The username to look up.</param>
        /// <returns>The user if found, or null if not found.</returns>
        Task<User.User?> GetUserAsync(string username);

        /// <summary>
        /// Updates a user's information.
        /// </summary>
        /// <param name="user">The user to update.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UpdateUserAsync(User.User user);

        /// <summary>
        /// Creates a new user account.
        /// </summary>
        /// <param name="username">The username for the new account.</param>
        /// <param name="fullName">The full name of the user.</param>
        /// <param name="password">The password for the new account.</param>
        /// <param name="isAdmin">Whether the user should have administrative privileges.</param>
        /// <returns>The newly created user.</returns>
        Task<User.User> CreateUserAsync(string username, string fullName, string password, bool isAdmin = false);
    }
}
