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
        private readonly IUserManager _userManager;
        private readonly ILogger<UserService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserService"/> class.
        /// </summary>
        /// <param name="userManager">The user manager service.</param>
        /// <param name="logger">The logger.</param>
        public UserService(IUserManager userManager, ILogger<UserService> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<User.User?> AuthenticateAsync(string username, string password)
        {
            try
            {
                _logger.LogInformation("Authenticating user: {Username}", username);
                var user = await _userManager.AuthenticateAsync(username, password);
                
                if (user == null)
                {
                    _logger.LogWarning("Authentication failed for user: {Username}", username);
                    return null;
                }

                _logger.LogInformation("User authenticated successfully: {Username}", username);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authenticating user: {Username}", username);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<User.User?> GetUserAsync(string username)
        {
            try
            {
                _logger.LogInformation("Getting user: {Username}", username);
                return await _userManager.GetUserAsync(username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user: {Username}", username);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UpdateUserAsync(User.User user)
        {
            try
            {
                _logger.LogInformation("Updating user: {Username}", user.Username);
                await _userManager.UpdateUserAsync(user);
                _logger.LogInformation("User updated successfully: {Username}", user.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {Username}", user.Username);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<User.User> CreateUserAsync(string username, string fullName, string password, bool isAdmin = false)
        {
            try
            {
                _logger.LogInformation("Creating user: {Username}", username);
                var user = await _userManager.CreateUserAsync(username, fullName, password, isAdmin);
                _logger.LogInformation("User created successfully: {Username}", username);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user: {Username}", username);
                throw;
            }
        }
    }
}
