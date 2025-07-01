using System;
using System.Threading.Tasks;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Security
{
    /// <summary>
    /// Implementation of the IUserService interface that provides user authentication and management.
    /// This service bridges the gap between the authentication system and the user management system.
    /// </summary>
    public class UserService : IUserService
    {
        private readonly User.IUserManager _userManager;
        private readonly ILogger<UserService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserService"/> class.
        /// </summary>
        /// <param name="userManager">The user manager service.</param>
        /// <param name="logger">The logger.</param>
        public UserService(User.IUserManager userManager, ILogger<UserService> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Validates a user's credentials.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>The user if validation succeeds, or null if validation fails.</returns>
        public async Task<User?> ValidateUserAsync(string username, string password)
        {
            try
            {
                _logger.LogInformation("Validating credentials for user: {Username}", username);
                
                // Use the user manager to authenticate the user
                var user = await _userManager.AuthenticateAsync(username, password);
                
                if (user != null)
                {
                    _logger.LogInformation("User {Username} authenticated successfully", username);
                    return user;
                }
                
                _logger.LogWarning("Authentication failed for user: {Username}", username);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user credentials for {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// Updates a user's information.
        /// </summary>
        /// <param name="user">The user to update.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UpdateUserAsync(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));
                
            try
            {
                _logger.LogInformation("Updating user information for {Username}", user.Username);
                
                // Use the user manager to update the user
                await _userManager.UpdateUserAsync(user);
                
                _logger.LogInformation("User {Username} updated successfully", user.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {Username}", user?.Username);
                throw;
            }
        }
    }
}
