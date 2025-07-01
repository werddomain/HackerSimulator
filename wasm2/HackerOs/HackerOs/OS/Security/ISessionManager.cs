using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Security
{
    /// <summary>
    /// Interface for managing user sessions in HackerOS.
    /// Provides functionality for creating, ending, and switching between user sessions.
    /// </summary>
    public interface ISessionManager
    {
        /// <summary>
        /// Creates a new user session.
        /// </summary>
        /// <param name="user">The user for whom to create a session.</param>
        /// <returns>The newly created user session.</returns>
        Task<UserSession> CreateSessionAsync(User user);

        /// <summary>
        /// Ends a user session with the specified ID.
        /// </summary>
        /// <param name="sessionId">The ID of the session to end.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task EndSessionAsync(string sessionId);

        /// <summary>
        /// Gets the currently active user session.
        /// </summary>
        /// <returns>The active user session, or null if no session is active.</returns>
        Task<UserSession?> GetActiveSessionAsync();

        /// <summary>
        /// Gets all active user sessions.
        /// </summary>
        /// <returns>A collection of all active user sessions.</returns>
        Task<IEnumerable<UserSession>> GetAllSessionsAsync();

        /// <summary>
        /// Switches to a different user session.
        /// </summary>
        /// <param name="sessionId">The ID of the session to switch to.</param>
        /// <returns>True if the switch was successful, false otherwise.</returns>
        Task<bool> SwitchSessionAsync(string sessionId);

        /// <summary>
        /// Locks the current session, requiring re-authentication to unlock.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LockSessionAsync();

        /// <summary>
        /// Unlocks a locked session.
        /// </summary>
        /// <param name="password">The password to authenticate with.</param>
        /// <returns>True if the unlock was successful, false otherwise.</returns>
        Task<bool> UnlockSessionAsync(string password);

        /// <summary>
        /// Event triggered when the session state changes.
        /// </summary>
        event EventHandler<SessionChangedEventArgs> SessionChanged;
    }

    /// <summary>
    /// EventArgs for session state changes.
    /// </summary>
    public class SessionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the ID of the session that changed.
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// Gets the new state of the session.
        /// </summary>
        public SessionState State { get; }

        /// <summary>
        /// Gets the user associated with the session.
        /// </summary>
        public User? User { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionChangedEventArgs"/> class.
        /// </summary>
        /// <param name="sessionId">The ID of the session that changed.</param>
        /// <param name="state">The new state of the session.</param>
        /// <param name="user">The user associated with the session.</param>
        public SessionChangedEventArgs(string sessionId, SessionState state, User? user = null)
        {
            SessionId = sessionId;
            State = state;
            User = user;
        }
    }

    /// <summary>
    /// Represents the state of a user session.
    /// </summary>
    public enum SessionState
    {
        /// <summary>
        /// The session is active and in use.
        /// </summary>
        Active,

        /// <summary>
        /// The session is locked and requires re-authentication.
        /// </summary>
        Locked,

        /// <summary>
        /// The session has expired.
        /// </summary>
        Expired,

        /// <summary>
        /// The session has been terminated.
        /// </summary>
        Terminated
    }
}
