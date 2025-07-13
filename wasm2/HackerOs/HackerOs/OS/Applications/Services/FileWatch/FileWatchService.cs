using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.Applications.Core;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.Services.FileWatch
{
    /// <summary>
    /// A service that monitors file system changes in specified directories
    /// This is a sample implementation using the new service application architecture
    /// </summary>
    [App("FileWatchService", "system.filewatchservice", 
        Description = "Monitors file system changes in specified directories",
        Type = ApplicationType.ServiceApplication,
        IconPath = "/images/icons/filewatch.png")]
    public class FileWatchService : ServiceBase
    {
        #region Dependencies

        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger<FileWatchService> _logger;

        #endregion

        #region State Variables

        /// <summary>
        /// List of directories being watched
        /// </summary>
        private readonly ConcurrentDictionary<string, WatchInfo> _watchedDirectories = new();

        /// <summary>
        /// Dictionary of file checksums to detect changes
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _fileChecksums = new();

        /// <summary>
        /// Polling interval in milliseconds
        /// </summary>
        private int _pollingIntervalMs = 5000;

        /// <summary>
        /// Cancellation token source for the background worker
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Last scan time
        /// </summary>
        private DateTime _lastScanTime = DateTime.MinValue;

        /// <summary>
        /// Maximum number of notifications to keep in history
        /// </summary>
        private const int MaxNotificationHistory = 100;

        /// <summary>
        /// Notification history
        /// </summary>
        private readonly ConcurrentQueue<FileChangeNotification> _notificationHistory = new();

        /// <summary>
        /// Event for file change notifications
        /// </summary>
        public event EventHandler<FileChangeNotification> FileChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new FileWatchService
        /// </summary>
        /// <param name="fileSystem">Virtual file system service</param>
        /// <param name="logger">Logger</param>
        public FileWatchService(IVirtualFileSystem fileSystem, ILogger<FileWatchService> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Service Lifecycle

        /// <summary>
        /// Called when the service is started
        /// </summary>
        /// <param name="context">Application launch context</param>
        protected override async Task<bool> OnStartAsync(ApplicationLaunchContext context)
        {
            _logger.LogInformation("Starting FileWatchService");

            try
            {
                // Process any configuration from the launch context
                if (context.AdditionalData.TryGetValue("pollingInterval", out var interval) && 
                    interval is int intervalMs)
                {
                    _pollingIntervalMs = Math.Max(1000, intervalMs); // Minimum 1 second
                }

                // Add default directories to watch if specified
                if (context.AdditionalData.TryGetValue("watchDirectories", out var dirs) && 
                    dirs is List<string> directories)
                {
                    foreach (var dir in directories)
                    {
                        await AddWatchDirectoryAsync(dir);
                    }
                }

                // Add user's home directory by default if no directories specified
                if (_watchedDirectories.Count == 0 && context.UserSession?.User != null)
                {
                    await AddWatchDirectoryAsync(context.UserSession.User.HomeDirectory);
                }

                // Initialize and start the background worker
                _cancellationTokenSource = new CancellationTokenSource();
                _ = Task.Run(() => MonitorFileSystemAsync(_cancellationTokenSource.Token));

                await RaiseOutputReceivedAsync("FileWatchService started successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting FileWatchService");
                await RaiseErrorReceivedAsync($"Failed to start FileWatchService: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Called when the service is stopped
        /// </summary>
        protected override async Task<bool> OnStopAsync()
        {
            _logger.LogInformation("Stopping FileWatchService");

            try
            {
                // Cancel the background worker
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                // Clear watched directories and checksums
                _watchedDirectories.Clear();
                _fileChecksums.Clear();
                
                await RaiseOutputReceivedAsync("FileWatchService stopped successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping FileWatchService");
                await RaiseErrorReceivedAsync($"Failed to stop FileWatchService: {ex.Message}", ex);
                return false;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Add a directory to watch
        /// </summary>
        /// <param name="directoryPath">Path to the directory</param>
        /// <param name="recursive">Whether to watch subdirectories</param>
        /// <param name="filePattern">File pattern to watch (e.g. *.txt)</param>
        public async Task<bool> AddWatchDirectoryAsync(string directoryPath, bool recursive = true, string filePattern = "*")
        {
            try
            {
                // Validate directory exists
                var directoryExists = await _fileSystem.DirectoryExistsAsync(directoryPath);
                if (!directoryExists)
                {
                    await RaiseErrorReceivedAsync($"Directory does not exist: {directoryPath}");
                    return false;
                }

                // Add or update watch info
                var watchInfo = new WatchInfo
                {
                    DirectoryPath = directoryPath,
                    Recursive = recursive,
                    FilePattern = filePattern
                };

                _watchedDirectories[directoryPath] = watchInfo;
                
                await RaiseOutputReceivedAsync($"Now watching directory: {directoryPath}");
                _logger.LogInformation("Added directory watch: {DirectoryPath}, Recursive: {Recursive}, Pattern: {Pattern}", 
                    directoryPath, recursive, filePattern);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding directory watch: {DirectoryPath}", directoryPath);
                await RaiseErrorReceivedAsync($"Failed to watch directory {directoryPath}: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Remove a directory from the watch list
        /// </summary>
        /// <param name="directoryPath">Path to the directory</param>
        public async Task<bool> RemoveWatchDirectoryAsync(string directoryPath)
        {
            if (_watchedDirectories.TryRemove(directoryPath, out _))
            {
                // Remove file checksums for this directory
                var keysToRemove = new List<string>();
                foreach (var key in _fileChecksums.Keys)
                {
                    if (key.StartsWith(directoryPath))
                    {
                        keysToRemove.Add(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _fileChecksums.TryRemove(key, out _);
                }

                await RaiseOutputReceivedAsync($"Stopped watching directory: {directoryPath}");
                _logger.LogInformation("Removed directory watch: {DirectoryPath}", directoryPath);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the list of watched directories
        /// </summary>
        public IEnumerable<WatchInfo> GetWatchedDirectories()
        {
            return _watchedDirectories.Values;
        }

        /// <summary>
        /// Get the notification history
        /// </summary>
        public IEnumerable<FileChangeNotification> GetNotificationHistory()
        {
            return _notificationHistory;
        }

        /// <summary>
        /// Set the polling interval
        /// </summary>
        /// <param name="intervalMs">Interval in milliseconds (minimum 1000)</param>
        public void SetPollingInterval(int intervalMs)
        {
            _pollingIntervalMs = Math.Max(1000, intervalMs);
            _logger.LogInformation("Polling interval set to {PollingInterval}ms", _pollingIntervalMs);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Background worker method to monitor the file system
        /// </summary>
        private async Task MonitorFileSystemAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("File system monitoring started with {PollingInterval}ms interval", _pollingIntervalMs);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ScanWatchedDirectoriesAsync();
                    
                    // Wait for the next scan interval
                    await Task.Delay(_pollingIntervalMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation, just exit
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in file system monitoring");
                    await RaiseErrorReceivedAsync($"Error monitoring file system: {ex.Message}", ex);
                    
                    // Wait a bit before retrying
                    try
                    {
                        await Task.Delay(10000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _logger.LogInformation("File system monitoring stopped");
        }

        /// <summary>
        /// Scan all watched directories for changes
        /// </summary>
        private async Task ScanWatchedDirectoriesAsync()
        {
            _lastScanTime = DateTime.UtcNow;
            
            foreach (var watchInfo in _watchedDirectories.Values)
            {
                await ScanDirectoryAsync(watchInfo.DirectoryPath, watchInfo.Recursive, watchInfo.FilePattern);
            }
        }

        /// <summary>
        /// Scan a directory for changes
        /// </summary>
        private async Task ScanDirectoryAsync(string directoryPath, bool recursive, string filePattern)
        {
            try
            {
                // Get all files in the directory
                var entries = await _fileSystem.GetDirectoryEntriesAsync(directoryPath);
                
                // Track current files to detect deletions
                var currentFiles = new HashSet<string>();
                
                foreach (var entry in entries)
                {
                    if (entry.IsDirectory && recursive)
                    {
                        // Recursively scan subdirectories
                        await ScanDirectoryAsync(entry.FullPath, recursive, filePattern);
                    }
                    else if (!entry.IsDirectory)
                    {
                        // Check if file matches pattern
                        if (MatchesPattern(entry.Name, filePattern))
                        {
                            currentFiles.Add(entry.FullPath);
                            
                            // Check for new or modified files
                            await CheckFileChangesAsync(entry.FullPath);
                        }
                    }
                }
                
                // Check for deleted files
                var filesToCheck = new List<string>();
                foreach (var key in _fileChecksums.Keys)
                {
                    // Only check files in the current directory (not subdirectories)
                    if (GetDirectoryPath(key) == directoryPath && !currentFiles.Contains(key))
                    {
                        filesToCheck.Add(key);
                    }
                }
                
                foreach (var filePath in filesToCheck)
                {
                    var fileExists = await _fileSystem.FileExistsAsync(filePath);
                    if (!fileExists)
                    {
                        // File was deleted
                        if (_fileChecksums.TryRemove(filePath, out _))
                        {
                            RaiseFileChangeNotification(filePath, FileChangeType.Deleted);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning directory: {DirectoryPath}", directoryPath);
                await RaiseErrorReceivedAsync($"Error scanning directory {directoryPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Check if a file has been modified
        /// </summary>
        private async Task CheckFileChangesAsync(string filePath)
        {
            try
            {
                // Calculate checksum for the file
                var checksum = await CalculateFileChecksumAsync(filePath);
                
                if (_fileChecksums.TryGetValue(filePath, out var oldChecksum))
                {
                    if (checksum != oldChecksum)
                    {
                        // File was modified
                        _fileChecksums[filePath] = checksum;
                        RaiseFileChangeNotification(filePath, FileChangeType.Modified);
                    }
                }
                else
                {
                    // New file
                    _fileChecksums[filePath] = checksum;
                    RaiseFileChangeNotification(filePath, FileChangeType.Created);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file changes: {FilePath}", filePath);
            }
        }

        /// <summary>
        /// Calculate a simple checksum for a file
        /// </summary>
        private async Task<string> CalculateFileChecksumAsync(string filePath)
        {
            try
            {
                var fileInfo = await _fileSystem.GetFileInfoAsync(filePath);
                return $"{fileInfo.Size}_{fileInfo.LastModified.Ticks}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating file checksum: {FilePath}", filePath);
                return Guid.NewGuid().ToString(); // Return a unique value to force detection as a change
            }
        }

        /// <summary>
        /// Check if a filename matches a pattern
        /// </summary>
        private bool MatchesPattern(string fileName, string pattern)
        {
            if (pattern == "*") return true;
            
            // Simple wildcard matching
            if (pattern.StartsWith("*") && pattern.Length > 1)
            {
                var extension = pattern.Substring(1);
                return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
            }
            
            return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get the directory part of a file path
        /// </summary>
        private string GetDirectoryPath(string filePath)
        {
            var lastSeparator = filePath.LastIndexOf('/');
            if (lastSeparator >= 0)
            {
                return filePath.Substring(0, lastSeparator);
            }
            
            return string.Empty;
        }

        /// <summary>
        /// Raise a file change notification
        /// </summary>
        private void RaiseFileChangeNotification(string filePath, FileChangeType changeType)
        {
            var notification = new FileChangeNotification
            {
                FilePath = filePath,
                ChangeType = changeType,
                Timestamp = DateTime.UtcNow
            };
            
            // Add to history
            _notificationHistory.Enqueue(notification);
            
            // Trim history if it gets too large
            while (_notificationHistory.Count > MaxNotificationHistory && 
                   _notificationHistory.TryDequeue(out _))
            {
                // Just removing excess items
            }
            
            // Log the change
            _logger.LogInformation("File change detected: {FilePath}, Type: {ChangeType}", 
                filePath, changeType);
            
            // Raise event
            FileChanged?.Invoke(this, notification);
            
            // Raise application output event
            _ = RaiseOutputReceivedAsync($"{changeType} - {filePath}");
        }

        #endregion
    }

    /// <summary>
    /// Information about a watched directory
    /// </summary>
    public class WatchInfo
    {
        /// <summary>
        /// Path to the directory
        /// </summary>
        public string DirectoryPath { get; set; }
        
        /// <summary>
        /// Whether to watch subdirectories
        /// </summary>
        public bool Recursive { get; set; }
        
        /// <summary>
        /// File pattern to watch (e.g. *.txt)
        /// </summary>
        public string FilePattern { get; set; } = "*";
    }

    /// <summary>
    /// Type of file change
    /// </summary>
    public enum FileChangeType
    {
        /// <summary>
        /// File was created
        /// </summary>
        Created,
        
        /// <summary>
        /// File was modified
        /// </summary>
        Modified,
        
        /// <summary>
        /// File was deleted
        /// </summary>
        Deleted
    }

    /// <summary>
    /// Notification for a file change
    /// </summary>
    public class FileChangeNotification
    {
        /// <summary>
        /// Path to the file
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// Type of change
        /// </summary>
        public FileChangeType ChangeType { get; set; }
        
        /// <summary>
        /// When the change was detected
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
