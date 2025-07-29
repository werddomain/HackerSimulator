namespace HackerOs.OS.Security
{
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
        public User.User? User { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionChangedEventArgs"/> class.
        /// </summary>
        /// <param name="sessionId">The ID of the session that changed.</param>
        /// <param name="state">The new state of the session.</param>
        /// <param name="user">The user associated with the session.</param>
        public SessionChangedEventArgs(string sessionId, SessionState state, User.User? user = null)
        {
            SessionId = sessionId;
            State = state;
            User = user;
        }
    }
}
