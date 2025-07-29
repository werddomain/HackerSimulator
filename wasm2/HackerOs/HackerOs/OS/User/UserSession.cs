using HackerOs.OS.Security;
using System.Text.Json;

namespace HackerOs.OS.User
{
    /// <summary>
    /// Represents an active user session in the system
    /// </summary>
    public class UserSession: IUserSession
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
        /// When the session was started
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the session was last active
        /// </summary>
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the session expires
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the current state of this session.
        /// </summary>
        public SessionState State { get; set; } = SessionState.Active;

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

        /// <summary>
        /// Gets the dictionary of session-specific data.
        /// </summary>
        public Dictionary<string, object> SessionData { get; set; } = new Dictionary<string, object>();

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
        /// Changes the session state to the specified new state.
        /// </summary>
        /// <param name="newState">The new session state.</param>
        public void ChangeState(SessionState newState)
        {
            State = newState;

            // Update additional metadata depending on the state
            if (newState == SessionState.Locked)
            {
                SetData("LockTime", DateTime.Now);
            }
        }
        /// <summary>
        /// Updates the last activity timestamp to the current time.
        /// </summary>
        public void UpdateActivity()
        {
            LastActivity = DateTime.UtcNow;
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
                UpdateActivity();
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
        /// Checks if the session has timed out
        /// </summary>
        /// <param name="timeout">The timeout duration</param>
        /// <returns>True if the session has timed out, false otherwise</returns>
        public bool HasTimedOut(TimeSpan timeout)
        {
            return DateTime.UtcNow - LastActivity > timeout;
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
}
