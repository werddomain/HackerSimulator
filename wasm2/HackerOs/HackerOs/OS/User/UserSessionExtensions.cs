using System;

namespace HackerOs.OS.User
{
    /// <summary>
    /// Extension methods for UserSession
    /// </summary>
    public static class UserSessionExtensions
    {
        /// <summary>
        /// Gets the User from a UserSession, handling null sessions gracefully
        /// </summary>
        /// <param name="session">The user session</param>
        /// <returns>The User object or null if session is null</returns>
        public static User? GetUser(this UserSession? session)
        {
            return session?.User;
        }

        /// <summary>
        /// Converts a UserSession to a User
        /// </summary>
        /// <param name="session">The user session to convert</param>
        /// <returns>The User object or null if session is null</returns>
        public static User? ToUser(this UserSession? session)
        {
            return session?.User;
        }
        
        /// <summary>
        /// Checks if the session is authenticated
        /// </summary>
        /// <param name="session">The user session</param>
        /// <returns>True if the session is authenticated</returns>
        public static bool IsAuthenticated(this UserSession? session)
        {
            return session?.User != null && !string.IsNullOrEmpty(session.Token);
        }
    }
}
