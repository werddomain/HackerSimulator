namespace HackerOs.OS.Security
{
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
