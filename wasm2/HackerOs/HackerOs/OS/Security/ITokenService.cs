using System;

namespace HackerOs.OS.Security
{
    /// <summary>
    /// Interface for token generation and validation services in HackerOS.
    /// </summary>
    public interface ITokenService
    {
        /// <summary>
        /// Generates a new authentication token for the specified user.
        /// </summary>
        /// <param name="user">The user for whom to generate a token.</param>
        /// <returns>A new authentication token.</returns>
        string GenerateToken(HackerOs.OS.User.User user);

        /// <summary>
        /// Validates the specified authentication token.
        /// </summary>
        /// <param name="token">The token to validate.</param>
        /// <returns>True if the token is valid, false otherwise.</returns>
        bool ValidateToken(string token);

        /// <summary>
        /// Refreshes the specified authentication token, extending its lifetime.
        /// </summary>
        /// <param name="token">The token to refresh.</param>
        /// <returns>A new token with extended lifetime, or null if the token is invalid.</returns>
        string? RefreshToken(string token);

        /// <summary>
        /// Gets the time remaining until the specified token expires.
        /// </summary>
        /// <param name="token">The token to check.</param>
        /// <returns>The time remaining until the token expires, or TimeSpan.Zero if the token is invalid or already expired.</returns>
        TimeSpan GetTokenTimeToExpiry(string token);

        /// <summary>
        /// Extracts the user information from the specified token.
        /// </summary>
        /// <param name="token">The token to extract user information from.</param>
        /// <returns>The user associated with the token, or null if the token is invalid.</returns>
        HackerOs.OS.User.User? GetUserFromToken(string token);
    }

    /// <summary>
    /// Represents token validation results.
    /// </summary>
    public class TokenValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the token is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets the user associated with the token, if the token is valid.
        /// </summary>
        public HackerOs.OS.User.User? User { get; set; }

        /// <summary>
        /// Gets the error message if the token validation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets the expiration time of the token.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Creates a successful token validation result.
        /// </summary>
        /// <param name="user">The user associated with the token.</param>
        /// <param name="expiresAt">The expiration time of the token.</param>
        /// <returns>A TokenValidationResult representing a successful validation.</returns>
        public static TokenValidationResult Success(HackerOs.OS.User.User user, DateTime expiresAt)
        {
            return new TokenValidationResult
            {
                IsValid = true,
                User = user,
                ExpiresAt = expiresAt
            };
        }

        /// <summary>
        /// Creates a failed token validation result.
        /// </summary>
        /// <param name="errorMessage">The error message explaining why validation failed.</param>
        /// <returns>A TokenValidationResult representing a failed validation.</returns>
        public static TokenValidationResult Failure(string errorMessage)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
