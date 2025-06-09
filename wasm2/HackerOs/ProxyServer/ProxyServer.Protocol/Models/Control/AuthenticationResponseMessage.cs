using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models.Control
{
    /// <summary>
    /// User information data transfer object.
    /// </summary>
    public class UserInfoDto
    {
        /// <summary>
        /// Gets or sets the user identifier.
        /// </summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user role.
        /// </summary>
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user permissions.
        /// </summary>
        [JsonPropertyName("permissions")]
        public List<string> Permissions { get; set; } = new();
    }

    /// <summary>
    /// Message for responding to an authentication request.
    /// </summary>
    public class AuthenticationResponseMessage : MessageBase
    {
        /// <summary>
        /// Gets or sets a value indicating whether authentication was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the session token if authentication was successful.
        /// </summary>
        [JsonPropertyName("sessionToken")]
        public string? SessionToken { get; set; }

        /// <summary>
        /// Gets or sets the error message if authentication failed.
        /// </summary>
        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the token expiration time.
        /// </summary>
        [JsonPropertyName("expiresAt")]
        public string? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the user information if authentication was successful.
        /// </summary>
        [JsonPropertyName("userInfo")]
        public UserInfoDto? UserInfo { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationResponseMessage"/> class.
        /// </summary>
        public AuthenticationResponseMessage() : base(MessageType.AUTHENTICATE_RESPONSE)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationResponseMessage"/> class for a successful authentication.
        /// </summary>
        /// <param name="sessionToken">The session token.</param>
        /// <param name="expiresAt">The expiration date/time in ISO8601 format.</param>
        /// <returns>A new authentication response message.</returns>
        public static AuthenticationResponseMessage CreateSuccess(string sessionToken, string expiresAt)
        {
            return new AuthenticationResponseMessage
            {
                Success = true,
                SessionToken = sessionToken,
                ExpiresAt = expiresAt
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationResponseMessage"/> class for a failed authentication.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A new authentication response message.</returns>
        public static AuthenticationResponseMessage Failure(string errorMessage)
        {
            return new AuthenticationResponseMessage
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
