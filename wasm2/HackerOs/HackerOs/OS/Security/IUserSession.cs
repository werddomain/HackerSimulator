using System;
using System.Collections.Generic;

namespace HackerOs.OS.Security
{
    /// <summary>
    /// Represents a user session in the HackerOS system.
    /// </summary>
    public class UserSession
    {
        /// <summary>
        /// Gets the unique identifier for this session.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets the user associated with this session.
        /// </summary>
        public User User { get; set; } = null!;

        /// <summary>
        /// Gets the authentication token for this session.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Gets the time when this session was started.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets the time of the last activity in this session.
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Gets the current state of this session.
        /// </summary>
        public SessionState State { get; set; }

        /// <summary>
        /// Gets the dictionary of session-specific data.
        /// </summary>
        public Dictionary<string, object> SessionData { get; set; } = new Dictionary<string, object>();

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
    }

    /// <summary>
    /// Interface for persistence of user sessions.
    /// </summary>
    public interface IUserSessionPersistence
    {
        /// <summary>
        /// Saves a user session to persistent storage.
        /// </summary>
        /// <param name="session">The session to save.</param>
        /// <returns>True if the save was successful, false otherwise.</returns>
        bool SaveSession(UserSession session);

        /// <summary>
        /// Loads a user session from persistent storage.
        /// </summary>
        /// <param name="sessionId">The ID of the session to load.</param>
        /// <returns>The loaded session, or null if the session could not be found.</returns>
        UserSession? LoadSession(string sessionId);

        /// <summary>
        /// Loads all active user sessions from persistent storage.
        /// </summary>
        /// <returns>A collection of all active user sessions.</returns>
        IEnumerable<UserSession> LoadAllSessions();

        /// <summary>
        /// Deletes a user session from persistent storage.
        /// </summary>
        /// <param name="sessionId">The ID of the session to delete.</param>
        /// <returns>True if the deletion was successful, false otherwise.</returns>
        bool DeleteSession(string sessionId);
    }
}
