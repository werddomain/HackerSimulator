using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.User;

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
        Task<HackerOs.OS.User.Models.UserSession> CreateSessionAsync(User.User user);

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
        Task<HackerOs.OS.User.Models.UserSession?> GetActiveSessionAsync();

        /// <summary>
        /// Gets all active user sessions.
        /// </summary>
        /// <returns>A collection of all active user sessions.</returns>
        Task<IEnumerable<HackerOs.OS.User.Models.UserSession>> GetAllSessionsAsync();

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
}
