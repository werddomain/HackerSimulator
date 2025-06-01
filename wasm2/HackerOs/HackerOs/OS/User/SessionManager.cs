using System.Text.Json;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.User
{
    /// <summary>
    /// Represents an active user session in the system
    /// </summary>
    public class UserSession
    {
        /// <summary>
        /// Unique session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// User associated with this session
        /// </summary>
        public User User { get; set; } = new();

        /// <summary>
        /// Session token for authentication
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// When the session was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the session was last active
        /// </summary>
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the session expires
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Whether the session is currently active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Session timeout in minutes
        /// </summary>
        public int TimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Whether this session is locked (requires password to resume)
        /// </summary>
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// Current working directory for this session
        /// </summary>
        public string CurrentWorkingDirectory { get; set; } = "/";

        /// <summary>
        /// Environment variables for this session
        /// </summary>
        public Dictionary<string, string> Environment { get; set; } = new();

        /// <summary>
        /// Session-specific data
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();

        public UserSession()
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(TimeoutMinutes);
        }

        public UserSession(User user, string token, int timeoutMinutes = 30) : this()
        {
            User = user;
            Token = token;
            TimeoutMinutes = timeoutMinutes;
            SessionId = GenerateSessionId();
            CurrentWorkingDirectory = user.HomeDirectory;
            Environment = new Dictionary<string, string>(user.Environment);
            RefreshExpiration();
        }

        /// <summary>
        /// Updates the last activity time and extends the session
        /// </summary>
        public void RefreshActivity()
        {
            LastActivity = DateTime.UtcNow;
            RefreshExpiration();
            IsLocked = false; // Unlock session on activity
        }

        /// <summary>
        /// Extends the session expiration time
        /// </summary>
        public void RefreshExpiration()
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(TimeoutMinutes);
        }

        /// <summary>
        /// Checks if the session has expired
        /// </summary>
        /// <returns>True if the session has expired</returns>
        public bool HasExpired()
        {
            return DateTime.UtcNow > ExpiresAt;
        }

        /// <summary>
        /// Checks if the session should be locked due to inactivity
        /// </summary>
        /// <param name="lockTimeoutMinutes">Minutes of inactivity before locking</param>
        /// <returns>True if the session should be locked</returns>
        public bool ShouldBeLocked(int lockTimeoutMinutes = 10)
        {
            return DateTime.UtcNow > LastActivity.AddMinutes(lockTimeoutMinutes);
        }

        /// <summary>
        /// Locks the session
        /// </summary>
        public void Lock()
        {
            IsLocked = true;
        }

        /// <summary>
        /// Unlocks the session with password verification
        /// </summary>
        /// <param name="password">User password</param>
        /// <returns>True if successfully unlocked</returns>
        public bool Unlock(string password)
        {
            if (User.VerifyPassword(password))
            {
                IsLocked = false;
                RefreshActivity();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Terminates the session
        /// </summary>
        public void Terminate()
        {
            IsActive = false;
            ExpiresAt = DateTime.UtcNow; // Force immediate expiration
        }

        /// <summary>
        /// Gets a session data value
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="key">Data key</param>
        /// <param name="defaultValue">Default value if key not found</param>
        /// <returns>The data value or default</returns>
        public T? GetData<T>(string key, T? defaultValue = default)
        {
            if (Data.TryGetValue(key, out var value))
            {
                if (value is T directValue)
                    return directValue;

                try
                {
                    if (value is JsonElement element)
                        return element.Deserialize<T>();
                    
                    return (T?)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Sets a session data value
        /// </summary>
        /// <param name="key">Data key</param>
        /// <param name="value">Data value</param>
        public void SetData(string key, object? value)
        {
            if (value == null)
            {
                Data.Remove(key);
            }
            else
            {
                Data[key] = value;
            }
        }

        /// <summary>
        /// Creates a serializable representation of the session
        /// </summary>
        /// <returns>Dictionary representation</returns>
        public Dictionary<string, object> ToSerializable()
        {
            return new Dictionary<string, object>
            {
                ["sessionId"] = SessionId,
                ["user"] = User.ToSerializable(),
                ["token"] = Token,
                ["createdAt"] = CreatedAt.ToString("O"),
                ["lastActivity"] = LastActivity.ToString("O"),
                ["expiresAt"] = ExpiresAt.ToString("O"),
                ["isActive"] = IsActive,
                ["timeoutMinutes"] = TimeoutMinutes,
                ["isLocked"] = IsLocked,
                ["currentWorkingDirectory"] = CurrentWorkingDirectory,
                ["environment"] = Environment,
                ["data"] = Data
            };
        }

        /// <summary>
        /// Creates a UserSession from a serializable representation
        /// </summary>
        /// <param name="data">Dictionary representation</param>
        /// <returns>UserSession instance</returns>
        public static UserSession FromSerializable(Dictionary<string, object> data)
        {
            var session = new UserSession();

            if (data.ContainsKey("sessionId"))
                session.SessionId = data["sessionId"].ToString() ?? "";

            if (data.ContainsKey("user") && data["user"] is Dictionary<string, object> userData)
                session.User = User.FromSerializable(userData);

            if (data.ContainsKey("token"))
                session.Token = data["token"].ToString() ?? "";

            if (data.ContainsKey("createdAt") && DateTime.TryParse(data["createdAt"].ToString(), out var createdAt))
                session.CreatedAt = createdAt;

            if (data.ContainsKey("lastActivity") && DateTime.TryParse(data["lastActivity"].ToString(), out var lastActivity))
                session.LastActivity = lastActivity;

            if (data.ContainsKey("expiresAt") && DateTime.TryParse(data["expiresAt"].ToString(), out var expiresAt))
                session.ExpiresAt = expiresAt;

            if (data.ContainsKey("isActive"))
                session.IsActive = Convert.ToBoolean(data["isActive"]);

            if (data.ContainsKey("timeoutMinutes"))
                session.TimeoutMinutes = Convert.ToInt32(data["timeoutMinutes"]);

            if (data.ContainsKey("isLocked"))
                session.IsLocked = Convert.ToBoolean(data["isLocked"]);

            if (data.ContainsKey("currentWorkingDirectory"))
                session.CurrentWorkingDirectory = data["currentWorkingDirectory"].ToString() ?? "/";

            if (data.ContainsKey("environment") && data["environment"] is Dictionary<string, object> env)
                session.Environment = env.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString() ?? "");

            if (data.ContainsKey("data") && data["data"] is Dictionary<string, object> sessionData)
                session.Data = sessionData;

            return session;
        }

        private static string GenerateSessionId()
        {
            return Guid.NewGuid().ToString("N")[..16]; // 16 character session ID
        }

        public override string ToString()
        {
            return $"Session {SessionId} - {User.Username} ({(IsActive ? "Active" : "Inactive")}, {(IsLocked ? "Locked" : "Unlocked")})";
        }
    }

    /// <summary>
    /// Manages user sessions with token-based authentication and session persistence
    /// </summary>
    public interface ISessionManager
    {
        /// <summary>
        /// Creates a new user session
        /// </summary>
        Task<UserSession> CreateSessionAsync(User user, int timeoutMinutes = 30);

        /// <summary>
        /// Validates a session token
        /// </summary>
        Task<UserSession?> ValidateSessionAsync(string token);

        /// <summary>
        /// Gets the current active session
        /// </summary>
        Task<UserSession?> GetCurrentSessionAsync();

        /// <summary>
        /// Sets the current active session
        /// </summary>
        Task SetCurrentSessionAsync(UserSession? session);

        /// <summary>
        /// Refreshes a session's activity and expiration
        /// </summary>
        Task<bool> RefreshSessionAsync(string token);

        /// <summary>
        /// Locks a session
        /// </summary>
        Task<bool> LockSessionAsync(string token);

        /// <summary>
        /// Unlocks a session with password verification
        /// </summary>
        Task<bool> UnlockSessionAsync(string token, string password);

        /// <summary>
        /// Terminates a session
        /// </summary>
        Task<bool> TerminateSessionAsync(string token);

        /// <summary>
        /// Gets all active sessions
        /// </summary>
        Task<IEnumerable<UserSession>> GetActiveSessionsAsync();

        /// <summary>
        /// Cleans up expired sessions
        /// </summary>
        Task CleanupExpiredSessionsAsync();

        /// <summary>
        /// Switches to a different user session
        /// </summary>
        Task<bool> SwitchSessionAsync(string token);
    }

    /// <summary>
    /// Implementation of session management with LocalStorage persistence
    /// </summary>
    public class SessionManager : ISessionManager
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<SessionManager> _logger;
        private readonly Dictionary<string, UserSession> _sessions;
        private readonly object _lock = new();
        private UserSession? _currentSession;

        private const string CurrentSessionKey = "hackeros_current_session";
        private const string SessionsKey = "hackeros_sessions";

        public SessionManager(IJSRuntime jsRuntime, ILogger<SessionManager> logger)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessions = new Dictionary<string, UserSession>();

            // Start cleanup timer
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5)); // Cleanup every 5 minutes
                    await CleanupExpiredSessionsAsync();
                }
            });
        }

        public async Task<UserSession> CreateSessionAsync(User user, int timeoutMinutes = 30)
        {
            var token = GenerateSecureToken();
            var session = new UserSession(user, token, timeoutMinutes);

            lock (_lock)
            {
                _sessions[token] = session;
            }

            await SaveSessionsToStorageAsync();
            
            _logger.LogInformation("Created session {SessionId} for user {Username}", session.SessionId, user.Username);
            return session;
        }

        public async Task<UserSession?> ValidateSessionAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            lock (_lock)
            {
                if (_sessions.TryGetValue(token, out var session))
                {
                    if (session.IsActive && !session.HasExpired())
                    {
                        // Check if session should be locked due to inactivity
                        if (!session.IsLocked && session.ShouldBeLocked())
                        {
                            session.Lock();
                            _logger.LogInformation("Session {SessionId} locked due to inactivity", session.SessionId);
                        }

                        return session;
                    }
                    else if (session.HasExpired())
                    {
                        session.Terminate();
                        _logger.LogInformation("Session {SessionId} expired", session.SessionId);
                    }
                }
            }

            return null;
        }

        public async Task<UserSession?> GetCurrentSessionAsync()
        {
            if (_currentSession != null)
                return _currentSession;

            try
            {
                var sessionData = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", CurrentSessionKey);
                if (!string.IsNullOrEmpty(sessionData))
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(sessionData);
                    if (data != null)
                    {
                        _currentSession = UserSession.FromSerializable(data);
                        
                        // Validate the session
                        var validatedSession = await ValidateSessionAsync(_currentSession.Token);
                        if (validatedSession == null)
                        {
                            _currentSession = null;
                            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", CurrentSessionKey);
                        }
                        else
                        {
                            _currentSession = validatedSession;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load current session from storage");
                _currentSession = null;
            }

            return _currentSession;
        }

        public async Task SetCurrentSessionAsync(UserSession? session)
        {
            _currentSession = session;

            try
            {
                if (session != null)
                {
                    var sessionData = JsonSerializer.Serialize(session.ToSerializable());
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", CurrentSessionKey, sessionData);
                    _logger.LogDebug("Set current session to {SessionId}", session.SessionId);
                }
                else
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", CurrentSessionKey);
                    _logger.LogDebug("Cleared current session");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save current session to storage");
            }
        }

        public async Task<bool> RefreshSessionAsync(string token)
        {
            var session = await ValidateSessionAsync(token);
            if (session != null && !session.IsLocked)
            {
                session.RefreshActivity();
                await SaveSessionsToStorageAsync();
                _logger.LogDebug("Refreshed session {SessionId}", session.SessionId);
                return true;
            }
            return false;
        }

        public async Task<bool> LockSessionAsync(string token)
        {
            var session = await ValidateSessionAsync(token);
            if (session != null)
            {
                session.Lock();
                await SaveSessionsToStorageAsync();
                _logger.LogInformation("Locked session {SessionId}", session.SessionId);
                return true;
            }
            return false;
        }

        public async Task<bool> UnlockSessionAsync(string token, string password)
        {
            var session = await ValidateSessionAsync(token);
            if (session != null && session.IsLocked)
            {
                if (session.Unlock(password))
                {
                    await SaveSessionsToStorageAsync();
                    _logger.LogInformation("Unlocked session {SessionId}", session.SessionId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to unlock session {SessionId} - invalid password", session.SessionId);
                }
            }
            return false;
        }

        public async Task<bool> TerminateSessionAsync(string token)
        {
            lock (_lock)
            {
                if (_sessions.TryGetValue(token, out var session))
                {
                    session.Terminate();
                    _sessions.Remove(token);
                    
                    if (_currentSession?.Token == token)
                    {
                        _currentSession = null;
                    }

                    _logger.LogInformation("Terminated session {SessionId}", session.SessionId);
                    _ = Task.Run(SaveSessionsToStorageAsync);
                    _ = Task.Run(() => SetCurrentSessionAsync(null));
                    return true;
                }
            }

            return false;
        }

        public async Task<IEnumerable<UserSession>> GetActiveSessionsAsync()
        {
            await LoadSessionsFromStorageAsync();

            lock (_lock)
            {
                return _sessions.Values
                    .Where(s => s.IsActive && !s.HasExpired())
                    .ToList();
            }
        }

        public async Task CleanupExpiredSessionsAsync()
        {
            var expiredTokens = new List<string>();

            lock (_lock)
            {
                foreach (var kvp in _sessions)
                {
                    if (!kvp.Value.IsActive || kvp.Value.HasExpired())
                    {
                        expiredTokens.Add(kvp.Key);
                    }
                }

                foreach (var token in expiredTokens)
                {
                    _sessions.Remove(token);
                }
            }

            if (expiredTokens.Count > 0)
            {
                await SaveSessionsToStorageAsync();
                _logger.LogInformation("Cleaned up {Count} expired sessions", expiredTokens.Count);
            }
        }

        public async Task<bool> SwitchSessionAsync(string token)
        {
            var session = await ValidateSessionAsync(token);
            if (session != null && !session.IsLocked)
            {
                await SetCurrentSessionAsync(session);
                _logger.LogInformation("Switched to session {SessionId} for user {Username}", session.SessionId, session.User.Username);
                return true;
            }
            return false;
        }

        private async Task LoadSessionsFromStorageAsync()
        {
            try
            {
                var sessionsData = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", SessionsKey);
                if (!string.IsNullOrEmpty(sessionsData))
                {
                    var sessionsList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(sessionsData);
                    if (sessionsList != null)
                    {
                        lock (_lock)
                        {
                            _sessions.Clear();
                            foreach (var sessionData in sessionsList)
                            {
                                var session = UserSession.FromSerializable(sessionData);
                                if (session.IsActive && !session.HasExpired())
                                {
                                    _sessions[session.Token] = session;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load sessions from storage");
            }
        }

        private async Task SaveSessionsToStorageAsync()
        {
            try
            {
                List<Dictionary<string, object>> sessionsList;
                
                lock (_lock)
                {
                    sessionsList = _sessions.Values
                        .Where(s => s.IsActive && !s.HasExpired())
                        .Select(s => s.ToSerializable())
                        .ToList();
                }

                var sessionsData = JsonSerializer.Serialize(sessionsList);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", SessionsKey, sessionsData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save sessions to storage");
            }
        }

        private static string GenerateSecureToken()
        {
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }
    }
}
