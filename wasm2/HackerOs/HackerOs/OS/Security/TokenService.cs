using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HackerOs.OS.Security
{
    /// <summary>
    /// Implementation of the ITokenService interface for token generation and validation.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly TimeSpan _tokenLifetime;
        private readonly Dictionary<string, TokenInfo> _tokenCache;
        private readonly byte[] _secretKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenService"/> class.
        /// </summary>
        public TokenService()
        {
            // Default token lifetime is 30 minutes
            _tokenLifetime = TimeSpan.FromMinutes(30);
            _tokenCache = new Dictionary<string, TokenInfo>();
            
            // Generate a random secret key for token signing
            _secretKey = new byte[32]; // 256 bits
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(_secretKey);
            }
        }

        /// <summary>
        /// Generates a new authentication token for the specified user.
        /// </summary>
        /// <param name="user">The user for whom to generate a token.</param>
        /// <returns>A new authentication token.</returns>
        public string GenerateToken(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            // Create token payload
            var tokenData = new TokenPayload
            {
                UserId = user.UserId,
                Username = user.Username,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow + _tokenLifetime
            };

            // Serialize token data
            string json = JsonSerializer.Serialize(tokenData);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            
            // Base64 encode the payload
            string base64Payload = Convert.ToBase64String(jsonBytes);
            
            // Generate signature
            string signature = GenerateSignature(base64Payload);
            
            // Combine payload and signature
            string token = $"{base64Payload}.{signature}";
            
            // Store token in cache
            _tokenCache[token] = new TokenInfo
            {
                User = user,
                ExpiresAt = tokenData.ExpiresAt
            };
            
            return token;
        }

        /// <summary>
        /// Validates the specified authentication token.
        /// </summary>
        /// <param name="token">The token to validate.</param>
        /// <returns>True if the token is valid, false otherwise.</returns>
        public bool ValidateToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            try
            {
                // Check token format
                string[] parts = token.Split('.');
                if (parts.Length != 2)
                {
                    return false;
                }

                string payload = parts[0];
                string signature = parts[1];

                // Verify signature
                string expectedSignature = GenerateSignature(payload);
                if (signature != expectedSignature)
                {
                    return false;
                }

                // Decode payload
                byte[] payloadBytes = Convert.FromBase64String(payload);
                string json = Encoding.UTF8.GetString(payloadBytes);
                var tokenData = JsonSerializer.Deserialize<TokenPayload>(json);

                if (tokenData == null)
                {
                    return false;
                }

                // Check expiration
                if (tokenData.ExpiresAt < DateTime.UtcNow)
                {
                    // Remove expired token from cache
                    if (_tokenCache.ContainsKey(token))
                    {
                        _tokenCache.Remove(token);
                    }
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Refreshes the specified authentication token, extending its lifetime.
        /// </summary>
        /// <param name="token">The token to refresh.</param>
        /// <returns>A new token with extended lifetime, or null if the token is invalid.</returns>
        public string? RefreshToken(string token)
        {
            if (!ValidateToken(token))
            {
                return null;
            }

            User? user = GetUserFromToken(token);
            if (user == null)
            {
                return null;
            }

            // Remove old token from cache
            _tokenCache.Remove(token);

            // Generate new token
            return GenerateToken(user);
        }

        /// <summary>
        /// Gets the time remaining until the specified token expires.
        /// </summary>
        /// <param name="token">The token to check.</param>
        /// <returns>The time remaining until the token expires, or TimeSpan.Zero if the token is invalid or already expired.</returns>
        public TimeSpan GetTokenTimeToExpiry(string token)
        {
            if (string.IsNullOrEmpty(token) || !ValidateToken(token))
            {
                return TimeSpan.Zero;
            }

            try
            {
                // Check if token is in cache
                if (_tokenCache.TryGetValue(token, out TokenInfo? tokenInfo) && tokenInfo != null)
                {
                    DateTime now = DateTime.UtcNow;
                    if (tokenInfo.ExpiresAt > now)
                    {
                        return tokenInfo.ExpiresAt - now;
                    }
                }

                // If not in cache, decode the token
                string[] parts = token.Split('.');
                string payload = parts[0];
                
                byte[] payloadBytes = Convert.FromBase64String(payload);
                string json = Encoding.UTF8.GetString(payloadBytes);
                var tokenData = JsonSerializer.Deserialize<TokenPayload>(json);

                if (tokenData == null)
                {
                    return TimeSpan.Zero;
                }

                DateTime now = DateTime.UtcNow;
                if (tokenData.ExpiresAt > now)
                {
                    return tokenData.ExpiresAt - now;
                }

                return TimeSpan.Zero;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Extracts the user information from the specified token.
        /// </summary>
        /// <param name="token">The token to extract user information from.</param>
        /// <returns>The user associated with the token, or null if the token is invalid.</returns>
        public User? GetUserFromToken(string token)
        {
            if (string.IsNullOrEmpty(token) || !ValidateToken(token))
            {
                return null;
            }

            // Check if token is in cache
            if (_tokenCache.TryGetValue(token, out TokenInfo? tokenInfo) && tokenInfo != null)
            {
                return tokenInfo.User;
            }

            try
            {
                // Decode the token payload
                string[] parts = token.Split('.');
                string payload = parts[0];
                
                byte[] payloadBytes = Convert.FromBase64String(payload);
                string json = Encoding.UTF8.GetString(payloadBytes);
                var tokenData = JsonSerializer.Deserialize<TokenPayload>(json);

                if (tokenData == null)
                {
                    return null;
                }

                // In a real system, we'd look up the user from the UserManager
                // For now, we can only return users from the cache
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Generates a signature for the given payload.
        /// </summary>
        /// <param name="payload">The payload to sign.</param>
        /// <returns>The signature.</returns>
        private string GenerateSignature(string payload)
        {
            using (var hmac = new HMACSHA256(_secretKey))
            {
                byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
                byte[] hash = hmac.ComputeHash(payloadBytes);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Represents the payload of a token.
        /// </summary>
        private class TokenPayload
        {
            /// <summary>
            /// Gets or sets the user ID associated with this token.
            /// </summary>
            public string UserId { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the username associated with this token.
            /// </summary>
            public string Username { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the time when this token was issued.
            /// </summary>
            public DateTime IssuedAt { get; set; }

            /// <summary>
            /// Gets or sets the time when this token expires.
            /// </summary>
            public DateTime ExpiresAt { get; set; }
        }

        /// <summary>
        /// Represents cached token information.
        /// </summary>
        private class TokenInfo
        {
            /// <summary>
            /// Gets or sets the user associated with this token.
            /// </summary>
            public User User { get; set; } = null!;

            /// <summary>
            /// Gets or sets the time when this token expires.
            /// </summary>
            public DateTime ExpiresAt { get; set; }
        }
    }
}
