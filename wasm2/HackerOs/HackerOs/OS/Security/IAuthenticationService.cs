using System;
using System.Threading.Tasks;

namespace HackerOs.OS.Security
{
    /// <summary>
    /// Interface for authentication services in HackerOS.
    /// Provides authentication functionality including login, logout, and session validation.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Attempts to authenticate a user with the provided credentials.
        /// </summary>
        /// <param name="username">The username of the user attempting to log in.</param>
        /// <param name="password">The password of the user attempting to log in.</param>
        /// <returns>An AuthenticationResult containing the result of the login attempt.</returns>
        Task<AuthenticationResult> LoginAsync(string username, string password);

        /// <summary>
        /// Logs out the current user, ending their session.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LogoutAsync();

        /// <summary>
        /// Validates whether a session token is valid and not expired.
        /// </summary>
        /// <param name="token">The session token to validate.</param>
        /// <returns>True if the token is valid, false otherwise.</returns>
        Task<bool> ValidateSessionAsync(string token);

        /// <summary>
        /// Refreshes an existing token, extending its lifetime.
        /// </summary>
        /// <param name="token">The token to refresh.</param>
        /// <returns>A new token with extended lifetime, or null if the token is invalid.</returns>
        Task<string> RefreshTokenAsync(string token);

        /// <summary>
        /// Checks if the current user is authenticated.
        /// </summary>
        /// <returns>True if the user is authenticated, false otherwise.</returns>
        Task<bool> IsAuthenticatedAsync();

        /// <summary>
        /// Initializes the authentication service.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InitializeAsync();

        /// <summary>
        /// Event triggered when the authentication state changes.
        /// </summary>
        event EventHandler<AuthenticationStateChangedEventArgs> AuthenticationStateChanged;
    }

    /// <summary>
    /// Represents the result of an authentication attempt.
    /// </summary>
    public class AuthenticationResult
    {
        /// <summary>
        /// Gets a value indicating whether the authentication was successful.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Gets the authentication token if the authentication was successful.
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// Gets the username of the authenticated user if the authentication was successful.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Gets the error message if the authentication failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Creates a successful authentication result.
        /// </summary>
        /// <param name="username">The username of the authenticated user.</param>
        /// <param name="token">The authentication token.</param>
        /// <returns>An AuthenticationResult representing a successful authentication.</returns>
        public static AuthenticationResult Success(string username, string token)
        {
            return new AuthenticationResult
            {
                IsAuthenticated = true,
                Username = username,
                Token = token
            };
        }

        /// <summary>
        /// Creates a failed authentication result.
        /// </summary>
        /// <param name="errorMessage">The error message explaining why authentication failed.</param>
        /// <returns>An AuthenticationResult representing a failed authentication.</returns>
        public static AuthenticationResult Failure(string errorMessage)
        {
            return new AuthenticationResult
            {
                IsAuthenticated = false,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// EventArgs for authentication state changes.
    /// </summary>
    public class AuthenticationStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a value indicating whether the user is authenticated.
        /// </summary>
        public bool IsAuthenticated { get; }

        /// <summary>
        /// Gets the username of the authenticated user, or null if not authenticated.
        /// </summary>
        public string? Username { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="isAuthenticated">Whether the user is authenticated.</param>
        /// <param name="username">The username of the authenticated user.</param>
        public AuthenticationStateChangedEventArgs(bool isAuthenticated, string? username = null)
        {
            IsAuthenticated = isAuthenticated;
            Username = username;
        }
    }
}
