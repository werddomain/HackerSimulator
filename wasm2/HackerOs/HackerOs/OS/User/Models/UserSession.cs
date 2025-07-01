using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using HackerOs.OS.Security;
using Microsoft.JSInterop;

namespace HackerOs.OS.User.Models
{
    /// <summary>
    /// Represents a user session in the HackerOS system, implementing the IUserSession interface.
    /// </summary>
    public class UserSession : IUserSession
    {
        /// <summary>
        /// Gets or sets the unique identifier for this session.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user associated with this session.
        /// </summary>
        [JsonIgnore]
        public Security.User? SecurityUser { get; set; }

        /// <summary>
        /// Gets or sets the detailed user model associated with this session.
        /// </summary>
        public User? User { get; set; }

        /// <summary>
        /// Gets or sets the authentication token for this session.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the refresh token for this session.
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the time when this session was started.
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the time of the last activity in this session.
        /// </summary>
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the time when this session expires.
        /// </summary>
        public DateTime ExpiryTime { get; set; } = DateTime.UtcNow.AddHours(8);

        /// <summary>
        /// Gets or sets the time when this token expires and needs refreshing.
        /// </summary>
        public DateTime TokenExpiryTime { get; set; } = DateTime.UtcNow.AddMinutes(30);

        /// <summary>
        /// Gets or sets the current state of this session.
        /// </summary>
        public SessionState State { get; set; } = SessionState.Active;

