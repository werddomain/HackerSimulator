using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.IO;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.User
{
    /// <summary>
    /// Integration class for the UserManager and HomeDirectoryTemplateManager
    /// </summary>
    public class EnhancedUserManager : IUserManager
    {
        private readonly IUserManager _baseUserManager;
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger<EnhancedUserManager> _logger;
        private readonly HomeDirectoryService _homeDirectoryService;

        /// <summary>
        /// Creates a new instance of EnhancedUserManager
        /// </summary>
        /// <param name="baseUserManager">The base user manager to wrap</param>
        /// <param name="fileSystem">The file system to use</param>
        /// <param name="logger">The logger to use</param>
        public EnhancedUserManager(IUserManager baseUserManager, IVirtualFileSystem fileSystem, ILogger<EnhancedUserManager> logger)
        {
            _baseUserManager = baseUserManager ?? throw new ArgumentNullException(nameof(baseUserManager));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize the home directory service
            _homeDirectoryService = new HomeDirectoryService(fileSystem, logger);
            InitializeAsync().Wait(); // Initialize synchronously in constructor
        }
        
        /// <summary>
        /// Initializes the enhanced user manager
        /// </summary>
        private async Task InitializeAsync()
        {
            try
            {
                await _homeDirectoryService.InitializeAsync();
                _logger.LogInformation("EnhancedUserManager initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing EnhancedUserManager");
            }
        }

        /// <summary>
        /// Gets the home directory service
        /// </summary>
        public HomeDirectoryService HomeDirectoryService => _homeDirectoryService;

        /// <summary>
        /// Authenticates a user with username and password
        /// </summary>
        public Task<User?> AuthenticateAsync(string username, string password)
        {
            return _baseUserManager.AuthenticateAsync(username, password);
        }

        /// <summary>
        /// Creates a new user account with enhanced home directory setup
        /// </summary>
        public async Task<User> CreateUserAsync(string username, string fullName, string password, bool isAdmin = false)
        {
            // Create the user with the base implementation
            User user = await _baseUserManager.CreateUserAsync(username, fullName, password, isAdmin);
            
            try
            {
                // Use the HomeDirectoryService to create the home directory
                _logger.LogInformation("Creating home directory for new user {Username}", username);
                
                // Determine template based on user type
                string templateName = isAdmin ? "admin" : "standard";
                
                // Create home directory with template
                bool success = await _homeDirectoryService.CreateHomeDirectoryAsync(user, templateName);
                
                if (!success)
                {
                    _logger.LogWarning("Failed to create home directory for user {Username}", username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating home directory for user {Username}", username);
                // We don't throw here because the user has already been created
            }
            
            return user;
        }

        /// <summary>
        /// Resets a user's home directory to the default template
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="preserveData">Whether to preserve user data in certain directories</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> ResetUserHomeDirectoryAsync(string username, bool preserveData = true)
        {
            try
            {
                // Use HomeDirectoryService for reset
                return await _homeDirectoryService.ResetHomeDirectoryAsync(username, preserveData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting home directory for user {Username}", username);
                return false;
            }
        }

        /// <summary>
        /// Migrates a user to a different home directory template
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="templateName">The template to migrate to</param>
        /// <param name="preserveData">Whether to preserve user data</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> MigrateUserTemplateAsync(string username, string templateName, bool preserveData = true)
        {
            try
            {
                // Use HomeDirectoryService for migration
                return await _homeDirectoryService.MigrateUserToTemplateAsync(username, templateName, preserveData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating user {Username} to template {TemplateName}", username, templateName);
                return false;
            }
        }

        /// <summary>
        /// Sets quota for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="softLimit">The soft quota limit in bytes</param>
        /// <param name="hardLimit">The hard quota limit in bytes</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> SetUserQuotaAsync(string username, long softLimit, long hardLimit)
        {
            try
            {
                return await _homeDirectoryService.SetUserQuotaAsync(username, softLimit, hardLimit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting quota for user {Username}", username);
                return false;
            }
        }

        /// <summary>
        /// Gets a user by username
        /// </summary>
        public Task<User?> GetUserAsync(string username)
        {
            return _baseUserManager.GetUserAsync(username);
        }

        /// <summary>
        /// Gets all users
        /// </summary>
        public Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return _baseUserManager.GetAllUsersAsync();
        }

        /// <summary>
        /// Updates a user
        /// </summary>
        public async Task<bool> UpdateUserAsync(User user)
        {
            // Update the user with the base implementation
            return await _baseUserManager.UpdateUserAsync(user);
        }

        /// <summary>
        /// Deletes a user account
        /// </summary>
        public async Task<bool> DeleteUserAsync(string username)
        {
            // First, backup the user's home directory
            try
            {
                string backupPath = $"/var/backups/home/{username}_{DateTime.Now:yyyyMMdd_HHmmss}";
                await _fileSystem.CreateDirectoryAsync(backupPath, true);
                
                string homePath = $"/home/{username}";
                if (await _fileSystem.DirectoryExistsAsync(homePath))
                {
                    await _fileSystem.CopyDirectoryAsync(homePath, backupPath);
                    _logger.LogInformation("Backed up home directory for user {Username} to {BackupPath}", username, backupPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to backup home directory for user {Username} before deletion", username);
                // Continue with deletion even if backup fails
            }
            
            // Delete the user with the base implementation
            bool deleted = await _baseUserManager.DeleteUserAsync(username);
            
            if (deleted)
            {
                // Remove the home directory
                try
                {
                    string homePath = $"/home/{username}";
                    if (await _fileSystem.DirectoryExistsAsync(homePath))
                    {
                        await _fileSystem.DeleteDirectoryAsync(homePath, true);
                        _logger.LogInformation("Deleted home directory for user {Username}", username);
                    }
                    
                    // Remove quota and umask settings
                    await _homeDirectoryService.QuotaManager.RemoveQuotaAsync(username);
                    await _homeDirectoryService.UmaskManager.RemoveUmaskAsync(username);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up resources for deleted user {Username}", username);
                    // User is already deleted, so we consider this operation successful
                }
            }
            
            return deleted;
        }
    }
}
