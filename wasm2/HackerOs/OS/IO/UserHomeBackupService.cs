using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.IO
{
    /// <summary>
    /// Service for managing user home directory backups
    /// </summary>
    public class UserHomeBackupService
    {
        private const string BACKUP_ROOT = "/var/backups/home";
        private const string BACKUP_INDEX_FILE = "/var/backups/home/backup-index.json";
        private const int MAX_BACKUPS_PER_USER = 5;
        
        private readonly IVirtualFileSystem _fileSystem;
        private readonly IUserManager _userManager;
        private readonly ILogger<UserHomeBackupService> _logger;
        private Dictionary<string, List<BackupMetadata>> _backupIndex;
        
        /// <summary>
        /// Metadata for a user home directory backup
        /// </summary>
        public class BackupMetadata
        {
            /// <summary>
            /// Gets or sets the backup ID
            /// </summary>
            public string BackupId { get; set; }
            
            /// <summary>
            /// Gets or sets the username
            /// </summary>
            public string Username { get; set; }
            
            /// <summary>
            /// Gets or sets the timestamp of the backup
            /// </summary>
            public DateTime Timestamp { get; set; }
            
            /// <summary>
            /// Gets or sets the size of the backup in bytes
            /// </summary>
            public long Size { get; set; }
            
            /// <summary>
            /// Gets or sets the reason for the backup
            /// </summary>
            public string Reason { get; set; }
            
            /// <summary>
            /// Gets or sets a value indicating whether this is an automated backup
            /// </summary>
            public bool IsAutomated { get; set; }
            
            /// <summary>
            /// Gets or sets the path to the backup directory
            /// </summary>
            public string BackupPath { get; set; }
            
            /// <summary>
            /// Gets or sets a list of directories included in the backup
            /// </summary>
            public List<string> IncludedDirectories { get; set; }
        }
        
        /// <summary>
        /// Initializes a new instance of the UserHomeBackupService
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="userManager">The user manager</param>
        /// <param name="logger">The logger</param>
        public UserHomeBackupService(
            IVirtualFileSystem fileSystem,
            IUserManager userManager,
            ILogger<UserHomeBackupService> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _backupIndex = new Dictionary<string, List<BackupMetadata>>();
        }
        
        /// <summary>
        /// Initializes the backup service
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing UserHomeBackupService");
                
                // Ensure backup directory exists
                if (!await _fileSystem.DirectoryExistsAsync(BACKUP_ROOT))
                {
                    await _fileSystem.CreateDirectoryAsync(BACKUP_ROOT);
                    _logger.LogInformation("Created backup root directory: {BackupRoot}", BACKUP_ROOT);
                }
                
                // Load backup index if it exists
                if (await _fileSystem.FileExistsAsync(BACKUP_INDEX_FILE))
                {
                    string indexJson = await _fileSystem.ReadFileAsync(BACKUP_INDEX_FILE);
                    _backupIndex = JsonSerializer.Deserialize<Dictionary<string, List<BackupMetadata>>>(indexJson)
                        ?? new Dictionary<string, List<BackupMetadata>>();
                    _logger.LogInformation("Loaded backup index with {Count} user entries", _backupIndex.Count);
                }
                
                _logger.LogInformation("UserHomeBackupService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing UserHomeBackupService");
                _backupIndex = new Dictionary<string, List<BackupMetadata>>();
            }
        }
        
        /// <summary>
        /// Saves the backup index to disk
        /// </summary>
        private async Task SaveBackupIndexAsync()
        {
            try
            {
                string indexJson = JsonSerializer.Serialize(_backupIndex, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await _fileSystem.WriteFileAsync(BACKUP_INDEX_FILE, indexJson);
                _logger.LogDebug("Saved backup index with {Count} user entries", _backupIndex.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving backup index");
            }
        }
        
        /// <summary>
        /// Creates a backup of a user's home directory
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="reason">Optional reason for the backup</param>
        /// <param name="isAutomated">Whether this is an automated backup</param>
        /// <param name="specificDirectories">Optional specific directories to include, null for all standard dirs</param>
        /// <returns>The backup metadata if successful, null otherwise</returns>
        public async Task<BackupMetadata> CreateBackupAsync(
            string username,
            string reason = "Manual backup",
            bool isAutomated = false,
            List<string> specificDirectories = null)
        {
            try
            {
                _logger.LogInformation("Creating backup for user {Username}, reason: {Reason}", username, reason);
                
                // Ensure user exists
                var user = await _userManager.GetUserAsync(username);
                if (user == null)
                {
                    _logger.LogWarning("Cannot create backup for non-existent user {Username}", username);
                    return null;
                }
                
                // Create unique backup ID
                string backupId = $"{username}_{DateTime.Now:yyyyMMdd_HHmmss}";
                string backupPath = $"{BACKUP_ROOT}/{backupId}";
                
                // Create backup directory
                if (!await _fileSystem.CreateDirectoryAsync(backupPath))
                {
                    _logger.LogError("Failed to create backup directory: {BackupPath}", backupPath);
                    return null;
                }
                
                // Define directories to backup (either specified or default)
                List<string> directoriesToBackup = specificDirectories ?? new List<string>
                {
                    "Documents",
                    "Downloads",
                    "Pictures",
                    "Music",
                    "Videos",
                    ".ssh",
                    ".config",
                    ".local",
                    "Desktop"
                };
                
                string homePath = $"/home/{username}";
                long totalSize = 0;
                
                // Copy each directory
                foreach (var dir in directoriesToBackup)
                {
                    string sourcePath = $"{homePath}/{dir}";
                    string targetPath = $"{backupPath}/{dir}";
                    
                    if (await _fileSystem.DirectoryExistsAsync(sourcePath))
                    {
                        // Create target directory
                        await _fileSystem.CreateDirectoryAsync(targetPath);
                        
                        // Copy directory recursively
                        bool success = await CopyDirectoryRecursivelyAsync(sourcePath, targetPath);
                        if (success)
                        {
                            // Calculate size
                            long dirSize = await CalculateDirectorySizeAsync(targetPath);
                            totalSize += dirSize;
                            _logger.LogDebug("Backed up {Dir} for user {Username}, size: {Size} bytes", dir, username, dirSize);
                        }
                    }
                }
                
                // Create backup metadata
                var metadata = new BackupMetadata
                {
                    BackupId = backupId,
                    Username = username,
                    Timestamp = DateTime.Now,
                    Size = totalSize,
                    Reason = reason,
                    IsAutomated = isAutomated,
                    BackupPath = backupPath,
                    IncludedDirectories = directoriesToBackup
                };
                
                // Add to index
                if (!_backupIndex.ContainsKey(username))
                {
                    _backupIndex[username] = new List<BackupMetadata>();
                }
                
                _backupIndex[username].Add(metadata);
                
                // Enforce backup limit per user
                if (_backupIndex[username].Count > MAX_BACKUPS_PER_USER)
                {
                    // Sort by timestamp and remove oldest
                    _backupIndex[username] = _backupIndex[username]
                        .OrderByDescending(b => b.Timestamp)
                        .Take(MAX_BACKUPS_PER_USER)
                        .ToList();
                    
                    // Remove old backups from disk
                    var backupsToKeep = _backupIndex[username].Select(b => b.BackupId).ToHashSet();
                    var allBackupDirs = await _fileSystem.GetDirectoriesAsync(BACKUP_ROOT);
                    
                    foreach (var dir in allBackupDirs)
                    {
                        if (dir.StartsWith(username) && !backupsToKeep.Contains(dir))
                        {
                            await _fileSystem.DeleteDirectoryAsync($"{BACKUP_ROOT}/{dir}", true);
                            _logger.LogInformation("Removed old backup: {BackupId}", dir);
                        }
                    }
                }
                
                // Save index
                await SaveBackupIndexAsync();
                
                _logger.LogInformation("Backup completed for user {Username}, ID: {BackupId}, size: {Size} bytes", 
                    username, backupId, totalSize);
                
                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating backup for user {Username}", username);
                return null;
            }
        }
        
        /// <summary>
        /// Restores a user's home directory from a backup
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="backupId">The backup ID to restore from, or null for most recent</param>
        /// <param name="specificDirectories">Optional specific directories to restore, null for all backed up dirs</param>
        /// <param name="createBackupFirst">Whether to create a backup before restoring</param>
        /// <returns>True if successful</returns>
        public async Task<bool> RestoreFromBackupAsync(
            string username,
            string backupId = null,
            List<string> specificDirectories = null,
            bool createBackupFirst = true)
        {
            try
            {
                _logger.LogInformation("Restoring backup for user {Username}, backupId: {BackupId}", 
                    username, backupId ?? "most recent");
                
                // Ensure user exists
                var user = await _userManager.GetUserAsync(username);
                if (user == null)
                {
                    _logger.LogWarning("Cannot restore backup for non-existent user {Username}", username);
                    return false;
                }
                
                // Get backups for user
                if (!_backupIndex.TryGetValue(username, out var backups) || backups.Count == 0)
                {
                    _logger.LogWarning("No backups found for user {Username}", username);
                    return false;
                }
                
                // Find the backup to restore
                BackupMetadata backupToRestore;
                if (backupId != null)
                {
                    backupToRestore = backups.FirstOrDefault(b => b.BackupId == backupId);
                    if (backupToRestore == null)
                    {
                        _logger.LogWarning("Backup ID {BackupId} not found for user {Username}", backupId, username);
                        return false;
                    }
                }
                else
                {
                    // Use most recent
                    backupToRestore = backups.OrderByDescending(b => b.Timestamp).First();
                }
                
                // Verify backup directory exists
                if (!await _fileSystem.DirectoryExistsAsync(backupToRestore.BackupPath))
                {
                    _logger.LogWarning("Backup directory {BackupPath} not found", backupToRestore.BackupPath);
                    return false;
                }
                
                // Create a backup first if requested
                if (createBackupFirst)
                {
                    await CreateBackupAsync(username, "Auto-backup before restore", true);
                }
                
                string homePath = $"/home/{username}";
                
                // Determine directories to restore
                List<string> directoriesToRestore = specificDirectories ?? backupToRestore.IncludedDirectories;
                
                // Restore each directory
                foreach (var dir in directoriesToRestore)
                {
                    string sourcePath = $"{backupToRestore.BackupPath}/{dir}";
                    string targetPath = $"{homePath}/{dir}";
                    
                    if (await _fileSystem.DirectoryExistsAsync(sourcePath))
                    {
                        // Create target directory if it doesn't exist
                        if (!await _fileSystem.DirectoryExistsAsync(targetPath))
                        {
                            await _fileSystem.CreateDirectoryAsync(targetPath);
                        }
                        
                        // Copy directory recursively
                        bool success = await CopyDirectoryRecursivelyAsync(sourcePath, targetPath);
                        if (success)
                        {
                            _logger.LogDebug("Restored {Dir} for user {Username} from backup {BackupId}", 
                                dir, username, backupToRestore.BackupId);
                        }
                    }
                }
                
                _logger.LogInformation("Restore completed for user {Username} from backup {BackupId}", 
                    username, backupToRestore.BackupId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring backup for user {Username}", username);
                return false;
            }
        }
        
        /// <summary>
        /// Lists all backups for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <returns>A list of backup metadata</returns>
        public async Task<List<BackupMetadata>> ListBackupsAsync(string username)
        {
            try
            {
                // Ensure user exists
                var user = await _userManager.GetUserAsync(username);
                if (user == null)
                {
                    _logger.LogWarning("Cannot list backups for non-existent user {Username}", username);
                    return new List<BackupMetadata>();
                }
                
                // Get backups for user
                if (!_backupIndex.TryGetValue(username, out var backups))
                {
                    return new List<BackupMetadata>();
                }
                
                return backups.OrderByDescending(b => b.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing backups for user {Username}", username);
                return new List<BackupMetadata>();
            }
        }
        
        /// <summary>
        /// Deletes a backup
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="backupId">The backup ID</param>
        /// <returns>True if successful</returns>
        public async Task<bool> DeleteBackupAsync(string username, string backupId)
        {
            try
            {
                _logger.LogInformation("Deleting backup {BackupId} for user {Username}", backupId, username);
                
                // Check if user has backups
                if (!_backupIndex.TryGetValue(username, out var backups))
                {
                    _logger.LogWarning("No backups found for user {Username}", username);
                    return false;
                }
                
                // Find the backup
                var backup = backups.FirstOrDefault(b => b.BackupId == backupId);
                if (backup == null)
                {
                    _logger.LogWarning("Backup ID {BackupId} not found for user {Username}", backupId, username);
                    return false;
                }
                
                // Delete backup directory
                if (await _fileSystem.DirectoryExistsAsync(backup.BackupPath))
                {
                    bool deleted = await _fileSystem.DeleteDirectoryAsync(backup.BackupPath, true);
                    if (!deleted)
                    {
                        _logger.LogWarning("Failed to delete backup directory: {BackupPath}", backup.BackupPath);
                        return false;
                    }
                }
                
                // Remove from index
                backups.Remove(backup);
                await SaveBackupIndexAsync();
                
                _logger.LogInformation("Deleted backup {BackupId} for user {Username}", backupId, username);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting backup {BackupId} for user {Username}", backupId, username);
                return false;
            }
        }
        
        /// <summary>
        /// Copies a directory recursively
        /// </summary>
        /// <param name="sourcePath">The source directory path</param>
        /// <param name="targetPath">The target directory path</param>
        /// <returns>True if successful</returns>
        private async Task<bool> CopyDirectoryRecursivelyAsync(string sourcePath, string targetPath)
        {
            try
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
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying directory from {SourcePath} to {TargetPath}", sourcePath, targetPath);
                return false;
            }
        }
        
        /// <summary>
        /// Calculates the size of a directory recursively
        /// </summary>
        /// <param name="path">The directory path</param>
        /// <returns>The size in bytes</returns>
        private async Task<long> CalculateDirectorySizeAsync(string path)
        {
            long size = 0;
            
            // Get file sizes
            var files = await _fileSystem.GetFilesAsync(path);
            foreach (var file in files)
            {
                var fileInfo = await _fileSystem.GetFileInfoAsync($"{path}/{file}");
                size += fileInfo?.Size ?? 0;
            }
            
            // Recursively get subdirectory sizes
            var directories = await _fileSystem.GetDirectoriesAsync(path);
            foreach (var dir in directories)
            {
                size += await CalculateDirectorySizeAsync($"{path}/{dir}");
            }
            
            return size;
        }
        
        /// <summary>
        /// Schedules a recurring backup for a user
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="intervalHours">The interval in hours</param>
        /// <returns>True if successful</returns>
        public async Task<bool> ScheduleRecurringBackupAsync(string username, int intervalHours)
        {
            // In a real implementation, this would integrate with a task scheduler
            // For now, we'll just log that it would be scheduled
            _logger.LogInformation("Would schedule recurring backup for user {Username} every {Hours} hours", 
                username, intervalHours);
            
            // Create an initial backup
            await CreateBackupAsync(username, $"Initial scheduled backup (every {intervalHours} hours)", true);
            
            return true;
        }
    }
}
