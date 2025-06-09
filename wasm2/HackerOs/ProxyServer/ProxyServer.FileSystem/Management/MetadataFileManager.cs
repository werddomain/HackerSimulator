using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProxyServer.FileSystem.Models;
using ProxyServer.FileSystem.Security;

namespace ProxyServer.FileSystem.Management
{
    /// <summary>
    /// Represents metadata for a file or directory in a shared folder
    /// </summary>
    public class FileMetadata
    {
        /// <summary>
        /// When the file/directory was created
        /// </summary>
        public DateTime Created { get; set; } = DateTime.Now;

        /// <summary>
        /// When the file/directory was last accessed
        /// </summary>
        public DateTime LastAccessed { get; set; } = DateTime.Now;

        /// <summary>
        /// Log of access events
        /// </summary>
        public List<AccessLogEntry> AccessLog { get; set; } = new List<AccessLogEntry>();

        /// <summary>
        /// Custom file permissions
        /// </summary>
        public FilePermissions Permissions { get; set; } = new FilePermissions();
    }

    /// <summary>
    /// Represents a log entry for file access
    /// </summary>
    public class AccessLogEntry
    {
        /// <summary>
        /// User who performed the operation
        /// </summary>
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp of the operation
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Type of operation performed
        /// </summary>
        public FileOperationType Operation { get; set; }
    }

    /// <summary>
    /// Represents Unix-style file permissions
    /// </summary>
    public class FilePermissions
    {
        /// <summary>
        /// Owner permissions (read, write, execute)
        /// </summary>
        public string Owner { get; set; } = "rwx";

        /// <summary>
        /// Group permissions (read, write, execute)
        /// </summary>
        public string Group { get; set; } = "r-x";

        /// <summary>
        /// Others permissions (read, write, execute)
        /// </summary>
        public string Others { get; set; } = "r--";
    }

    /// <summary>
    /// Manages metadata for files and directories in shared folders
    /// </summary>
    public class MetadataFileManager
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, FileMetadata> _metadataCache = new Dictionary<string, FileMetadata>();
        private readonly object _cacheLock = new object();

        public MetadataFileManager(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets metadata for a file or directory
        /// </summary>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="sharedFolder">Shared folder containing the path</param>
        /// <returns>Metadata for the file or directory</returns>
        public FileMetadata GetMetadata(string path, SharedFolderInfo sharedFolder)
        {
            lock (_cacheLock)
            {
                // Check if metadata is in cache
                if (_metadataCache.TryGetValue(path, out var metadata))
                {
                    return metadata;
                }

                // Try to load metadata from file
                var metadataFilePath = GetMetadataFilePath(path, sharedFolder);
                if (File.Exists(metadataFilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(metadataFilePath);
                        metadata = JsonSerializer.Deserialize<FileMetadata>(json) ?? new FileMetadata();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("MetadataFileManager", $"Error loading metadata for {path}: {ex.Message}");
                        metadata = new FileMetadata();
                    }
                }
                else
                {
                    // Create new metadata
                    metadata = new FileMetadata();
                }

                // Add to cache
                _metadataCache[path] = metadata;
                return metadata;
            }
        }

        /// <summary>
        /// Updates access information for a file or directory
        /// </summary>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="operation">Type of operation</param>
        /// <param name="sharedFolder">Shared folder containing the path</param>
        public void UpdateAccessInfo(string path, string user, FileOperationType operation, SharedFolderInfo sharedFolder)
        {
            var metadata = GetMetadata(path, sharedFolder);
            
            // Update last accessed time
            metadata.LastAccessed = DateTime.Now;
            
            // Add to access log
            metadata.AccessLog.Add(new AccessLogEntry
            {
                User = user,
                Timestamp = DateTime.Now,
                Operation = operation
            });
            
            // Limit log size to prevent growth
            if (metadata.AccessLog.Count > 100)
            {
                metadata.AccessLog = metadata.AccessLog.Skip(metadata.AccessLog.Count - 100).ToList();
            }
            
            // Save metadata
            SaveMetadataFile(path, metadata, sharedFolder);
        }

        /// <summary>
        /// Sets permissions for a file or directory
        /// </summary>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="permissions">New permissions</param>
        /// <param name="sharedFolder">Shared folder containing the path</param>
        public void SetPermissions(string path, FilePermissions permissions, SharedFolderInfo sharedFolder)
        {
            var metadata = GetMetadata(path, sharedFolder);
            metadata.Permissions = permissions;
            
            // Save metadata
            SaveMetadataFile(path, metadata, sharedFolder);
        }

        /// <summary>
        /// Gets the path to the metadata file for a given file or directory
        /// </summary>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="sharedFolder">Shared folder containing the path</param>
        /// <returns>Path to the metadata file</returns>
        private string GetMetadataFilePath(string path, SharedFolderInfo sharedFolder)
        {
            var directory = Path.GetDirectoryName(path) ?? path;
            return Path.Combine(directory, sharedFolder.MetadataFileName);
        }

        /// <summary>
        /// Saves metadata to file
        /// </summary>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="metadata">Metadata to save</param>
        /// <param name="sharedFolder">Shared folder containing the path</param>
        private void SaveMetadataFile(string path, FileMetadata metadata, SharedFolderInfo sharedFolder)
        {
            try
            {
                var metadataFilePath = GetMetadataFilePath(path, sharedFolder);
                var directory = Path.GetDirectoryName(metadataFilePath);
                
                // Ensure directory exists
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Write metadata to file
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                File.WriteAllText(metadataFilePath, json);
                
                // Update cache
                lock (_cacheLock)
                {
                    _metadataCache[path] = metadata;
                }
                
                _logger.LogDebug("MetadataFileManager", $"Updated metadata for {path}");
            }
            catch (Exception ex)
            {
                _logger.LogError("MetadataFileManager", $"Error saving metadata for {path}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Clears the metadata cache
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _metadataCache.Clear();
            }
        }
    }
}
