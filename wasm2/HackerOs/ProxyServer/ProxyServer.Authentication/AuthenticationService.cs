using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ProxyServer.Authentication
{    /// <summary>
    /// Default implementation of the authentication service.
    /// </summary>
    public class AuthenticationService : IAuthenticationService, IDisposable
    {
        private readonly ILogger<AuthenticationService> _logger;
        private readonly AuthenticationConfiguration _configuration;
        private readonly Dictionary<string, ApiKeyConfiguration> _apiKeyLookup = new();
        private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
        private readonly ConcurrentDictionary<string, List<string>> _clientSessions = new();
        private readonly Timer _cleanupTimer;
        private readonly object _lock = new();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationService"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="configuration">The authentication configuration.</param>
        public AuthenticationService(ILogger<AuthenticationService> logger, AuthenticationConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Build API key lookup dictionary
            foreach (var apiKeyConfig in _configuration.ApiKeys.Where(k => k.IsEnabled))
            {
                _apiKeyLookup[apiKeyConfig.Key] = apiKeyConfig;
            }

            // Initialize with default API keys if none provided
            if (_apiKeyLookup.Count == 0)
            {
                var defaultConfig = new ApiKeyConfiguration
                {
                    Key = "default-api-key-12345678901234567890",
                    UserId = "default-user",
                    Role = "admin",
                    DisplayName = "Default Development Key",
                    IsEnabled = true
                };
                _apiKeyLookup[defaultConfig.Key] = defaultConfig;
                _logger.LogWarning("No API keys configured, using default development key");
            }

            // Start cleanup timer
            _cleanupTimer = new Timer(async _ => await CleanupExpiredSessionsAsync(),
                null,
                TimeSpan.FromMinutes(_configuration.SessionCleanupIntervalMinutes),
                TimeSpan.FromMinutes(_configuration.SessionCleanupIntervalMinutes));

            _logger.LogInformation("Authentication service initialized with {ApiKeyCount} valid API keys",
                _apiKeyLookup.Count);
        }        /// <inheritdoc />
        public async Task<AuthenticationResult> AuthenticateAsync(string apiKey, string clientId, string clientVersion)
        {
            try
            {
                _logger.LogDebug("Authentication attempt for client {ClientId} with API key ending in ...{ApiKeySuffix}",
                    clientId, apiKey?.Length > 4 ? apiKey[^4..] : "****");

                // Validate API key
                if (string.IsNullOrEmpty(apiKey) || !_apiKeyLookup.TryGetValue(apiKey, out var apiKeyConfig))
                {
                    _logger.LogWarning("Invalid API key used by client {ClientId}", clientId);
                    return AuthenticationResult.Failure("Invalid API key");
                }

                // Check session limits for this client
                lock (_lock)
                {
                    if (_clientSessions.TryGetValue(clientId, out var existingSessions))
                    {
                        if (existingSessions.Count >= _configuration.MaxSessionsPerClient)
                        {
                            _logger.LogWarning("Client {ClientId} exceeded maximum session limit ({MaxSessions})",
                                clientId, _configuration.MaxSessionsPerClient);
                            return AuthenticationResult.Failure("Maximum session limit exceeded");
                        }
                    }
                }

                // Create new session
                var sessionToken = GenerateSessionToken();
                var expiresAt = DateTime.UtcNow.AddMinutes(_configuration.SessionTimeoutMinutes);
                var userInfo = CreateUserInfo(apiKeyConfig, clientId);

                var sessionInfo = new SessionInfo
                {
                    SessionToken = sessionToken,
                    ClientId = clientId,
                    ClientVersion = clientVersion,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    LastActivity = DateTime.UtcNow,
                    UserInfo = userInfo
                };

                // Store session
                _sessions[sessionToken] = sessionInfo;

                // Track client sessions
                lock (_lock)
                {
                    if (!_clientSessions.TryGetValue(clientId, out var clientSessionList))
                    {
                        clientSessionList = new List<string>();
                        _clientSessions[clientId] = clientSessionList;
                    }
                    clientSessionList.Add(sessionToken);
                }

                _logger.LogInformation("Authentication successful for client {ClientId}, user {UserId}, session expires at {ExpiresAt}",
                    clientId, userInfo.UserId, expiresAt);

                return AuthenticationResult.Successful(sessionToken, expiresAt, userInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication for client {ClientId}", clientId);
                return AuthenticationResult.Failure("Authentication error occurred");
            }
        }

        /// <inheritdoc />
        public async Task<bool> ValidateTokenAsync(string sessionToken)
        {
            if (string.IsNullOrEmpty(sessionToken))
                return false;

            if (!_sessions.TryGetValue(sessionToken, out var session))
                return false;

            if (session.IsExpired)
            {
                await RevokeTokenAsync(sessionToken);
                return false;
            }

            session.UpdateActivity();
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RevokeTokenAsync(string sessionToken)
        {
            if (!_sessions.TryRemove(sessionToken, out var session))
                return false;

            // Remove from client session tracking
            lock (_lock)
            {
                if (_clientSessions.TryGetValue(session.ClientId, out var clientSessions))
                {
                    clientSessions.Remove(sessionToken);
                    if (clientSessions.Count == 0)
                    {
                        _clientSessions.TryRemove(session.ClientId, out _);
                    }
                }
            }

            _logger.LogInformation("Session {SessionToken} revoked for client {ClientId}",
                sessionToken[..8] + "...", session.ClientId);

            return true;
        }

        /// <inheritdoc />
        public async Task<SessionInfo?> GetSessionAsync(string sessionToken)
        {
            if (string.IsNullOrEmpty(sessionToken))
                return null;

            if (!_sessions.TryGetValue(sessionToken, out var session))
                return null;

            if (session.IsExpired)
            {
                await RevokeTokenAsync(sessionToken);
                return null;
            }

            session.UpdateActivity();
            return session;
        }

        /// <inheritdoc />
        public async Task<bool> HasPermissionAsync(string sessionToken, string operation, string? resource = null)
        {
            var session = await GetSessionAsync(sessionToken);
            if (session == null)
                return false;

            // Check specific permission
            if (session.UserInfo.HasPermission(operation))
                return true;

            // Check resource-specific permission
            if (!string.IsNullOrEmpty(resource))
            {
                var resourcePermission = $"{operation}:{resource}";
                if (session.UserInfo.HasPermission(resourcePermission))
                    return true;
            }

            return false;
        }

        /// <inheritdoc />
        public int GetActiveSessionCount()
        {
            return _sessions.Count;
        }

        /// <inheritdoc />
        public async Task<int> CleanupExpiredSessionsAsync()
        {
            var expiredTokens = new List<string>();

            foreach (var kvp in _sessions)
            {
                if (kvp.Value.IsExpired)
                {
                    expiredTokens.Add(kvp.Key);
                }
            }

            var cleanupCount = 0;
            foreach (var token in expiredTokens)
            {
                if (await RevokeTokenAsync(token))
                {
                    cleanupCount++;
                }
            }

            if (cleanupCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired sessions", cleanupCount);
            }

            return cleanupCount;
        }

        private string GenerateSessionToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var tokenBytes = new byte[32];
            rng.GetBytes(tokenBytes);
            return Convert.ToBase64String(tokenBytes);
        }        private UserInfo CreateUserInfo(ApiKeyConfiguration apiKeyConfig, string clientId)
        {
            // Create user based on API key configuration
            var userInfo = new UserInfo
            {
                UserId = apiKeyConfig.UserId,
                Username = apiKeyConfig.DisplayName ?? $"user_{apiKeyConfig.UserId}",
                Role = apiKeyConfig.Role
            };            // Use custom permissions if provided, otherwise use role-based permissions
            if (apiKeyConfig.CustomPermissions != null && apiKeyConfig.CustomPermissions.Count > 0)
            {
                foreach (var permission in apiKeyConfig.CustomPermissions)
                {
                    userInfo.Permissions.Add(permission);
                }
            }
            else
            {
                // Set permissions based on role
                switch (userInfo.Role.ToLowerInvariant())
                {
                    case "admin":
                        userInfo.Permissions.Add("*"); // Admin has all permissions
                        break;
                    case "power_user":
                        userInfo.Permissions.Add("tcp_connect");
                        userInfo.Permissions.Add("tcp_send");
                        userInfo.Permissions.Add("file_read");
                        userInfo.Permissions.Add("file_write");
                        userInfo.Permissions.Add("file_list");
                        break;
                    case "user":
                    default:
                        userInfo.Permissions.Add("tcp_connect");
                        userInfo.Permissions.Add("tcp_send");
                        userInfo.Permissions.Add("file_read");
                        userInfo.Permissions.Add("file_list");
                        break;
                }
            }

            return userInfo;
        }        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _cleanupTimer?.Dispose();
                _sessions.Clear();
                _clientSessions.Clear();
            }

            _disposed = true;
        }
    }
}
