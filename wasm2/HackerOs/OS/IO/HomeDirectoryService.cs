using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.IO
{
    /// <summary>
    /// Service that provides home directory management capabilities
    /// </summary>
    public class HomeDirectoryService
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger<HomeDirectoryService> _logger;
        private readonly HomeDirectoryManager _directoryManager;
        private readonly UserQuotaManager _quotaManager;
        private readonly UmaskManager _umaskManager;
        private readonly HomeDirectoryTemplateManager _templateManager;
        private readonly UserHomeBackupService _backupService;
        
        /// <summary>
        /// Initializes a new instance of the HomeDirectoryService
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="logger">The logger</param>
        public HomeDirectoryService(IVirtualFileSystem fileSystem, ILogger<HomeDirectoryService> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Create user manager factory
            var userManager = UserManagerFactory.CreateStandard(fileSystem, logger);
            
            // Initialize managers
            _directoryManager = new HomeDirectoryManager(fileSystem, userManager, logger);
            _quotaManager = new UserQuotaManager(fileSystem, logger);
            _umaskManager = new UmaskManager(fileSystem, logger);
            _templateManager = new HomeDirectoryTemplateManager(fileSystem);
            _backupService = new UserHomeBackupService(fileSystem, userManager, logger);
        }
        
        /// <summary>
        /// Initializes the service by loading necessary data
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing HomeDirectoryService");
                
                // Initialize managers
                await _quotaManager.InitializeAsync();
                await _umaskManager.InitializeAsync();
                await _backupService.InitializeAsync();
                
                _logger.LogInformation("HomeDirectoryService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing HomeDirectoryService");
            }
        }
        
        /// <summary>
        /// Gets the home directory manager
        /// </summary>
        public HomeDirectoryManager DirectoryManager => _directoryManager;
        
        /// <summary>
        /// Gets the quota manager
        /// </summary>
        public UserQuotaManager QuotaManager => _quotaManager;
        
        /// <summary>
        /// Gets the umask manager
        /// </summary>
        public UmaskManager UmaskManager => _umaskManager;
        
        /// <summary>
        /// Gets the template manager
        /// </summary>
        public HomeDirectoryTemplateManager TemplateManager => _templateManager;
        
        /// <summary>
        /// Gets the backup service
        /// </summary>
        public UserHomeBackupService BackupService => _backupService;
        
        /// <summary>
        /// Creates a home directory for a new user
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="templateName">Optional template name</param>
        /// <returns>True if successful</returns>
        public async Task<bool> CreateHomeDirectoryAsync(User.User user, string templateName = null)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            
            try
            {
                _logger.LogInformation("Creating home directory for user {Username}", user.Username);
                
                // Create home directory
                bool success = await _directoryManager.CreateHomeDirectoryAsync(user, templateName);
                
                if (success)
                {
                    // Set default quota
                    await _quotaManager.SetQuotaAsync(user.Username, 1073741824, 2147483648); // 1GB soft, 2GB hard
                    
                    // Update usage
                    await _quotaManager.UpdateUsageAsync(user.Username);
                    
                    // Set default umask
                    await _umaskManager.SetUmaskAsync(user.Username, _umaskManager.GetDefaultUmask());
                    
                    // Create initial backup
                    await _backupService.CreateBackupAsync(user.Username, "Initial home directory setup", true);
                    
                    _logger.LogInformation("Home directory created successfully for user {Username}", user.Username);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating home directory for user {Username}", user.Username);
                return false;
            }
        }
        
        /// <summary>
        /// Resets a user's home directory to template defaults
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="preserveUserData">Whether to preserve user data</param>
        /// <param name="templateName">Optional template name</param>
        /// <returns>True if successful</returns>
        public async Task<bool> ResetHomeDirectoryAsync(string username, bool preserveUserData = true, string templateName = null)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                _logger.LogInformation("Resetting home directory for user {Username}", username);
                
                // Get user
                var userManager = UserManagerFactory.CreateStandard(_fileSystem, _logger);
                var user = await userManager.GetUserAsync(username);
                
                if (user == null)
                {
                    _logger.LogWarning("User {Username} not found", username);
                    return false;
                }
                
                // Create backup if preserving data
                if (preserveUserData)
                {
                    await _backupService.CreateBackupAsync(username, "Pre-reset backup", true);
                }
                
                // Reset home directory
                bool success = await _directoryManager.ResetHomeDirectoryAsync(user, preserveUserData, templateName);
                
                if (success)
                {
                    // Update quota usage
                    await _quotaManager.UpdateUsageAsync(username);
                    
                    // Restore user data if requested
                    if (preserveUserData)
                    {
                        await _backupService.RestoreFromBackupAsync(username, null, null, false);
                    }
                    
                    _logger.LogInformation("Home directory reset successfully for user {Username}", username);
                }
                
                return success;
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
        /// <param name="newTemplateName">The new template name</param>
        /// <param name="preserveUserData">Whether to preserve user data</param>
        /// <returns>True if successful</returns>
        public async Task<bool> MigrateUserToTemplateAsync(string username, string newTemplateName, bool preserveUserData = true)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            if (string.IsNullOrEmpty(newTemplateName))
            {
                throw new ArgumentException("Template name cannot be empty", nameof(newTemplateName));
            }
            
            try
            {
                _logger.LogInformation("Migrating user {Username} to template {TemplateName}", username, newTemplateName);
                
                // Get user
                var userManager = UserManagerFactory.CreateStandard(_fileSystem, _logger);
                var user = await userManager.GetUserAsync(username);
                
                if (user == null)
                {
                    _logger.LogWarning("User {Username} not found", username);
                    return false;
                }
                
                // Create backup if preserving data
                if (preserveUserData)
                {
                    await _backupService.CreateBackupAsync(username, $"Pre-migration to {newTemplateName} template", true);
                }
                
                // Migrate to new template
                bool success = await _directoryManager.MigrateToTemplateAsync(user, newTemplateName, preserveUserData);
                
                if (success)
                {
                    // Update quota usage
                    await _quotaManager.UpdateUsageAsync(username);
                    
                    _logger.LogInformation("User {Username} migrated successfully to template {TemplateName}", username, newTemplateName);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating user {Username} to template {TemplateName}", username, newTemplateName);
                return false;
            }
        }
        
        /// <summary>
        /// Calculates and returns the home directory size for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>The directory size in bytes, or -1 if calculation failed</returns>
        public async Task<long> GetHomeDirectorySizeAsync(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                _logger.LogDebug("Getting home directory size for user {Username}", username);
                
                return await _directoryManager.CalculateHomeDirectorySizeAsync(username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting home directory size for user {Username}", username);
                return -1;
            }
        }
        
        /// <summary>
        /// Sets a quota for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="softLimit">The soft limit in bytes</param>
        /// <param name="hardLimit">The hard limit in bytes</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SetUserQuotaAsync(string username, long softLimit, long hardLimit)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                _logger.LogInformation("Setting quota for user {Username}: soft={SoftLimit}, hard={HardLimit}", 
                    username, softLimit, hardLimit);
                
                return await _quotaManager.SetQuotaAsync(username, softLimit, hardLimit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting quota for user {Username}", username);
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a user is over their quota
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="newBytes">Additional bytes to consider</param>
        /// <returns>The quota status</returns>
        public async Task<QuotaStatus> CheckUserQuotaAsync(string username, long newBytes = 0)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                return await _quotaManager.CheckQuotaAsync(username, newBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking quota for user {Username}", username);
                return QuotaStatus.Error;
            }
        }
        
        /// <summary>
        /// Sets the umask for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="umask">The umask value</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SetUserUmaskAsync(string username, int umask)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                _logger.LogInformation("Setting umask for user {Username} to {Umask:000}", username, umask);
                
                return await _umaskManager.SetUmaskAsync(username, umask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting umask for user {Username}", username);
                return false;
            }
        }
        
        /// <summary>
        /// Applies standard permission presets to a user's home directory
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>True if successful</returns>
        public async Task<bool> ApplyStandardPermissionsAsync(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                _logger.LogInformation("Applying standard permissions to home directory for user {Username}", username);
                
                // Get user
                var userManager = UserManagerFactory.CreateStandard(_fileSystem, _logger);
                var user = await userManager.GetUserAsync(username);
                
                if (user == null)
                {
                    _logger.LogWarning("User {Username} not found", username);
                    return false;
                }
                
                // Apply standard permissions
                string homePath = $"/home/{username}";
                bool success = await DirectoryPermissionPresets.ApplyStandardPermissionsAsync(
                    _fileSystem, 
                    homePath, 
                    user.UserId, 
                    user.PrimaryGroupId);
                
                if (success)
                {
                    _logger.LogInformation("Standard permissions applied successfully to home directory for user {Username}", username);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying standard permissions to home directory for user {Username}", username);
                return false;
            }
        }
        
        /// <summary>
        /// Creates a backup of a user's home directory
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="reason">Optional reason for the backup</param>
        /// <param name="specificDirectories">Optional list of specific directories to back up</param>
        /// <returns>True if successful</returns>
        public async Task<bool> CreateUserBackupAsync(string username, string reason = "Manual backup", List<string> specificDirectories = null)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                var result = await _backupService.CreateBackupAsync(username, reason, false, specificDirectories);
                
                if (result != null)
                {
                    _logger.LogInformation("Created backup for user {Username}, ID: {BackupId}, size: {Size} bytes", 
                        username, result.BackupId, result.Size);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating backup for user {Username}", username);
                return false;
            }
        }
        
        /// <summary>
        /// Restores a user's home directory from a backup
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="backupId">Optional specific backup ID to restore from</param>
        /// <param name="createBackupFirst">Whether to create a backup before restoring</param>
        /// <param name="specificDirectories">Optional specific directories to restore</param>
        /// <returns>True if successful</returns>
        public async Task<bool> RestoreUserBackupAsync(
            string username, 
            string backupId = null, 
            bool createBackupFirst = true,
            List<string> specificDirectories = null)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                bool success = await _backupService.RestoreFromBackupAsync(
                    username, 
                    backupId, 
                    specificDirectories, 
                    createBackupFirst);
                
                if (success)
                {
                    _logger.LogInformation("Restored backup for user {Username}, backupId: {BackupId}", 
                        username, backupId ?? "most recent");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring backup for user {Username}", username);
                return false;
            }
        }
        
        /// <summary>
        /// Lists available backups for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>List of backup metadata</returns>
        public async Task<List<UserHomeBackupService.BackupMetadata>> ListUserBackupsAsync(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            try
            {
                return await _backupService.ListBackupsAsync(username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing backups for user {Username}", username);
                return new List<UserHomeBackupService.BackupMetadata>();
            }
        }
        
        /// <summary>
        /// Schedules recurring backups for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="intervalHours">The interval in hours</param>
        /// <returns>True if successful</returns>
        public async Task<bool> ScheduleRecurringBackupsAsync(string username, int intervalHours)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }
            
            if (intervalHours <= 0)
            {
                throw new ArgumentException("Interval must be greater than zero", nameof(intervalHours));
            }
            
            try
            {
                return await _backupService.ScheduleRecurringBackupAsync(username, intervalHours);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling recurring backups for user {Username}", username);
                return false;
            }
        }
    }
}
