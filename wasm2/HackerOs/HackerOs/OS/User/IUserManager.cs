using System;
using System.Threading.Tasks;

namespace HackerOs.OS.User
{
    /// <summary>
    /// User event arguments
    /// </summary>
    public class UserEventArgs : EventArgs
    {
        /// <summary>
        /// User associated with the event
        /// </summary>
        public User User { get; set; } = null!;

       
    }
    /// <summary>
    /// Interface for user management operations
    /// </summary>
    public interface IUserManager
    {
        /// <summary>
        /// Authenticates a user with username and password
        /// </summary>
        Task<User?> AuthenticateAsync(string username, string password);

        /// <summary>
        /// Creates a new user account
        /// </summary>
        Task<User> CreateUserAsync(string username, string fullName, string password, bool isAdmin = false);

        /// <summary>
        /// Gets a user by username
        /// </summary>
        Task<User?> GetUserAsync(string username);

        /// <summary>
        /// Gets a user by user ID
        /// </summary>
        Task<User?> GetUserAsync(int userId);

        /// <summary>
        /// Gets all users in the system
        /// </summary>
        Task<IEnumerable<User>> GetAllUsersAsync();

        /// <summary>
        /// Updates an existing user
        /// </summary>
        Task<bool> UpdateUserAsync(User user);

        /// <summary>
        /// Deletes a user account
        /// </summary>
        Task<bool> DeleteUserAsync(string username);

        /// <summary>
        /// Changes a user's password
        /// </summary>
        Task<bool> ChangePasswordAsync(string username, string oldPassword, string newPassword);

        /// <summary>
        /// Creates a new group
        /// </summary>
        Task<Group> CreateGroupAsync(string groupName, string description = "");

        /// <summary>
        /// Gets a group by name
        /// </summary>
        Task<Group?> GetGroupAsync(string groupName);

        /// <summary>
        /// Gets a group by group ID
        /// </summary>
        Task<Group?> GetGroupAsync(int groupId);

        /// <summary>
        /// Gets all groups in the system
        /// </summary>
        Task<IEnumerable<Group>> GetAllGroupsAsync();

        /// <summary>
        /// Updates an existing group
        /// </summary>
        Task<bool> UpdateGroupAsync(Group group);

        /// <summary>
        /// Deletes a group
        /// </summary>
        Task<bool> DeleteGroupAsync(string groupName);

        /// <summary>
        /// Adds a user to a group
        /// </summary>
        Task<bool> AddUserToGroupAsync(string username, string groupName);

        /// <summary>
        /// Removes a user from a group
        /// </summary>
        Task<bool> RemoveUserFromGroupAsync(string username, string groupName);

        /// <summary>
        /// Checks if a user belongs to a specific group
        /// </summary>
        Task<bool> IsUserInGroupAsync(string username, string groupName);

        /// <summary>
        /// Gets all groups a user belongs to
        /// </summary>
        Task<IEnumerable<Group>> GetUserGroupsAsync(string username);

        /// <summary>
        /// Initializes the user management system
        /// </summary>
        Task InitializeAsync();
    }

    ///// <summary>
    ///// Interface for user management in HackerOS
    ///// </summary>
    //public interface IUserManager
    //{
    //    /// <summary>
    //    /// Event raised when a user logs in
    //    /// </summary>
    //    event EventHandler<UserEventArgs>? UserLoggedIn;

    //    /// <summary>
    //    /// Event raised when a user logs out
    //    /// </summary>
    //    event EventHandler<UserEventArgs>? UserLoggedOut;

    //    /// <summary>
    //    /// Get the current user
    //    /// </summary>
    //    /// <returns>Current user or null if no user is logged in</returns>
    //    Task<User?> GetCurrentUserAsync();

    //    /// <summary>
    //    /// Create a session for a user
    //    /// </summary>
    //    /// <param name="user">User to create session for</param>
    //    /// <returns>User session</returns>
    //    Task<UserSession> CreateSessionAsync(User user);

    //    /// <summary>
    //    /// Sign in a user
    //    /// </summary>
    //    /// <param name="username">Username</param>
    //    /// <param name="password">Password</param>
    //    /// <returns>User if sign in successful, null otherwise</returns>
    //    Task<User?> SignInAsync(string username, string password);

    //    /// <summary>
    //    /// Sign out a user
    //    /// </summary>
    //    /// <param name="username">Username to sign out</param>
    //    /// <returns>True if sign out successful</returns>
    //    Task<bool> SignOutAsync(string username);

    //    /// <summary>
    //    /// Create a new user
    //    /// </summary>
    //    /// <param name="username">Username</param>
    //    /// <param name="password">Password</param>
    //    /// <param name="fullName">Full name</param>
    //    /// <returns>Created user if successful, null otherwise</returns>
    //    Task<User?> CreateUserAsync(string username, string password, string fullName);

    //    /// <summary>
    //    /// Delete a user
    //    /// </summary>
    //    /// <param name="username">Username to delete</param>
    //    /// <returns>True if deletion successful</returns>
    //    Task<bool> DeleteUserAsync(string username);

    //    /// <summary>
    //    /// Check if a user exists
    //    /// </summary>
    //    /// <param name="username">Username to check</param>
    //    /// <returns>True if user exists</returns>
    //    Task<bool> UserExistsAsync(string username);

    //    /// <summary>
    //    /// Get a user by username
    //    /// </summary>
    //    /// <param name="username">Username</param>
    //    /// <returns>User if found, null otherwise</returns>
    //    Task<User?> GetUserAsync(string username);

    //    /// <summary>
    //    /// Get all users
    //    /// </summary>
    //    /// <returns>List of all users</returns>
    //    Task<List<User>> GetAllUsersAsync();

    //    /// <summary>
    //    /// Change user password
    //    /// </summary>
    //    /// <param name="username">Username</param>
    //    /// <param name="currentPassword">Current password</param>
    //    /// <param name="newPassword">New password</param>
    //    /// <returns>True if password change successful</returns>
    //    Task<bool> ChangePasswordAsync(string username, string currentPassword, string newPassword);

    //    /// <summary>
    //    /// Update user profile
    //    /// </summary>
    //    /// <param name="username">Username</param>
    //    /// <param name="fullName">New full name</param>
    //    /// <param name="profileData">Profile data</param>
    //    /// <returns>Updated user if successful, null otherwise</returns>
    //    Task<User?> UpdateProfileAsync(string username, string fullName, Dictionary<string, string> profileData);
    //}


}
