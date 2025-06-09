using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HackerOs.OS.IO.FileSystem;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Provides configuration backup and restore functionality for HackerOS settings
    /// </summary>
    public class ConfigurationBackupService
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger<ConfigurationBackupService> _logger;
        private readonly string _backupDirectory;
        private readonly int _maxBackupCount;

        public ConfigurationBackupService(IVirtualFileSystem fileSystem, ILogger<ConfigurationBackupService> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _backupDirectory = "/var/backups/config";
            _maxBackupCount = 10; // Keep last 10 backups
        }

        /// <summary>
        /// Creates a backup of the specified configuration file
        /// </summary>
        /// <param name="configFilePath">Path to the configuration file to backup</param>
        /// <returns>The path to the created backup file</returns>
        public async Task<string> CreateBackupAsync(string configFilePath)
        {
            if (string.IsNullOrEmpty(configFilePath))
                throw new ArgumentException("Configuration file path cannot be null or empty", nameof(configFilePath));

            try
            {
                // Ensure backup directory exists
                await EnsureBackupDirectoryExistsAsync();

                // Check if the source file exists
                if (!await _fileSystem.ExistsAsync(configFilePath))
                {
                    _logger.LogWarning("Configuration file does not exist, cannot create backup: {FilePath}", configFilePath);
                    throw new FileNotFoundException($"Configuration file not found: {configFilePath}");
                }

                // Generate backup file name with timestamp
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var fileName = System.IO.Path.GetFileName(configFilePath);
                var backupFileName = $"{fileName}.{timestamp}.backup";
                var backupFilePath = $"{_backupDirectory}/{backupFileName}";

                // Read the source configuration
                var configContent = await _fileSystem.ReadAllTextAsync(configFilePath);

                // Create backup metadata
                var backupMetadata = new ConfigurationBackupMetadata
                {
                    OriginalPath = configFilePath,
                    BackupPath = backupFilePath,
                    BackupTime = DateTime.UtcNow,
                    FileSize = configContent.Length,
                    Version = GenerateVersionHash(configContent)
                };

                // Save backup file
                await _fileSystem.WriteAllTextAsync(backupFilePath, configContent);

                // Save backup metadata
                var metadataPath = backupFilePath + ".meta";
                var metadataJson = JsonSerializer.Serialize(backupMetadata, new JsonSerializerOptions { WriteIndented = true });
                await _fileSystem.WriteAllTextAsync(metadataPath, metadataJson);

                _logger.LogInformation("Configuration backup created: {BackupPath}", backupFilePath);

                // Clean up old backups
                await CleanupOldBackupsAsync(configFilePath);

                return backupFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create configuration backup for: {FilePath}", configFilePath);
                throw;
            }
        }

        /// <summary>
        /// Restores a configuration file from a backup
        /// </summary>
        /// <param name="backupFilePath">Path to the backup file</param>
        /// <param name="targetPath">Target path to restore to (optional, uses original path if null)</param>
        /// <returns>The path where the configuration was restored</returns>
        public async Task<string> RestoreBackupAsync(string backupFilePath, string? targetPath = null)
        {
            if (string.IsNullOrEmpty(backupFilePath))
                throw new ArgumentException("Backup file path cannot be null or empty", nameof(backupFilePath));

            try
            {
                // Check if backup file exists
                if (!await _fileSystem.ExistsAsync(backupFilePath))
                {
                    throw new FileNotFoundException($"Backup file not found: {backupFilePath}");
                }

                // Load backup metadata
                var metadataPath = backupFilePath + ".meta";
                ConfigurationBackupMetadata? metadata = null;
                
                if (await _fileSystem.ExistsAsync(metadataPath))
                {
                    var metadataJson = await _fileSystem.ReadAllTextAsync(metadataPath);
                    metadata = JsonSerializer.Deserialize<ConfigurationBackupMetadata>(metadataJson);
                }

                // Determine restore target
                var restoreTarget = targetPath ?? metadata?.OriginalPath;
                if (string.IsNullOrEmpty(restoreTarget))
                {
                    throw new InvalidOperationException("Cannot determine restore target path");
                }

                // Create backup of current file before restoring (if it exists)
                if (await _fileSystem.ExistsAsync(restoreTarget))
                {
                    var currentBackupPath = await CreateBackupAsync(restoreTarget);
                    _logger.LogInformation("Created backup of current configuration before restore: {BackupPath}", currentBackupPath);
                }

                // Read backup content
                var backupContent = await _fileSystem.ReadAllTextAsync(backupFilePath);

                // Restore the configuration
                await _fileSystem.WriteAllTextAsync(restoreTarget, backupContent);

                _logger.LogInformation("Configuration restored from backup: {BackupPath} -> {RestoreTarget}", backupFilePath, restoreTarget);

                return restoreTarget;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore configuration from backup: {BackupPath}", backupFilePath);
                throw;
            }        }

        /// <summary>
        /// Lists all available backups for a configuration file
        /// </summary>
        /// <param name="configFilePath">Path to the original configuration file</param>
        /// <returns>List of backup information</returns>
        public async Task<List<ConfigurationBackupInfo>> ListBackupsAsync(string configFilePath)
        {
            var backups = new List<ConfigurationBackupInfo>();

            try
            {
                if (!await _fileSystem.ExistsAsync(_backupDirectory))
                    return backups;

                var fileName = System.IO.Path.GetFileName(configFilePath);
                var backupFiles = await _fileSystem.GetFilesAsync(_backupDirectory);

                foreach (var backupFile in backupFiles)
                {
                    if (backupFile.Name.StartsWith(fileName + ".") && backupFile.Name.EndsWith(".backup"))
                    {
                        var metadataPath = backupFile.FullPath + ".meta";
                        ConfigurationBackupMetadata? metadata = null;

                        if (await _fileSystem.ExistsAsync(metadataPath))
                        {
                            try
                            {
                                var metadataJson = await _fileSystem.ReadAllTextAsync(metadataPath);
                                metadata = JsonSerializer.Deserialize<ConfigurationBackupMetadata>(metadataJson);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to read backup metadata: {MetadataPath}", metadataPath);
                            }
                        }

                        backups.Add(new ConfigurationBackupInfo
                        {
                            BackupPath = backupFile.FullPath,
                            OriginalPath = metadata?.OriginalPath ?? configFilePath,
                            BackupTime = metadata?.BackupTime ?? backupFile.LastModifiedTime,
                            FileSize = metadata?.FileSize ?? backupFile.Size,
                            Version = metadata?.Version ?? "unknown"
                        });
                    }
                }

                // Sort by backup time (newest first)
                backups.Sort((a, b) => b.BackupTime.CompareTo(a.BackupTime));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list backups for configuration file: {FilePath}", configFilePath);
            }

            return backups;
        }

        /// <summary>
        /// Deletes old backups for a configuration file, keeping only the most recent ones
        /// </summary>
        /// <param name="configFilePath">Path to the original configuration file</param>
        /// <returns>Number of backups deleted</returns>
        public async Task<int> CleanupOldBackupsAsync(string configFilePath)
        {
            var deletedCount = 0;

            try
            {
                var backups = await ListBackupsAsync(configFilePath);
                
                if (backups.Count <= _maxBackupCount)
                    return 0; // No cleanup needed

                // Delete oldest backups beyond the limit
                var backupsToDelete = backups.Skip(_maxBackupCount).ToList();
                
                foreach (var backup in backupsToDelete)
                {
                    try
                    {
                        await _fileSystem.DeleteAsync(backup.BackupPath, false);
                        
                        // Also delete metadata file if it exists
                        var metadataPath = backup.BackupPath + ".meta";
                        if (await _fileSystem.ExistsAsync(metadataPath))
                        {
                            await _fileSystem.DeleteAsync(metadataPath, false);
                        }

                        deletedCount++;
                        _logger.LogDebug("Deleted old backup: {BackupPath}", backup.BackupPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete old backup: {BackupPath}", backup.BackupPath);
                    }
                }

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} old backups for: {FilePath}", deletedCount, configFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old backups for: {FilePath}", configFilePath);
            }

            return deletedCount;
        }

        /// <summary>
        /// Creates a full system configuration backup
        /// </summary>
        /// <returns>Path to the system backup archive</returns>
        public async Task<string> CreateSystemBackupAsync()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var systemBackupPath = $"{_backupDirectory}/system_backup_{timestamp}.json";

            try
            {
                await EnsureBackupDirectoryExistsAsync();

                var systemBackup = new Dictionary<string, object>();

                // Backup system configuration
                if (await _fileSystem.ExistsAsync("/etc/hackeros.conf"))
                {
                    systemBackup["system_config"] = await _fileSystem.ReadAllTextAsync("/etc/hackeros.conf");
                }

                // Backup user configurations (scan all users)
                var userConfigs = new Dictionary<string, object>();
                var homeDir = "/home";
                
                if (await _fileSystem.ExistsAsync(homeDir))
                {
                    var userDirs = await _fileSystem.GetDirectoriesAsync(homeDir);
                    
                    foreach (var userDir in userDirs)
                    {
                        var userConfigDir = $"{userDir.FullPath}/.config/hackeros";
                        if (await _fileSystem.ExistsAsync(userConfigDir))
                        {
                            var userConfig = new Dictionary<string, string>();
                            var configFiles = await _fileSystem.GetFilesAsync(userConfigDir);
                            
                            foreach (var configFile in configFiles)
                            {
                                if (configFile.Name.EndsWith(".conf"))
                                {
                                    userConfig[configFile.Name] = await _fileSystem.ReadAllTextAsync(configFile.FullPath);
                                }
                            }
                            
                            if (userConfig.Count > 0)
                            {
                                userConfigs[userDir.Name] = userConfig;
                            }
                        }
                    }
                }

                if (userConfigs.Count > 0)
                {
                    systemBackup["user_configs"] = userConfigs;
                }

                // Add backup metadata
                systemBackup["backup_metadata"] = new
                {
                    created_at = DateTime.UtcNow,
                    hackeros_version = "1.0.0", // TODO: Get actual version
                    backup_type = "full_system"
                };

                // Save system backup
                var backupJson = JsonSerializer.Serialize(systemBackup, new JsonSerializerOptions { WriteIndented = true });
                await _fileSystem.WriteAllTextAsync(systemBackupPath, backupJson);

                _logger.LogInformation("System configuration backup created: {BackupPath}", systemBackupPath);
                return systemBackupPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create system configuration backup");
                throw;
            }
        }

        private async Task EnsureBackupDirectoryExistsAsync()
        {
            if (!await _fileSystem.ExistsAsync(_backupDirectory))
            {
                await _fileSystem.CreateDirectoryAsync(_backupDirectory);
                _logger.LogDebug("Created backup directory: {BackupDirectory}", _backupDirectory);
            }
        }

        private string GenerateVersionHash(string content)
        {
            // Simple hash for version tracking
            return Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(content)))[..16];
        }
    }

    /// <summary>
    /// Metadata for a configuration backup
    /// </summary>
    public class ConfigurationBackupMetadata
    {
        /// <summary>
        /// Path to the original configuration file
        /// </summary>
        public string OriginalPath { get; set; } = string.Empty;

        /// <summary>
        /// Path to the backup file
        /// </summary>
        public string BackupPath { get; set; } = string.Empty;

        /// <summary>
        /// Time when the backup was created
        /// </summary>
        public DateTime BackupTime { get; set; }

        /// <summary>
        /// Size of the backed up file in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Version hash of the configuration content
        /// </summary>
        public string Version { get; set; } = string.Empty;
    }

    /// <summary>
    /// Information about a configuration backup
    /// </summary>
    public class ConfigurationBackupInfo
    {
        /// <summary>
        /// Path to the backup file
        /// </summary>
        public string BackupPath { get; set; } = string.Empty;

        /// <summary>
        /// Path to the original configuration file
        /// </summary>
        public string OriginalPath { get; set; } = string.Empty;

        /// <summary>
        /// Time when the backup was created
        /// </summary>
        public DateTime BackupTime { get; set; }

        /// <summary>
        /// Size of the backup file in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Version identifier of the backup
        /// </summary>
        public string Version { get; set; } = string.Empty;
    }
}