        /// <summary>
        /// Gets or sets the client IP address associated with this session.
        /// </summary>
        public string ClientIpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client device information associated with this session.
        /// </summary>
        public string ClientDeviceInfo { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current working directory for this session.
        /// </summary>
        public string CurrentWorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current shell process for this session.
        /// </summary>
        public string CurrentShell { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the environment variables for this session.
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the dictionary of session-specific data.
        /// </summary>
        public Dictionary<string, object> SessionData { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the history of commands executed in this session.
        /// </summary>
        public List<string> CommandHistory { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the list of open application windows in this session.
        /// </summary>
        public List<string> OpenWindows { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the list of running processes in this session.
        /// </summary>
        public List<string> RunningProcesses { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets whether this session is persistent across page reloads.
        /// </summary>
        public bool IsPersistent { get; set; } = true;

        /// <summary>
        /// Updates the last activity timestamp to the current time.
        /// </summary>
        public void UpdateActivity()
        {
            LastActivity = DateTime.UtcNow;
        }

        /// <summary>
        /// Checks if the session has been inactive for longer than the specified timeout.
        /// </summary>
        /// <param name="timeout">The timeout period.</param>
        /// <returns>True if the session has timed out, false otherwise.</returns>
        public bool HasTimedOut(TimeSpan timeout)
        {
            return DateTime.UtcNow - LastActivity > timeout;
        }

        /// <summary>
        /// Checks if the session token has expired and needs refreshing.
        /// </summary>
        /// <returns>True if the token has expired, false otherwise.</returns>
        public bool HasTokenExpired()
        {
            return DateTime.UtcNow >= TokenExpiryTime;
        }

        /// <summary>
        /// Checks if the entire session has expired.
        /// </summary>
        /// <returns>True if the session has expired, false otherwise.</returns>
        public bool HasSessionExpired()
        {
            return DateTime.UtcNow >= ExpiryTime;
        }

        /// <summary>
        /// Changes the session state to the specified new state.
        /// </summary>
        /// <param name="newState">The new session state.</param>
        public void ChangeState(SessionState newState)
        {
            State = newState;
        }

        /// <summary>
        /// Stores a value in the session data.
        /// </summary>
        /// <param name="key">The key to store the data under.</param>
        /// <param name="value">The value to store.</param>
        public void SetData(string key, object value)
        {
            SessionData[key] = value;
        }

        /// <summary>
        /// Retrieves a value from the session data.
        /// </summary>
        /// <typeparam name="T">The type of the value to retrieve.</typeparam>
        /// <param name="key">The key of the value to retrieve.</param>
        /// <param name="defaultValue">The default value to return if the key is not found.</param>
        /// <returns>The value associated with the specified key, or the default value if the key is not found.</returns>
        public T? GetData<T>(string key, T? defaultValue = default)
        {
            if (SessionData.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// Adds a command to the session's command history.
        /// </summary>
        /// <param name="command">The command to add.</param>
        public void AddCommandToHistory(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            // Don't add duplicate consecutive commands
            if (CommandHistory.Count > 0 && CommandHistory[CommandHistory.Count - 1] == command)
            {
                return;
            }

            CommandHistory.Add(command);

            // Limit history size
            const int maxHistorySize = 1000;
            if (CommandHistory.Count > maxHistorySize)
            {
                CommandHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Sets the current working directory for this session.
        /// </summary>
        /// <param name="directory">The directory path.</param>
        public void SetCurrentWorkingDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Directory cannot be empty or whitespace.", nameof(directory));
            }

            CurrentWorkingDirectory = directory;
        }

        /// <summary>
        /// Sets an environment variable for this session.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <param name="value">The value of the environment variable.</param>
        public void SetEnvironmentVariable(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Environment variable name cannot be empty or whitespace.", nameof(name));
            }

            EnvironmentVariables[name] = value;
        }

        /// <summary>
        /// Gets the value of an environment variable.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <param name="defaultValue">The default value to return if the environment variable is not set.</param>
        /// <returns>The value of the environment variable, or the default value if it is not set.</returns>
        public string GetEnvironmentVariable(string name, string defaultValue = "")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Environment variable name cannot be empty or whitespace.", nameof(name));
            }

            return EnvironmentVariables.TryGetValue(name, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Registers an open window in this session.
        /// </summary>
        /// <param name="windowId">The ID of the window.</param>
        public void RegisterOpenWindow(string windowId)
        {
            if (string.IsNullOrWhiteSpace(windowId))
            {
                throw new ArgumentException("Window ID cannot be empty or whitespace.", nameof(windowId));
            }

            if (!OpenWindows.Contains(windowId))
            {
                OpenWindows.Add(windowId);
            }
        }

        /// <summary>
        /// Unregisters an open window from this session.
        /// </summary>
        /// <param name="windowId">The ID of the window.</param>
        public void UnregisterOpenWindow(string windowId)
        {
            if (string.IsNullOrWhiteSpace(windowId))
            {
                throw new ArgumentException("Window ID cannot be empty or whitespace.", nameof(windowId));
            }

            OpenWindows.Remove(windowId);
        }

        /// <summary>
        /// Registers a running process in this session.
        /// </summary>
        /// <param name="processId">The ID of the process.</param>
        public void RegisterRunningProcess(string processId)
        {
            if (string.IsNullOrWhiteSpace(processId))
            {
                throw new ArgumentException("Process ID cannot be empty or whitespace.", nameof(processId));
            }

            if (!RunningProcesses.Contains(processId))
            {
                RunningProcesses.Add(processId);
            }
        }

        /// <summary>
        /// Unregisters a running process from this session.
        /// </summary>
        /// <param name="processId">The ID of the process.</param>
        public void UnregisterRunningProcess(string processId)
        {
            if (string.IsNullOrWhiteSpace(processId))
            {
                throw new ArgumentException("Process ID cannot be empty or whitespace.", nameof(processId));
            }

            RunningProcesses.Remove(processId);
        }
    }

    /// <summary>
    /// Provides persistence functionality for user sessions using browser local storage.
    /// </summary>
    public class UserSessionLocalStoragePersistence : IUserSessionPersistence
    {
        private readonly IJSRuntime _jsRuntime;
        private const string SessionKeyPrefix = "hackeros_session_";

        /// <summary>
        /// Initializes a new instance of the <see cref="UserSessionLocalStoragePersistence"/> class.
        /// </summary>
        /// <param name="jsRuntime">The JavaScript runtime for interacting with browser APIs.</param>
        public UserSessionLocalStoragePersistence(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }

        /// <summary>
        /// Saves a user session to local storage.
        /// </summary>
        /// <param name="session">The session to save.</param>
        /// <returns>True if the save was successful, false otherwise.</returns>
        public bool SaveSession(UserSession session)
        {
            try
            {
                var json = JsonSerializer.Serialize(session, new JsonSerializerOptions 
                { 
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                
                _ = _jsRuntime.InvokeVoidAsync("localStorage.setItem", 
                    SessionKeyPrefix + session.SessionId, json);
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Loads a user session from local storage.
        /// </summary>
        /// <param name="sessionId">The ID of the session to load.</param>
        /// <returns>The loaded session, or null if the session could not be found.</returns>
        public UserSession? LoadSession(string sessionId)
        {
            try
            {
                var json = _jsRuntime.InvokeAsync<string>(
                    "localStorage.getItem", SessionKeyPrefix + sessionId).Result;
                
                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }
                
                return JsonSerializer.Deserialize<UserSession>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Loads all active user sessions from local storage.
        /// </summary>
        /// <returns>A collection of all active user sessions.</returns>
        public IEnumerable<UserSession> LoadAllSessions()
        {
            try
            {
                var sessions = new List<UserSession>();
                var keys = _jsRuntime.InvokeAsync<string[]>(
                    "eval", "Object.keys(localStorage).filter(k => k.startsWith('" + SessionKeyPrefix + "'))").Result;
                
                foreach (var key in keys)
                {
                    var json = _jsRuntime.InvokeAsync<string>("localStorage.getItem", key).Result;
                    if (!string.IsNullOrEmpty(json))
                    {
                        var session = JsonSerializer.Deserialize<UserSession>(json);
                        if (session != null)
                        {
                            sessions.Add(session);
                        }
                    }
                }
                
                return sessions;
            }
            catch (Exception)
            {
                return Array.Empty<UserSession>();
            }
        }

        /// <summary>
        /// Deletes a user session from local storage.
        /// </summary>
        /// <param name="sessionId">The ID of the session to delete.</param>
        /// <returns>True if the deletion was successful, false otherwise.</returns>
        public bool DeleteSession(string sessionId)
        {
            try
            {
                _ = _jsRuntime.InvokeVoidAsync("localStorage.removeItem", 
                    SessionKeyPrefix + sessionId);
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Asynchronously saves a user session to local storage.
        /// </summary>
        /// <param name="session">The session to save.</param>
        /// <returns>A task representing the asynchronous operation, with a value indicating whether the save was successful.</returns>
        public async Task<bool> SaveSessionAsync(UserSession session)
        {
            try
            {
                var json = JsonSerializer.Serialize(session, new JsonSerializerOptions 
                { 
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", 
                    SessionKeyPrefix + session.SessionId, json);
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Asynchronously loads a user session from local storage.
        /// </summary>
        /// <param name="sessionId">The ID of the session to load.</param>
        /// <returns>A task representing the asynchronous operation, with a value containing the loaded session, or null if the session could not be found.</returns>
        public async Task<UserSession?> LoadSessionAsync(string sessionId)
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>(
                    "localStorage.getItem", SessionKeyPrefix + sessionId);
                
                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }
                
                return JsonSerializer.Deserialize<UserSession>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Asynchronously loads all active user sessions from local storage.
        /// </summary>
        /// <returns>A task representing the asynchronous operation, with a value containing a collection of all active user sessions.</returns>
        public async Task<IEnumerable<UserSession>> LoadAllSessionsAsync()
        {
            try
            {
                var sessions = new List<UserSession>();
                var keys = await _jsRuntime.InvokeAsync<string[]>(
                    "eval", "Object.keys(localStorage).filter(k => k.startsWith('" + SessionKeyPrefix + "'))");
                
                foreach (var key in keys)
                {
                    var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var session = JsonSerializer.Deserialize<UserSession>(json);
                        if (session != null)
                        {
                            sessions.Add(session);
                        }
                    }
                }
                
                return sessions;
            }
            catch (Exception)
            {
                return Array.Empty<UserSession>();
            }
        }

        /// <summary>
        /// Asynchronously deletes a user session from local storage.
        /// </summary>
        /// <param name="sessionId">The ID of the session to delete.</param>
        /// <returns>A task representing the asynchronous operation, with a value indicating whether the deletion was successful.</returns>
        public async Task<bool> DeleteSessionAsync(string sessionId)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", 
                    SessionKeyPrefix + sessionId);
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
