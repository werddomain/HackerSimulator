using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.IO
{
    /// <summary>
    /// Service for managing user home directories
    /// </summary>
    public class HomeDirectoryManager
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly IUserManager _userManager;
        private readonly HomeDirectoryTemplateManager _templateManager;
        private readonly ILogger<HomeDirectoryManager> _logger;

        /// <summary>
        /// Initializes a new instance of the HomeDirectoryManager
        /// </summary>
        /// <param name="fileSystem">The file system to use</param>
        /// <param name="userManager">The user manager</param>
        /// <param name="logger">The logger</param>
        public HomeDirectoryManager(
            IVirtualFileSystem fileSystem,
            IUserManager userManager,
            ILogger<HomeDirectoryManager> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize the template manager
            _templateManager = new HomeDirectoryTemplateManager(fileSystem);
        }

        /// <summary>
        /// Creates or resets a home directory for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="reset">Whether to reset an existing directory</param>
        /// <param name="templateName">Optional specific template to use</param>
        /// <param name="preserveData">Whether to preserve user data when resetting</param>
        /// <returns>True if successful</returns>
        public async Task<bool> CreateOrResetHomeDirectoryAsync(
            string username,
            bool reset = false,
            string templateName = null,
            bool preserveData = true)
        {
            try
            {
                // Get the user
                User.User? user = await _userManager.GetUserAsync(username);
                if (user == null)
                {
                    _logger.LogWarning("Cannot create home directory for non-existent user {Username}", username);
                    return false;
                }

                string homePath = $"/home/{username}";
                bool homeExists = await _fileSystem.DirectoryExistsAsync(homePath);

                // Check if we need to reset
                if (homeExists && reset)
                {
                    if (preserveData)
                    {
                        await BackupUserDataAsync(username);
                    }

                    // Delete existing home directory
                    bool deleted = await _fileSystem.DeleteDirectoryAsync(homePath, true);
                    if (!deleted)
                    {
                        _logger.LogWarning("Failed to delete existing home directory for user {Username}", username);
                        return false;
                    }

                    homeExists = false;
                }
                else if (homeExists && !reset)
                {
                    _logger.LogInformation("Home directory already exists for user {Username} and reset not requested", username);
                    return true;
                }

                // Create home directory from template
                bool success = await HomeDirectoryApplicator.ApplyHomeDirectoryTemplateAsync(
                    _fileSystem,
                    _templateManager,
                    user,
                    templateName);

                // Restore data if we did a reset
                if (success && reset && preserveData)
                {
                    await RestoreUserDataAsync(username);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating/resetting home directory for user {Username}", username);
                return false;
            }
        }

        /// <summary>
        /// Calculates the disk usage of a user's home directory
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>The size in bytes</returns>
        public async Task<long> CalculateHomeDirectorySizeAsync(string username)
        {
            try
            {
                string homePath = $"/home/{username}";
                if (!await _fileSystem.DirectoryExistsAsync(homePath))
                {
                    return 0;
                }

                return await CalculateDirectorySizeAsync(homePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating home directory size for user {Username}", username);
                return -1;
            }
        }

        /// <summary>
        /// Recursively calculates the size of a directory
        /// </summary>
        /// <param name="path">The directory path</param>
        /// <returns>The size in bytes</returns>
        private async Task<long> CalculateDirectorySizeAsync(string path)
        {
            long size = 0;

            // Get all files in this directory
            var files = await _fileSystem.GetFilesAsync(path);
            foreach (var file in files)
            {
                var fileInfo = await _fileSystem.GetFileInfoAsync($"{path}/{file}");
                size += fileInfo?.Size ?? 0;
            }

            // Recursively get sizes of subdirectories
            var directories = await _fileSystem.GetDirectoriesAsync(path);
            foreach (var dir in directories)
            {
                size += await CalculateDirectorySizeAsync($"{path}/{dir}");
            }

            return size;
        }

        /// <summary>
        /// Migrates a user's home directory structure to a new template
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="templateName">The template to migrate to</param>
        /// <param name="preserveData">Whether to preserve user data</param>
        /// <returns>True if successful</returns>
        public async Task<bool> MigrateToTemplateAsync(
            string username,
            string templateName,
            bool preserveData = true)
        {
            try
            {
                // Get the user
                User.User? user = await _userManager.GetUserAsync(username);
                if (user == null)
                {
                    _logger.LogWarning("Cannot migrate home directory for non-existent user {Username}", username);
                    return false;
                }

                // Get the template
                var template = _templateManager.GetTemplate(templateName);
                if (template == null)
                {
                    _logger.LogWarning("Template {TemplateName} not found for migration", templateName);
                    return false;
                }

                // Backup user data if requested
                if (preserveData)
                {
                    await BackupUserDataAsync(username);
                }

                // Reset the home directory with the new template
                bool success = await CreateOrResetHomeDirectoryAsync(
                    username,
                    reset: true,
                    templateName: templateName,
                    preserveData: false);

                // Restore user data if requested
                if (success && preserveData)
                {
                    await RestoreUserDataAsync(username);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating home directory for user {Username} to template {TemplateName}", username, templateName);
                return false;
            }
        }

        /// <summary>
        /// Backs up important user data to a permanent backup location
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="reason">Optional reason for the backup</param>
        /// <returns>True if successful</returns>
        public async Task<bool> BackupUserDataAsync(string username, string reason = "Manual backup")
        {
            try
            {
                _logger.LogInformation("Creating permanent backup for user {Username}, reason: {Reason}", username, reason);
                
                // Create backup directory structure
                string permanentBackupRoot = "/var/backups/home";
                if (!await _fileSystem.DirectoryExistsAsync(permanentBackupRoot))
                {
                    await _fileSystem.CreateDirectoryAsync(permanentBackupRoot);
                }
                
                string userBackupDir = $"{permanentBackupRoot}/{username}";
                if (!await _fileSystem.DirectoryExistsAsync(userBackupDir))
                {
                    await _fileSystem.CreateDirectoryAsync(userBackupDir);
                }
                
                string backupId = $"{DateTime.Now:yyyyMMdd_HHmmss}";
                string backupPath = $"{userBackupDir}/{backupId}";
                await _fileSystem.CreateDirectoryAsync(backupPath);
                
                string homePath = $"/home/{username}";
                
                // Directories to backup (expanded from previous version)
                string[] directoriesToBackup = new[]
                {
                    "Documents",
                    "Downloads",
                    "Pictures",
                    "Music",
                    "Videos",
                    "Desktop",
                    ".ssh",
                    ".config",
                    ".local/share",
                    ".bash_history"
                };
                
                // Copy each directory
                foreach (var dir in directoriesToBackup)
                {
                    string sourcePath = $"{homePath}/{dir}";
                    string targetPath = $"{backupPath}/{dir}";
                    
                    if (await _fileSystem.DirectoryExistsAsync(sourcePath))
                    {
                        // Create target directory
                        await _fileSystem.CreateDirectoryAsync(Path.GetDirectoryName(targetPath));
                        
                        // Copy directory recursively
                        if (dir.Contains("/"))
                        {
                            // For paths with subdirectories, ensure parent dirs exist
                            string[] parts = dir.Split('/');
                            string currentPath = backupPath;
                            for (int i = 0; i < parts.Length - 1; i++)
                            {
                                currentPath = $"{currentPath}/{parts[i]}";
                                if (!await _fileSystem.DirectoryExistsAsync(currentPath))
                                {
                                    await _fileSystem.CreateDirectoryAsync(currentPath);
                                }
                            }
                        }
                        
                        // Copy recursively
                        await CopyDirectoryRecursivelyAsync(sourcePath, targetPath);
                    }
                }
                
                // Create metadata file
                var metadata = new
                {
                    Username = username,
                    Timestamp = DateTime.Now,
                    Reason = reason,
                    Directories = directoriesToBackup
                };
                
                string metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await _fileSystem.WriteFileAsync($"{backupPath}/backup-metadata.json", metadataJson);
                
                _logger.LogInformation("Created permanent backup for user {Username} at {BackupPath}", username, backupPath);
                
                // Cleanup old backups (keep only 5 most recent)
                await CleanupOldBackupsAsync(username);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating permanent backup for user {Username}", username);
                return false;
            }
        }

        /// <summary>
        /// Restores user data from a permanent backup
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="backupId">Optional specific backup ID to restore, null for most recent</param>
        /// <returns>True if successful</returns>
        public async Task<bool> RestoreUserDataAsync(string username, string backupId = null)
        {
            try
            {
                string permanentBackupRoot = "/var/backups/home";
                string userBackupDir = $"{permanentBackupRoot}/{username}";
                
                if (!await _fileSystem.DirectoryExistsAsync(userBackupDir))
                {
                    _logger.LogWarning("No backups found for user {Username}", username);
                    return false;
                }
                
                // Find the backup to restore
                string backupPath;
                if (backupId != null)
                {
                    backupPath = $"{userBackupDir}/{backupId}";
                    if (!await _fileSystem.DirectoryExistsAsync(backupPath))
                    {
                        _logger.LogWarning("Backup ID {BackupId} not found for user {Username}", backupId, username);
                        return false;
                    }
                }
                else
                {
                    // Find the most recent backup
                    var backupDirs = await _fileSystem.GetDirectoriesAsync(userBackupDir);
                    if (backupDirs.Count == 0)
                    {
                        _logger.LogWarning("No backups found for user {Username}", username);
                        return false;
                    }
                    
                    // Sort by name (which includes timestamp) to get the most recent
                    var sortedBackups = new List<string>(backupDirs);
                    sortedBackups.Sort((a, b) => string.Compare(b, a)); // Descending order
                    backupPath = $"{userBackupDir}/{sortedBackups[0]}";
                }
                
                _logger.LogInformation("Restoring backup for user {Username} from {BackupPath}", username, backupPath);
                
                string homePath = $"/home/{username}";
                
                // Read metadata to get directories
                string metadataPath = $"{backupPath}/backup-metadata.json";
                string[] directoriesToRestore;
                
                if (await _fileSystem.FileExistsAsync(metadataPath))
                {
                    string metadataJson = await _fileSystem.ReadFileAsync(metadataPath);
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<dynamic>(metadataJson);
                    directoriesToRestore = ((System.Text.Json.JsonElement)metadata.Directories).EnumerateArray()
                        .Select(e => e.GetString())
                        .ToArray();
                }
                else
                {
                    // Fallback to default directories
                    directoriesToRestore = new[]
                    {
                        "Documents",
                        "Downloads",
                        "Pictures",
                        "Music",
                        "Videos",
                        "Desktop",
                        ".ssh",
                        ".config",
                        ".local/share",
                        ".bash_history"
                    };
                }
                
                // Restore each directory
                foreach (var dir in directoriesToRestore)
                {
                    string sourcePath = $"{backupPath}/{dir}";
                    string targetPath = $"{homePath}/{dir}";
                    
                    if (await _fileSystem.DirectoryExistsAsync(sourcePath))
                    {
                        // Ensure target directory exists
                        if (dir.Contains("/"))
                        {
                            // For paths with subdirectories, ensure parent dirs exist
                            string[] parts = dir.Split('/');
                            string currentPath = homePath;
                            for (int i = 0; i < parts.Length - 1; i++)
                            {
                                currentPath = $"{currentPath}/{parts[i]}";
                                if (!await _fileSystem.DirectoryExistsAsync(currentPath))
                                {
                                    await _fileSystem.CreateDirectoryAsync(currentPath);
                                }
                            }
                        }
                        
                        // Restore recursively
                        await CopyDirectoryRecursivelyAsync(sourcePath, targetPath);
                    }
                }
                
                _logger.LogInformation("Restored user data for {Username} from backup", username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring user data for {Username}", username);
                return false;
            }
        }
        
        /// <summary>
        /// Lists available backups for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>List of backup IDs with timestamps</returns>
        public async Task<List<(string BackupId, DateTime Timestamp, string Reason)>> ListBackupsAsync(string username)
        {
            try
            {
                string permanentBackupRoot = "/var/backups/home";
                string userBackupDir = $"{permanentBackupRoot}/{username}";
                
                if (!await _fileSystem.DirectoryExistsAsync(userBackupDir))
                {
                    return new List<(string, DateTime, string)>();
                }
                
                var result = new List<(string BackupId, DateTime Timestamp, string Reason)>();
                var backupDirs = await _fileSystem.GetDirectoriesAsync(userBackupDir);
                
                foreach (var backupId in backupDirs)
                {
                    string metadataPath = $"{userBackupDir}/{backupId}/backup-metadata.json";
                    
                    if (await _fileSystem.FileExistsAsync(metadataPath))
                    {
                        string metadataJson = await _fileSystem.ReadFileAsync(metadataPath);
                        var metadata = System.Text.Json.JsonSerializer.Deserialize<dynamic>(metadataJson);
                        
                        DateTime timestamp = ((System.Text.Json.JsonElement)metadata.Timestamp).GetDateTime();
                        string reason = ((System.Text.Json.JsonElement)metadata.Reason).GetString();
                        
                        result.Add((backupId, timestamp, reason));
                    }
                    else
                    {
                        // For backups without metadata, parse timestamp from directory name
                        if (DateTime.TryParseExact(backupId, "yyyyMMdd_HHmmss", null, 
                            System.Globalization.DateTimeStyles.None, out DateTime timestamp))
                        {
                            result.Add((backupId, timestamp, "Unknown reason"));
                        }
                    }
                }
                
                // Sort by timestamp (newest first)
                return result.OrderByDescending(b => b.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing backups for user {Username}", username);
                return new List<(string, DateTime, string)>();
            }
        }
        
        /// <summary>
        /// Deletes a specific backup
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="backupId">The backup ID to delete</param>
        /// <returns>True if successful</returns>
        public async Task<bool> DeleteBackupAsync(string username, string backupId)
        {
            try
            {
                string permanentBackupRoot = "/var/backups/home";
                string backupPath = $"{permanentBackupRoot}/{username}/{backupId}";
                
                if (!await _fileSystem.DirectoryExistsAsync(backupPath))
                {
                    _logger.LogWarning("Backup ID {BackupId} not found for user {Username}", backupId, username);
                    return false;
                }
                
                bool success = await _fileSystem.DeleteDirectoryAsync(backupPath, true);
                
                if (success)
                {
                    _logger.LogInformation("Deleted backup {BackupId} for user {Username}", backupId, username);
                }
                else
                {
                    _logger.LogWarning("Failed to delete backup {BackupId} for user {Username}", backupId, username);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting backup {BackupId} for user {Username}", backupId, username);
                return false;
            }
        }
        
        /// <summary>
        /// Cleanup old backups, keeping only the 5 most recent
        /// </summary>
        /// <param name="username">The username</param>
        private async Task CleanupOldBackupsAsync(string username)
        {
            try
            {
                string permanentBackupRoot = "/var/backups/home";
                string userBackupDir = $"{permanentBackupRoot}/{username}";
                
                if (!await _fileSystem.DirectoryExistsAsync(userBackupDir))
                {
                    return;
                }
                
                var backupDirs = await _fileSystem.GetDirectoriesAsync(userBackupDir);
                if (backupDirs.Count <= 5)
                {
                    return; // No cleanup needed
                }
                
                // Sort by name (which is the timestamp) in descending order
                var sortedBackups = new List<string>(backupDirs);
                sortedBackups.Sort((a, b) => string.Compare(b, a)); // Newest first
                
                // Keep the 5 most recent backups, delete the rest
                for (int i = 5; i < sortedBackups.Count; i++)
                {
                    string backupPath = $"{userBackupDir}/{sortedBackups[i]}";
                    await _fileSystem.DeleteDirectoryAsync(backupPath, true);
                    _logger.LogInformation("Cleaned up old backup {BackupId} for user {Username}", sortedBackups[i], username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old backups for user {Username}", username);
            }
        }
        
        /// <summary>
        /// Copies a directory recursively
        /// </summary>
        /// <param name="sourcePath">The source directory path</param>
        /// <param name="targetPath">The target directory path</param>
        private async Task CopyDirectoryRecursivelyAsync(string sourcePath, string targetPath)
        {
            // Create target directory if it doesn't exist
            if (!await _fileSystem.DirectoryExistsAsync(targetPath))
            {
                await _fileSystem.CreateDirectoryAsync(targetPath);
            }
            
            // Copy all files
            var files = await _fileSystem.GetFilesAsync(sourcePath);
            foreach (var file in files)
            {
                string sourceFilePath = $"{sourcePath}/{file}";
                string targetFilePath = $"{targetPath}/{file}";
                
                string content = await _fileSystem.ReadFileAsync(sourceFilePath);
                await _fileSystem.WriteFileAsync(targetFilePath, content);
            }
            
            // Recursively copy subdirectories
            var directories = await _fileSystem.GetDirectoriesAsync(sourcePath);
            foreach (var dir in directories)
            {
                string sourceSubDir = $"{sourcePath}/{dir}";
                string targetSubDir = $"{targetPath}/{dir}";
                
                await CopyDirectoryRecursivelyAsync(sourceSubDir, targetSubDir);
            }
        }
    }
}
