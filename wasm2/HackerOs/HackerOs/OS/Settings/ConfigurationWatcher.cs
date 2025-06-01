using HackerOs.IO.FileSystem;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Monitors configuration files for changes and triggers reload events
    /// </summary>
    public class ConfigurationWatcher : IDisposable
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger<ConfigurationWatcher> _logger;
        private readonly Dictionary<string, WatchedFile> _watchedFiles;
        private readonly object _lockObject = new object();
        private bool _disposed = false;        public ConfigurationWatcher(IVirtualFileSystem fileSystem, ILogger<ConfigurationWatcher> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _watchedFiles = new Dictionary<string, WatchedFile>();

            // Subscribe to file system events
            _fileSystem.OnFileSystemEvent += OnFileSystemEvent;
        }

        /// <summary>
        /// Event raised when a watched configuration file changes
        /// </summary>
        public event EventHandler<ConfigurationFileChangedEventArgs>? ConfigurationFileChanged;        /// <summary>
        /// Starts watching a configuration file for changes
        /// </summary>
        /// <param name="filePath">Path to the configuration file</param>
        /// <param name="debounceMs">Debounce time in milliseconds (default: 500ms)</param>
        public async Task WatchFileAsync(string filePath, int debounceMs = 500)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (debounceMs < 0)
                throw new ArgumentException("Debounce time cannot be negative", nameof(debounceMs));

            // Normalize path manually (simple implementation)
            var normalizedPath = NormalizePath(filePath);

            lock (_lockObject)
            {
                if (_watchedFiles.ContainsKey(normalizedPath))
                {
                    _logger.LogWarning("File {FilePath} is already being watched", normalizedPath);
                    return;
                }

                var watchedFile = new WatchedFile
                {
                    Path = normalizedPath,
                    DebounceMs = debounceMs,
                    LastModified = DateTime.UtcNow
                };

                _watchedFiles[normalizedPath] = watchedFile;
            }

            _logger.LogDebug("Started watching configuration file: {FilePath}", normalizedPath);

            // Check if file exists and get initial state
            if (await _fileSystem.ExistsAsync(normalizedPath))
            {
                try
                {
                    // Since GetFileStatAsync doesn't exist, we'll use the current time
                    // and rely on file change events to detect modifications
                    lock (_lockObject)
                    {
                        if (_watchedFiles.TryGetValue(normalizedPath, out var file))
                        {
                            file.LastModified = DateTime.UtcNow;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get initial state for watched file: {FilePath}", normalizedPath);
                }
            }
        }        /// <summary>
        /// Stops watching a configuration file
        /// </summary>
        /// <param name="filePath">Path to the configuration file</param>
        public void StopWatching(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            var normalizedPath = NormalizePath(filePath);

            lock (_lockObject)
            {
                if (_watchedFiles.TryGetValue(normalizedPath, out var watchedFile))
                {
                    // Cancel any pending debounce timer
                    watchedFile.DebounceTimer?.Dispose();
                    _watchedFiles.Remove(normalizedPath);
                    _logger.LogDebug("Stopped watching configuration file: {FilePath}", normalizedPath);
                }
            }
        }

        /// <summary>
        /// Gets the list of currently watched files
        /// </summary>
        public IReadOnlyList<string> GetWatchedFiles()
        {
            lock (_lockObject)
            {
                return new List<string>(_watchedFiles.Keys);
            }
        }        private void OnFileSystemEvent(object? sender, FileSystemEvent e)
        {
            if (_disposed)
                return;

            // Only handle file modification events (using FileWritten since FileModified doesn't exist)
            if (e.EventType != FileSystemEventType.FileWritten)
                return;

            WatchedFile? watchedFile = null;
            lock (_lockObject)
            {
                _watchedFiles.TryGetValue(e.Path, out watchedFile);
            }

            if (watchedFile == null)
                return;

            _logger.LogDebug("File system event for watched file: {FilePath} - {EventType}", e.Path, e.EventType);

            // Cancel existing debounce timer
            watchedFile.DebounceTimer?.Dispose();

            // Start new debounce timer
            watchedFile.DebounceTimer = new Timer(
                callback: _ => HandleDebouncedFileChange(e.Path),
                state: null,
                dueTime: TimeSpan.FromMilliseconds(watchedFile.DebounceMs),
                period: Timeout.InfiniteTimeSpan
            );
        }

        private async void HandleDebouncedFileChange(string filePath)
        {
            if (_disposed)
                return;

            try
            {
                _logger.LogDebug("Processing debounced file change: {FilePath}", filePath);

                WatchedFile? watchedFile = null;
                lock (_lockObject)
                {
                    _watchedFiles.TryGetValue(filePath, out watchedFile);
                }

                if (watchedFile == null)
                    return;                // Check if file was actually modified
                DateTime newModifiedTime = DateTime.UtcNow;
                if (await _fileSystem.ExistsAsync(filePath))
                {
                    try
                    {
                        // Use GetNodeAsync to get file information since GetFileStatAsync doesn't exist
                        var node = await _fileSystem.GetNodeAsync(filePath);
                        if (node != null && !node.IsDirectory)
                        {
                            newModifiedTime = node.LastModifiedTime;

                            // Only process if the file was actually modified
                            if (newModifiedTime <= watchedFile.LastModified)
                            {
                                _logger.LogDebug("File {FilePath} modification time unchanged, skipping reload", filePath);
                                return;
                            }

                            watchedFile.LastModified = newModifiedTime;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get file info for {FilePath}", filePath);
                    }
                }
                else
                {
                    _logger.LogInformation("Watched configuration file was deleted: {FilePath}", filePath);
                }

                // Raise configuration file changed event
                var eventArgs = new ConfigurationFileChangedEventArgs(filePath, newModifiedTime);
                ConfigurationFileChanged?.Invoke(this, eventArgs);

                _logger.LogInformation("Configuration file changed: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file change for {FilePath}", filePath);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            lock (_lockObject)
            {
                // Dispose all debounce timers
                foreach (var watchedFile in _watchedFiles.Values)
                {
                    watchedFile.DebounceTimer?.Dispose();
                }
                _watchedFiles.Clear();
            }            // Unsubscribe from file system events
            if (_fileSystem != null)
            {
                _fileSystem.OnFileSystemEvent -= OnFileSystemEvent;
            }

            _logger.LogDebug("ConfigurationWatcher disposed");        }

        /// <summary>
        /// Normalizes a file path by converting backslashes to forward slashes and resolving relative paths
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>The normalized path</returns>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // Convert backslashes to forward slashes for consistency
            var normalized = path.Replace('\\', '/');

            // Remove duplicate slashes
            while (normalized.Contains("//"))
            {
                normalized = normalized.Replace("//", "/");
            }

            // Ensure path starts with / for absolute paths
            if (!normalized.StartsWith('/') && !normalized.StartsWith('.'))
            {
                normalized = "/" + normalized;
            }

            return normalized;
        }

        private class WatchedFile
        {
            public required string Path { get; set; }
            public int DebounceMs { get; set; }
            public DateTime LastModified { get; set; }
            public Timer? DebounceTimer { get; set; }
        }
    }

    /// <summary>
    /// Event arguments for configuration file changes
    /// </summary>
    public class ConfigurationFileChangedEventArgs : EventArgs
    {
        public ConfigurationFileChangedEventArgs(string filePath, DateTime modifiedTime)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            ModifiedTime = modifiedTime;
        }

        /// <summary>
        /// Path to the configuration file that changed
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Time when the file was modified
        /// </summary>
        public DateTime ModifiedTime { get; }
    }
}
