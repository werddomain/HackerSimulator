using ProxyServer.Protocol.Models.Control;

namespace ProxyServer.Authentication
{
    /// <summary>
    /// Service interface for handling authentication and authorization.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Authenticates a client using API key.
        /// </summary>
        /// <param name="apiKey">The API key provided by the client.</param>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="clientVersion">The client version.</param>
        /// <returns>Authentication result containing success status and token if successful.</returns>
        Task<AuthenticationResult> AuthenticateAsync(string apiKey, string clientId, string clientVersion);

        /// <summary>
        /// Validates an existing session token.
        /// </summary>
        /// <param name="sessionToken">The session token to validate.</param>
        /// <returns>True if the token is valid, false otherwise.</returns>
        Task<bool> ValidateTokenAsync(string sessionToken);

        /// <summary>
        /// Revokes a session token (logout).
        /// </summary>
        /// <param name="sessionToken">The session token to revoke.</param>
        /// <returns>True if the token was successfully revoked, false otherwise.</returns>
        Task<bool> RevokeTokenAsync(string sessionToken);

        /// <summary>
        /// Gets the session information for a token.
        /// </summary>
        /// <param name="sessionToken">The session token.</param>
        /// <returns>Session information if the token is valid, null otherwise.</returns>
        Task<SessionInfo?> GetSessionAsync(string sessionToken);

        /// <summary>
        /// Checks if a client has permission to perform a specific operation.
        /// </summary>
        /// <param name="sessionToken">The session token.</param>
        /// <param name="operation">The operation to check.</param>
        /// <param name="resource">The resource being accessed (optional).</param>
        /// <returns>True if the client has permission, false otherwise.</returns>
        Task<bool> HasPermissionAsync(string sessionToken, string operation, string? resource = null);

        /// <summary>
        /// Gets active session count.
        /// </summary>
        /// <returns>Number of active sessions.</returns>
        int GetActiveSessionCount();

        /// <summary>
        /// Cleans up expired sessions.
        /// </summary>
        /// <returns>Number of sessions cleaned up.</returns>
        Task<int> CleanupExpiredSessionsAsync();
    }

    /// <summary>
    /// Result of an authentication attempt.
    /// </summary>
    public class AuthenticationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether authentication was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the session token if authentication was successful.
        /// </summary>
        public string? SessionToken { get; set; }

        /// <summary>
        /// Gets or sets the error message if authentication failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the expiration time of the token.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the user information.
        /// </summary>
        public UserInfo? UserInfo { get; set; }        /// <summary>
        /// Creates a successful authentication result.
        /// </summary>
        /// <param name="sessionToken">The session token.</param>
        /// <param name="expiresAt">The expiration time.</param>
        /// <param name="userInfo">The user information.</param>
        /// <returns>A successful authentication result.</returns>
        public static AuthenticationResult Successful(string sessionToken, DateTime expiresAt, UserInfo userInfo)
        {
            return new AuthenticationResult
            {
                Success = true,
                SessionToken = sessionToken,
                ExpiresAt = expiresAt,
                UserInfo = userInfo
            };
        }

        /// <summary>
        /// Creates a failed authentication result.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A failed authentication result.</returns>
        public static AuthenticationResult Failure(string errorMessage)
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// Information about an active session.
    /// </summary>
    public class SessionInfo
    {
        /// <summary>
        /// Gets or sets the session token.
        /// </summary>
        public string SessionToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client identifier.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client version.
        /// </summary>
        public string ClientVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the session creation time.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the session expiration time.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the last activity time.
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Gets or sets the user information.
        /// </summary>
        public UserInfo UserInfo { get; set; } = new();

        /// <summary>
        /// Gets a value indicating whether the session is expired.
        /// </summary>
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        /// <summary>
        /// Updates the last activity time to now.
        /// </summary>
        public void UpdateActivity()
        {
            LastActivity = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Information about a user.
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// Gets or sets the user identifier.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user role.
        /// </summary>
        public string Role { get; set; } = "user";

        /// <summary>
        /// Gets or sets the user permissions.
        /// </summary>
        public HashSet<string> Permissions { get; set; } = new();

        /// <summary>
        /// Checks if the user has a specific permission.
        /// </summary>
        /// <param name="permission">The permission to check.</param>
        /// <returns>True if the user has the permission, false otherwise.</returns>
        public bool HasPermission(string permission)
        {
            return Permissions.Contains(permission) || Permissions.Contains("*");
        }
    }
}
