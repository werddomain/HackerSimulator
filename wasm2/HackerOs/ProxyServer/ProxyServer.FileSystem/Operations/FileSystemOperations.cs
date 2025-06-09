using Microsoft.Extensions.Logging;
using ProxyServer.FileSystem.Management;
using ProxyServer.FileSystem.Models;
using ProxyServer.FileSystem.Security;
using System.Text.Json.Serialization;

namespace ProxyServer.FileSystem.Operations
{    /// <summary>
    /// Represents an entry in the file system (file or directory)
    /// </summary>
    public class FileSystemEntry
    {
        /// <summary>
        /// Name of the file or directory
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Full path of the file or directory
        /// </summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>
        /// Virtual path if accessed through mount point
        /// </summary>
        public string? VirtualPath { get; set; }
        
        /// <summary>
        /// Whether the entry is a directory
        /// </summary>
        public bool IsDirectory { get; set; }
        
        /// <summary>
        /// Size of the file in bytes (0 for directories)
        /// </summary>
        public long Size { get; set; }
        
        /// <summary>
        /// Last modified time of the file or directory
        /// </summary>
        public DateTime LastModified { get; set; }
        
        /// <summary>
        /// Last access time of the file or directory
        /// </summary>
        public DateTime LastAccessed { get; set; }
        
        /// <summary>
        /// Creation time of the file or directory
        /// </summary>
        public DateTime CreationTime { get; set; }
        
        /// <summary>
        /// File attributes
        /// </summary>
        public FileAttributes Attributes { get; set; }
        
        /// <summary>
        /// Mount point ID if entry was accessed through a mount point
        /// </summary>
        [JsonIgnore]
        public string? MountPointId { get; set; }
        
        /// <summary>
        /// Shared folder ID for this entry
        /// </summary>
        [JsonIgnore]
        public string? SharedFolderId { get; set; }
    }    /// <summary>
    /// Provides file system operations for shared folders and mount points
    /// </summary>
    public class FileSystemOperations
    {
        private readonly ILogger _logger;
        private readonly FileSystemSecurity _security;
        private readonly SharedFolderManager _sharedFolderManager;
        private readonly MountPointManager _mountPointManager;
        private readonly MetadataFileManager _metadataManager;
        
        public FileSystemOperations(
            ILogger logger,
            FileSystemSecurity security,
            SharedFolderManager sharedFolderManager,
            MountPointManager mountPointManager,
            MetadataFileManager metadataManager)
        {
            _logger = logger;
            _security = security;
            _sharedFolderManager = sharedFolderManager;
            _mountPointManager = mountPointManager;
            _metadataManager = metadataManager;
        }
        
        #region Mount Point Operations
        
        /// <summary>
        /// Resolves a virtual path to a real file system path
        /// </summary>
        /// <param name="virtualPath">Virtual path to resolve</param>
        /// <param name="username">Username for access logging</param>
        /// <returns>Resolved path information or null if not found</returns>
        public (string HostPath, SharedFolderInfo SharedFolder, MountPoint MountPoint)? ResolveVirtualPath(
            string virtualPath, 
            string username = "anonymous")
        {
            try
            {
                // Use mount point manager to resolve the path
                var hostPath = _mountPointManager.ResolveVirtualPath(
                    virtualPath, 
                    out var sharedFolder,
                    out var mountPoint);
                
                if (hostPath == null || sharedFolder == null || mountPoint == null)
                {
                    _security.LogAccess(username, virtualPath, FileOperationType.Read, false);
                    return null;
                }
                
                // Validate that the path exists
                if (!File.Exists(hostPath) && !Directory.Exists(hostPath))
                {
                    _security.LogAccess(username, virtualPath, FileOperationType.Read, false);
                    return null;
                }
                
                return (hostPath, sharedFolder, mountPoint);
            }
            catch (Exception ex)
            {
                _logger.LogError("FileSystemOperations", $"Error resolving virtual path {virtualPath}: {ex.Message}", ex);
                _security.LogAccess(username, virtualPath, FileOperationType.Read, false);
                return null;
            }
        }
        
        /// <summary>
        /// Creates a new mount point
        /// </summary>
        /// <param name="sharedFolderId">ID of the shared folder to mount</param>
        /// <param name="virtualPath">Virtual path where the shared folder will be mounted</param>
        /// <param name="options">Mount options</param>
        /// <param name="username">Username for access logging</param>
        /// <returns>The created mount point</returns>
        public MountPoint CreateMountPoint(
            string sharedFolderId,
            string virtualPath,
            MountOptions options,
            string username = "anonymous")
        {
            try
            {
                // First validate the shared folder exists
                var sharedFolder = _sharedFolderManager.GetSharedFolder(sharedFolderId);
                if (sharedFolder == null)
                {
                    _logger.LogWarning("FileSystemOperations", 
                        $"User {username} attempted to create mount point with non-existent shared folder: {sharedFolderId}");
                    throw new ArgumentException($"Shared folder with ID {sharedFolderId} not found");
                }
                
                // Create the mount point
                var mountPoint = _mountPointManager.CreateMountPoint(sharedFolderId, virtualPath, options);
                  _logger.LogInformation("User {username} created mount point at {virtualPath} for shared folder {alias}", 
                    username, virtualPath, sharedFolder.Alias);
                
                return mountPoint;
            }
            catch (Exception ex)
            {
                _logger.LogError("FileSystemOperations", 
                    $"Error creating mount point {virtualPath} for {sharedFolderId}: {ex.Message}", ex);
                throw;
            }
        }
        
        /// <summary>
        /// Gets all active mount points
        /// </summary>
        /// <returns>List of mount points</returns>
        public IReadOnlyList<MountPoint> GetMountPoints()
        {
            return _mountPointManager.GetMountPoints();
        }
        
        /// <summary>
        /// Removes a mount point
        /// </summary>
        /// <param name="id">ID of the mount point to remove</param>
        /// <param name="permanently">Whether to permanently remove the mount point</param>
        /// <param name="username">Username for access logging</param>
        /// <returns>True if removal was successful</returns>
        public bool RemoveMountPoint(string id, bool permanently = false, string username = "anonymous")
        {
            try
            {
                var mountPoint = _mountPointManager.GetMountPoint(id);
                if (mountPoint == null)
                {
                    _logger.LogWarning("FileSystemOperations", 
                        $"User {username} attempted to remove non-existent mount point: {id}");
                    return false;
                }
                
                bool removed = _mountPointManager.RemoveMountPoint(id, permanently);
                
                if (removed)
                {                    _logger.LogInformation("User {username} {action} mount point at {virtualPath}", 
                        username, permanently ? "permanently removed" : "deactivated", mountPoint.VirtualPath);
                }
                
                return removed;
            }
            catch (Exception ex)
            {
                _logger.LogError("FileSystemOperations", 
                    $"Error removing mount point {id}: {ex.Message}", ex);
                throw;
            }
        }
        
        #endregion

        #region File Operations

        /// <summary>
        /// Reads a file from a shared folder
        /// </summary>
        /// <param name="relativePath">Relative path within the shared folder</param>
        /// <param name="shareId">ID of the shared folder</param>
        /// <param name="username">User performing the operation</param>
        /// <returns>File content as byte array</returns>
        public async Task<byte[]> ReadFileAsync(string relativePath, string shareId, string username = "anonymous")
        {
            var sharedFolder = _sharedFolderManager.GetSharedFolder(shareId);
            if (sharedFolder == null)
            {
                _security.LogAccess(username, relativePath, FileOperationType.Read, false);
                throw new ArgumentException($"Shared folder with ID {shareId} not found");
            }

            // Validate the path
            if (!_security.ValidatePath(relativePath, sharedFolder, out var normalizedPath))
            {
                _security.LogAccess(username, relativePath, FileOperationType.Read, false);
                throw new ArgumentException($"Invalid path: {relativePath}");
            }

            // Check if operation is allowed
            if (!_security.IsOperationAllowed(FileOperationType.Read, normalizedPath, sharedFolder))
            {
                _security.LogAccess(username, relativePath, FileOperationType.Read, false);
                throw new UnauthorizedAccessException($"Read operation not allowed on {relativePath}");
            }

            try
            {
                // Check if file exists
                if (!File.Exists(normalizedPath))
                {
                    _security.LogAccess(username, relativePath, FileOperationType.Read, false);
                    throw new FileNotFoundException($"File not found: {relativePath}");
                }

                // Read file content
                var content = await File.ReadAllBytesAsync(normalizedPath);
                
                // Update access info
                _metadataManager.UpdateAccessInfo(normalizedPath, username, FileOperationType.Read, sharedFolder);
                _sharedFolderManager.UpdateLastAccessed(shareId);
                
                // Log access
                _security.LogAccess(username, relativePath, FileOperationType.Read, true);
                
                return content;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not UnauthorizedAccessException && ex is not FileNotFoundException)
            {
                _logger.LogError("FileSystemOperations", $"Error reading file {relativePath}: {ex.Message}", ex);
                _security.LogAccess(username, relativePath, FileOperationType.Read, false);
                throw;
            }
        }

        /// <summary>
        /// Writes content to a file in a shared folder
        /// </summary>
        /// <param name="relativePath">Relative path within the shared folder</param>
        /// <param name="content">Content to write</param>
        /// <param name="shareId">ID of the shared folder</param>
        /// <param name="username">User performing the operation</param>
        /// <param name="overwrite">Whether to overwrite if file exists</param>
        /// <returns>True if operation was successful</returns>
        public async Task<bool> WriteFileAsync(
            string relativePath, 
            byte[] content, 
            string shareId, 
            string username = "anonymous",
            bool overwrite = true)
        {
            var sharedFolder = _sharedFolderManager.GetSharedFolder(shareId);
            if (sharedFolder == null)
            {
                _security.LogAccess(username, relativePath, FileOperationType.Write, false);
                throw new ArgumentException($"Shared folder with ID {shareId} not found");
            }

            // Validate the path
            if (!_security.ValidatePath(relativePath, sharedFolder, out var normalizedPath))
            {
                _security.LogAccess(username, relativePath, FileOperationType.Write, false);
                throw new ArgumentException($"Invalid path: {relativePath}");
            }

            // Check file name validity
            var fileName = Path.GetFileName(relativePath);
            if (!_security.IsValidFileName(fileName))
            {
                _security.LogAccess(username, relativePath, FileOperationType.Write, false);
                throw new ArgumentException($"Invalid file name: {fileName}");
            }

            // Check if operation is allowed
            if (!_security.IsOperationAllowed(FileOperationType.Write, normalizedPath, sharedFolder))
            {
                _security.LogAccess(username, relativePath, FileOperationType.Write, false);
                throw new UnauthorizedAccessException($"Write operation not allowed on {relativePath}");
            }

            try
            {
                // Check if file exists
                bool fileExists = File.Exists(normalizedPath);
                if (fileExists && !overwrite)
                {
                    _security.LogAccess(username, relativePath, FileOperationType.Write, false);
                    throw new IOException($"File already exists: {relativePath}");
                }

                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(normalizedPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write file content
                await File.WriteAllBytesAsync(normalizedPath, content);
                
                // Update access info
                var operationType = fileExists ? FileOperationType.Write : FileOperationType.Create;
                _metadataManager.UpdateAccessInfo(normalizedPath, username, operationType, sharedFolder);
                _sharedFolderManager.UpdateLastAccessed(shareId);
                
                // Log access
                _security.LogAccess(username, relativePath, operationType, true);
                
                return true;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not UnauthorizedAccessException && ex is not IOException)
            {
                _logger.LogError("FileSystemOperations", $"Error writing file {relativePath}: {ex.Message}", ex);
                _security.LogAccess(username, relativePath, FileOperationType.Write, false);
                throw;
            }
        }

        /// <summary>
        /// Deletes a file or directory from a shared folder
        /// </summary>
        /// <param name="relativePath">Relative path within the shared folder</param>
        /// <param name="shareId">ID of the shared folder</param>
        /// <param name="username">User performing the operation</param>
        /// <param name="recursive">Whether to delete directories recursively</param>
        /// <returns>True if operation was successful</returns>
        public bool DeleteFileOrDirectory(
            string relativePath, 
            string shareId, 
            string username = "anonymous",
            bool recursive = false)
        {
            var sharedFolder = _sharedFolderManager.GetSharedFolder(shareId);
            if (sharedFolder == null)
            {
                _security.LogAccess(username, relativePath, FileOperationType.Delete, false);
                throw new ArgumentException($"Shared folder with ID {shareId} not found");
            }

            // Validate the path
            if (!_security.ValidatePath(relativePath, sharedFolder, out var normalizedPath))
            {
                _security.LogAccess(username, relativePath, FileOperationType.Delete, false);
                throw new ArgumentException($"Invalid path: {relativePath}");
            }

            // Check if operation is allowed
            if (!_security.IsOperationAllowed(FileOperationType.Delete, normalizedPath, sharedFolder))
            {
                _security.LogAccess(username, relativePath, FileOperationType.Delete, false);
                throw new UnauthorizedAccessException($"Delete operation not allowed on {relativePath}");
            }

            try
            {
                bool success = false;
                
                // Check if path exists
                if (File.Exists(normalizedPath))
                {
                    // Delete file
                    File.Delete(normalizedPath);
                    success = true;
                }
                else if (Directory.Exists(normalizedPath))
                {
                    // Delete directory
                    if (recursive)
                    {
                        Directory.Delete(normalizedPath, true);
                    }
                    else
                    {
                        Directory.Delete(normalizedPath);
                    }
                    success = true;
                }
                else
                {
                    _security.LogAccess(username, relativePath, FileOperationType.Delete, false);
                    return false;
                }
                
                if (success)
                {
                    // Update access info
                    _sharedFolderManager.UpdateLastAccessed(shareId);
                    
                    // Log access
                    _security.LogAccess(username, relativePath, FileOperationType.Delete, true);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError("FileSystemOperations", $"Error deleting {relativePath}: {ex.Message}", ex);
                _security.LogAccess(username, relativePath, FileOperationType.Delete, false);
                throw;
            }
        }

        #endregion

        #region Directory Operations

        /// <summary>
        /// Lists the contents of a directory in a shared folder
        /// </summary>
        /// <param name="relativePath">Relative path within the shared folder</param>
        /// <param name="shareId">ID of the shared folder</param>
        /// <param name="username">User performing the operation</param>
        /// <param name="includeHidden">Whether to include hidden files</param>
        /// <returns>List of file system entries</returns>
        public IEnumerable<FileSystemEntry> ListDirectory(
            string relativePath, 
            string shareId, 
            string username = "anonymous",
            bool includeHidden = false)
        {
            var sharedFolder = _sharedFolderManager.GetSharedFolder(shareId);
            if (sharedFolder == null)
            {
                _security.LogAccess(username, relativePath, FileOperationType.List, false);
                throw new ArgumentException($"Shared folder with ID {shareId} not found");
            }

            // Handle empty path (root directory)
            if (string.IsNullOrEmpty(relativePath))
            {
                relativePath = ".";
            }

            // Validate the path
            if (!_security.ValidatePath(relativePath, sharedFolder, out var normalizedPath))
            {
                _security.LogAccess(username, relativePath, FileOperationType.List, false);
                throw new ArgumentException($"Invalid path: {relativePath}");
            }

            // Check if operation is allowed
            if (!_security.IsOperationAllowed(FileOperationType.List, normalizedPath, sharedFolder))
            {
                _security.LogAccess(username, relativePath, FileOperationType.List, false);
                throw new UnauthorizedAccessException($"List operation not allowed on {relativePath}");
            }

            try
            {
                // Check if directory exists
                if (!Directory.Exists(normalizedPath))
                {
                    _security.LogAccess(username, relativePath, FileOperationType.List, false);
                    throw new DirectoryNotFoundException($"Directory not found: {relativePath}");
                }

                var entries = new List<FileSystemEntry>();
                
                // Get directories
                foreach (var directory in Directory.GetDirectories(normalizedPath))
                {
                    var dirInfo = new DirectoryInfo(directory);
                    
                    // Skip hidden directories if not including hidden
                    if (!includeHidden && (dirInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                    {
                        continue;
                    }
                    
                    // Skip metadata file
                    if (dirInfo.Name == sharedFolder.MetadataFileName)
                    {
                        continue;
                    }
                    
                    entries.Add(new FileSystemEntry
                    {
                        Name = dirInfo.Name,
                        Path = GetRelativePath(directory, sharedFolder.HostPath),
                        IsDirectory = true,
                        Size = 0,
                        LastModified = dirInfo.LastWriteTime,
                        LastAccessed = dirInfo.LastAccessTime,
                        CreationTime = dirInfo.CreationTime,
                        Attributes = dirInfo.Attributes
                    });
                }
                
                // Get files
                foreach (var file in Directory.GetFiles(normalizedPath))
                {
                    var fileInfo = new FileInfo(file);
                    
                    // Skip hidden files if not including hidden
                    if (!includeHidden && (fileInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                    {
                        continue;
                    }
                    
                    // Skip metadata file
                    if (fileInfo.Name == sharedFolder.MetadataFileName)
                    {
                        continue;
                    }
                    
                    entries.Add(new FileSystemEntry
                    {
                        Name = fileInfo.Name,
                        Path = GetRelativePath(file, sharedFolder.HostPath),
                        IsDirectory = false,
                        Size = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTime,
                        LastAccessed = fileInfo.LastAccessTime,
                        CreationTime = fileInfo.CreationTime,
                        Attributes = fileInfo.Attributes
                    });
                }
                
                // Update access info
                _metadataManager.UpdateAccessInfo(normalizedPath, username, FileOperationType.List, sharedFolder);
                _sharedFolderManager.UpdateLastAccessed(shareId);
                
                // Log access
                _security.LogAccess(username, relativePath, FileOperationType.List, true);
                
                return entries;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not UnauthorizedAccessException && ex is not DirectoryNotFoundException)
            {
                _logger.LogError("FileSystemOperations", $"Error listing directory {relativePath}: {ex.Message}", ex);
                _security.LogAccess(username, relativePath, FileOperationType.List, false);
                throw;
            }
        }

        /// <summary>
        /// Creates a directory in a shared folder
        /// </summary>
        /// <param name="relativePath">Relative path within the shared folder</param>
        /// <param name="shareId">ID of the shared folder</param>
        /// <param name="username">User performing the operation</param>
        /// <returns>True if operation was successful</returns>
        public bool CreateDirectory(
            string relativePath, 
            string shareId, 
            string username = "anonymous")
        {
            var sharedFolder = _sharedFolderManager.GetSharedFolder(shareId);
            if (sharedFolder == null)
            {
                _security.LogAccess(username, relativePath, FileOperationType.Create, false);
                throw new ArgumentException($"Shared folder with ID {shareId} not found");
            }

            // Validate the path
            if (!_security.ValidatePath(relativePath, sharedFolder, out var normalizedPath))
            {
                _security.LogAccess(username, relativePath, FileOperationType.Create, false);
                throw new ArgumentException($"Invalid path: {relativePath}");
            }

            // Check directory name validity
            var dirName = Path.GetFileName(relativePath);
            if (!_security.IsValidFileName(dirName))
            {
                _security.LogAccess(username, relativePath, FileOperationType.Create, false);
                throw new ArgumentException($"Invalid directory name: {dirName}");
            }

            // Check if operation is allowed
            if (!_security.IsOperationAllowed(FileOperationType.Create, normalizedPath, sharedFolder))
            {
                _security.LogAccess(username, relativePath, FileOperationType.Create, false);
                throw new UnauthorizedAccessException($"Create operation not allowed on {relativePath}");
            }

            try
            {
                // Check if directory already exists
                if (Directory.Exists(normalizedPath))
                {
                    _security.LogAccess(username, relativePath, FileOperationType.Create, true);
                    return true;
                }

                // Create directory
                Directory.CreateDirectory(normalizedPath);
                
                // Update access info
                _metadataManager.UpdateAccessInfo(normalizedPath, username, FileOperationType.Create, sharedFolder);
                _sharedFolderManager.UpdateLastAccessed(shareId);
                
                // Log access
                _security.LogAccess(username, relativePath, FileOperationType.Create, true);
                
                return true;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not UnauthorizedAccessException)
            {
                _logger.LogError("FileSystemOperations", $"Error creating directory {relativePath}: {ex.Message}", ex);
                _security.LogAccess(username, relativePath, FileOperationType.Create, false);
                throw;
            }
        }

        #endregion

        #region Advanced Operations

        /// <summary>
        /// Copies a file from source to destination within shared folders
        /// </summary>
        /// <param name="sourcePath">Source relative path</param>
        /// <param name="sourceShareId">Source shared folder ID</param>
        /// <param name="destinationPath">Destination relative path</param>
        /// <param name="destinationShareId">Destination shared folder ID</param>
        /// <param name="username">User performing the operation</param>
        /// <param name="overwrite">Whether to overwrite if destination exists</param>
        /// <returns>True if operation was successful</returns>
        public bool CopyFile(
            string sourcePath,
            string sourceShareId,
            string destinationPath,
            string destinationShareId,
            string username = "anonymous",
            bool overwrite = false)
        {
            // Get shared folders
            var sourceSharedFolder = _sharedFolderManager.GetSharedFolder(sourceShareId);
            if (sourceSharedFolder == null)
            {
                _security.LogAccess(username, sourcePath, FileOperationType.Copy, false);
                throw new ArgumentException($"Source shared folder with ID {sourceShareId} not found");
            }
            
            var destSharedFolder = _sharedFolderManager.GetSharedFolder(destinationShareId);
            if (destSharedFolder == null)
            {
                _security.LogAccess(username, destinationPath, FileOperationType.Copy, false);
                throw new ArgumentException($"Destination shared folder with ID {destinationShareId} not found");
            }

            // Validate the paths
            if (!_security.ValidatePath(sourcePath, sourceSharedFolder, out var normalizedSourcePath))
            {
                _security.LogAccess(username, sourcePath, FileOperationType.Copy, false);
                throw new ArgumentException($"Invalid source path: {sourcePath}");
            }
            
            if (!_security.ValidatePath(destinationPath, destSharedFolder, out var normalizedDestPath))
            {
                _security.LogAccess(username, destinationPath, FileOperationType.Copy, false);
                throw new ArgumentException($"Invalid destination path: {destinationPath}");
            }

            // Check file name validity
            var fileName = Path.GetFileName(destinationPath);
            if (!_security.IsValidFileName(fileName))
            {
                _security.LogAccess(username, destinationPath, FileOperationType.Copy, false);
                throw new ArgumentException($"Invalid destination file name: {fileName}");
            }

            // Check if operations are allowed
            if (!_security.IsOperationAllowed(FileOperationType.Read, normalizedSourcePath, sourceSharedFolder))
            {
                _security.LogAccess(username, sourcePath, FileOperationType.Copy, false);
                throw new UnauthorizedAccessException($"Read operation not allowed on source {sourcePath}");
            }
            
            if (!_security.IsOperationAllowed(FileOperationType.Write, normalizedDestPath, destSharedFolder))
            {
                _security.LogAccess(username, destinationPath, FileOperationType.Copy, false);
                throw new UnauthorizedAccessException($"Write operation not allowed on destination {destinationPath}");
            }

            try
            {
                // Check if source exists
                if (!File.Exists(normalizedSourcePath))
                {
                    _security.LogAccess(username, sourcePath, FileOperationType.Copy, false);
                    throw new FileNotFoundException($"Source file not found: {sourcePath}");
                }

                // Check if destination exists
                if (File.Exists(normalizedDestPath) && !overwrite)
                {
                    _security.LogAccess(username, destinationPath, FileOperationType.Copy, false);
                    throw new IOException($"Destination file already exists: {destinationPath}");
                }

                // Create destination directory if it doesn't exist
                var destDir = Path.GetDirectoryName(normalizedDestPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                // Copy file
                File.Copy(normalizedSourcePath, normalizedDestPath, overwrite);
                
                // Update access info
                _metadataManager.UpdateAccessInfo(normalizedSourcePath, username, FileOperationType.Read, sourceSharedFolder);
                _metadataManager.UpdateAccessInfo(normalizedDestPath, username, FileOperationType.Write, destSharedFolder);
                _sharedFolderManager.UpdateLastAccessed(sourceShareId);
                _sharedFolderManager.UpdateLastAccessed(destinationShareId);
                
                // Log access
                _security.LogAccess(username, sourcePath, FileOperationType.Read, true);
                _security.LogAccess(username, destinationPath, FileOperationType.Write, true);
                
                return true;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not UnauthorizedAccessException && 
                                      ex is not FileNotFoundException && ex is not IOException)
            {
                _logger.LogError("FileSystemOperations", 
                    $"Error copying file from {sourcePath} to {destinationPath}: {ex.Message}", ex);
                _security.LogAccess(username, sourcePath, FileOperationType.Copy, false);
                throw;
            }
        }

        /// <summary>
        /// Moves a file from source to destination within shared folders
        /// </summary>
        /// <param name="sourcePath">Source relative path</param>
        /// <param name="sourceShareId">Source shared folder ID</param>
        /// <param name="destinationPath">Destination relative path</param>
        /// <param name="destinationShareId">Destination shared folder ID</param>
        /// <param name="username">User performing the operation</param>
        /// <param name="overwrite">Whether to overwrite if destination exists</param>
        /// <returns>True if operation was successful</returns>
        public bool MoveFile(
            string sourcePath,
            string sourceShareId,
            string destinationPath,
            string destinationShareId,
            string username = "anonymous",
            bool overwrite = false)
        {
            // Get shared folders
            var sourceSharedFolder = _sharedFolderManager.GetSharedFolder(sourceShareId);
            if (sourceSharedFolder == null)
            {
                _security.LogAccess(username, sourcePath, FileOperationType.Move, false);
                throw new ArgumentException($"Source shared folder with ID {sourceShareId} not found");
            }
            
            var destSharedFolder = _sharedFolderManager.GetSharedFolder(destinationShareId);
            if (destSharedFolder == null)
            {
                _security.LogAccess(username, destinationPath, FileOperationType.Move, false);
                throw new ArgumentException($"Destination shared folder with ID {destinationShareId} not found");
            }

            // Validate the paths
            if (!_security.ValidatePath(sourcePath, sourceSharedFolder, out var normalizedSourcePath))
            {
                _security.LogAccess(username, sourcePath, FileOperationType.Move, false);
                throw new ArgumentException($"Invalid source path: {sourcePath}");
            }
            
            if (!_security.ValidatePath(destinationPath, destSharedFolder, out var normalizedDestPath))
            {
                _security.LogAccess(username, destinationPath, FileOperationType.Move, false);
                throw new ArgumentException($"Invalid destination path: {destinationPath}");
            }

            // Check file name validity
            var fileName = Path.GetFileName(destinationPath);
            if (!_security.IsValidFileName(fileName))
            {
                _security.LogAccess(username, destinationPath, FileOperationType.Move, false);
                throw new ArgumentException($"Invalid destination file name: {fileName}");
            }

            // Check if operations are allowed
            if (!_security.IsOperationAllowed(FileOperationType.Delete, normalizedSourcePath, sourceSharedFolder))
            {
                _security.LogAccess(username, sourcePath, FileOperationType.Move, false);
                throw new UnauthorizedAccessException($"Delete operation not allowed on source {sourcePath}");
            }
            
            if (!_security.IsOperationAllowed(FileOperationType.Write, normalizedDestPath, destSharedFolder))
            {
                _security.LogAccess(username, destinationPath, FileOperationType.Move, false);
                throw new UnauthorizedAccessException($"Write operation not allowed on destination {destinationPath}");
            }

            try
            {
                // Check if source exists
                if (!File.Exists(normalizedSourcePath))
                {
                    _security.LogAccess(username, sourcePath, FileOperationType.Move, false);
                    throw new FileNotFoundException($"Source file not found: {sourcePath}");
                }

                // Check if destination exists
                if (File.Exists(normalizedDestPath) && !overwrite)
                {
                    _security.LogAccess(username, destinationPath, FileOperationType.Move, false);
                    throw new IOException($"Destination file already exists: {destinationPath}");
                }

                // Create destination directory if it doesn't exist
                var destDir = Path.GetDirectoryName(normalizedDestPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                // Move file
                if (sourceShareId == destinationShareId)
                {
                    // Within same shared folder, we can use direct move
                    File.Move(normalizedSourcePath, normalizedDestPath, overwrite);
                }
                else
                {
                    // Across shared folders, copy then delete
                    File.Copy(normalizedSourcePath, normalizedDestPath, overwrite);
                    File.Delete(normalizedSourcePath);
                }
                
                // Update access info
                _metadataManager.UpdateAccessInfo(normalizedSourcePath, username, FileOperationType.Delete, sourceSharedFolder);
                _metadataManager.UpdateAccessInfo(normalizedDestPath, username, FileOperationType.Write, destSharedFolder);
                _sharedFolderManager.UpdateLastAccessed(sourceShareId);
                _sharedFolderManager.UpdateLastAccessed(destinationShareId);
                
                // Log access
                _security.LogAccess(username, sourcePath, FileOperationType.Delete, true);
                _security.LogAccess(username, destinationPath, FileOperationType.Write, true);
                
                return true;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not UnauthorizedAccessException && 
                                      ex is not FileNotFoundException && ex is not IOException)
            {
                _logger.LogError("FileSystemOperations", 
                    $"Error moving file from {sourcePath} to {destinationPath}: {ex.Message}", ex);
                _security.LogAccess(username, sourcePath, FileOperationType.Move, false);
                throw;
            }
        }

        /// <summary>
        /// Gets attributes for a file or directory in a shared folder
        /// </summary>
        /// <param name="relativePath">Relative path within the shared folder</param>
        /// <param name="shareId">ID of the shared folder</param>
        /// <param name="username">User performing the operation</param>
        /// <returns>File attributes</returns>
        public FileAttributes GetAttributes(
            string relativePath, 
            string shareId, 
            string username = "anonymous")
        {
            var sharedFolder = _sharedFolderManager.GetSharedFolder(shareId);
            if (sharedFolder == null)
            {
                _security.LogAccess(username, relativePath, FileOperationType.Read, false);
                throw new ArgumentException($"Shared folder with ID {shareId} not found");
            }

            // Validate the path
            if (!_security.ValidatePath(relativePath, sharedFolder, out var normalizedPath))
            {
                _security.LogAccess(username, relativePath, FileOperationType.Read, false);
                throw new ArgumentException($"Invalid path: {relativePath}");
            }

            // Check if operation is allowed
            if (!_security.IsOperationAllowed(FileOperationType.Read, normalizedPath, sharedFolder))
            {
                _security.LogAccess(username, relativePath, FileOperationType.Read, false);
                throw new UnauthorizedAccessException($"Read operation not allowed on {relativePath}");
            }

            try
            {
                // Check if path exists
                if (!File.Exists(normalizedPath) && !Directory.Exists(normalizedPath))
                {
                    _security.LogAccess(username, relativePath, FileOperationType.Read, false);
                    throw new FileNotFoundException($"Path not found: {relativePath}");
                }

                // Get attributes
                var attributes = File.GetAttributes(normalizedPath);
                
                // Update access info
                _metadataManager.UpdateAccessInfo(normalizedPath, username, FileOperationType.Read, sharedFolder);
                _sharedFolderManager.UpdateLastAccessed(shareId);
                
                // Log access
                _security.LogAccess(username, relativePath, FileOperationType.Read, true);
                
                return attributes;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not UnauthorizedAccessException && 
                                      ex is not FileNotFoundException)
            {
                _logger.LogError("FileSystemOperations", 
                    $"Error getting attributes for {relativePath}: {ex.Message}", ex);
                _security.LogAccess(username, relativePath, FileOperationType.Read, false);
                throw;
            }
        }

        #endregion

        #region Virtual Path Operations

        /// <summary>
        /// Reads a file using a virtual path
        /// </summary>
        /// <param name="virtualPath">Virtual path to the file</param>
        /// <param name="username">User performing the operation</param>
        /// <returns>File content as byte array</returns>
        public async Task<byte[]> ReadFileByVirtualPathAsync(string virtualPath, string username = "anonymous")
        {
            // Resolve the virtual path
            var resolved = ResolveVirtualPath(virtualPath, username);
            if (resolved == null)
            {
                throw new FileNotFoundException($"File not found: {virtualPath}");
            }

            var (hostPath, sharedFolder, mountPoint) = resolved.Value;
            
            // Check if operation is allowed based on mount options
            var effectivePermission = mountPoint.Options.GetEffectivePermission(sharedFolder.Permission);
            if (effectivePermission == SharedFolderPermission.ReadOnly || 
                effectivePermission == SharedFolderPermission.ReadWrite)
            {
                // Read is allowed with either permission level
                try
                {
                    // Check if file exists
                    if (!File.Exists(hostPath))
                    {
                        _security.LogAccess(username, virtualPath, FileOperationType.Read, false);
                        throw new FileNotFoundException($"File not found: {virtualPath}");
                    }
                    
                    // Read file content
                    var content = await File.ReadAllBytesAsync(hostPath);
                    
                    // Update access info if tracking is enabled
                    if (mountPoint.Options.TrackAccess)
                    {
                        _metadataManager.UpdateAccessInfo(hostPath, username, FileOperationType.Read, sharedFolder);
                    }
                    
                    // Update last accessed times
                    _mountPointManager.UpdateLastAccessed(mountPoint.Id);
                    
                    // Log access
                    _security.LogAccess(username, virtualPath, FileOperationType.Read, true);
                    
                    return content;
                }
                catch (Exception ex) when (ex is not ArgumentException && ex is not UnauthorizedAccessException && 
                                          ex is not FileNotFoundException)
                {
                    _logger.LogError("FileSystemOperations", $"Error reading file {virtualPath}: {ex.Message}", ex);
                    _security.LogAccess(username, virtualPath, FileOperationType.Read, false);
                    throw;
                }
            }
            else
            {
                _security.LogAccess(username, virtualPath, FileOperationType.Read, false);
                throw new UnauthorizedAccessException($"Read operation not allowed on {virtualPath}");
            }
        }

        /// <summary>
        /// Writes content to a file using a virtual path
        /// </summary>
        /// <param name="virtualPath">Virtual path to the file</param>
        /// <param name="content">Content to write</param>
        /// <param name="username">User performing the operation</param>
        /// <param name="overwrite">Whether to overwrite if file exists</param>
        /// <returns>True if operation was successful</returns>
        public async Task<bool> WriteFileByVirtualPathAsync(
            string virtualPath, 
            byte[] content, 
            string username = "anonymous",
            bool overwrite = true)
        {
            // Resolve the virtual path
            var resolved = ResolveVirtualPath(virtualPath, username);
            if (resolved == null)
            {
                // Special case: if the file doesn't exist yet, we need to resolve the parent directory
                var parentPath = Path.GetDirectoryName(virtualPath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(parentPath))
                {
                    _security.LogAccess(username, virtualPath, FileOperationType.Write, false);
                    throw new DirectoryNotFoundException($"Parent directory not found for: {virtualPath}");
                }
                
                var parentResolved = ResolveVirtualPath(parentPath, username);
                if (parentResolved == null)
                {
                    _security.LogAccess(username, virtualPath, FileOperationType.Write, false);
                    throw new DirectoryNotFoundException($"Parent directory not found: {parentPath}");
                }
                
                var (parentHostPath, sharedFolder, mountPoint) = parentResolved.Value;
                
                // If parent directory exists, construct the full path
                if (!Directory.Exists(parentHostPath))
                {
                    _security.LogAccess(username, virtualPath, FileOperationType.Write, false);
                    throw new DirectoryNotFoundException($"Parent directory not found: {parentPath}");
                }
                
                var fileName = Path.GetFileName(virtualPath);
                var hostPath = Path.Combine(parentHostPath, fileName);
                
                // Check file name validity
                if (!_security.IsValidFileName(fileName))
                {
                    _security.LogAccess(username, virtualPath, FileOperationType.Write, false);
                    throw new ArgumentException($"Invalid file name: {fileName}");
                }
                
                // Check if operation is allowed based on mount options
                var effectivePermission = mountPoint.Options.GetEffectivePermission(sharedFolder.Permission);
                if (effectivePermission != SharedFolderPermission.ReadWrite)
                {
                    _security.LogAccess(username, virtualPath, FileOperationType.Write, false);
                    throw new UnauthorizedAccessException($"Write operation not allowed on {virtualPath}");
                }
                
                try
                {
                    // Write file content
                    await File.WriteAllBytesAsync(hostPath, content);
                    
                    // Update access info if tracking is enabled
                    if (mountPoint.Options.TrackAccess)
                    {
                        _metadataManager.UpdateAccessInfo(hostPath, username, FileOperationType.Write, sharedFolder);
                    }
                    
                    // Update last accessed times
                    _mountPointManager.UpdateLastAccessed(mountPoint.Id);
                    
                    // Log access
                    _security.LogAccess(username, virtualPath, FileOperationType.Write, true);
                    
                    return true;
                }
                catch (Exception ex) when (ex is not ArgumentException && ex is not UnauthorizedAccessException)
                {
                    _logger.LogError("FileSystemOperations", $"Error writing file {virtualPath}: {ex.Message}", ex);
                    _security.LogAccess(username, virtualPath, FileOperationType.Write, false);
                    throw;
                }
            }
            else
            {
                var (hostPath, sharedFolder, mountPoint) = resolved.Value;
                
                // Check if operation is allowed based on mount options
                var effectivePermission = mountPoint.Options.GetEffectivePermission(sharedFolder.Permission);
                if (effectivePermission == SharedFolderPermission.ReadWrite)
                {
                    try
                    {
                        // Check if file exists
                        bool fileExists = File.Exists(hostPath);
                        if (fileExists && !overwrite)
                        {
                            _security.LogAccess(username, virtualPath, FileOperationType.Write, false);
                            throw new IOException($"File already exists: {virtualPath}");
                        }
                        
                        // Write file content
                        await File.WriteAllBytesAsync(hostPath, content);
                        
                        // Update access info if tracking is enabled
                        if (mountPoint.Options.TrackAccess)
                        {
                            _metadataManager.UpdateAccessInfo(hostPath, username, FileOperationType.Write, sharedFolder);
                        }
                        
                        // Update last accessed times
                        _mountPointManager.UpdateLastAccessed(mountPoint.Id);
                        
                        // Log access
                        _security.LogAccess(username, virtualPath, FileOperationType.Write, true);
                        
                        return true;
                    }
                    catch (Exception ex) when (ex is not ArgumentException && ex is not UnauthorizedAccessException && 
                                              ex is not IOException)
                    {
                        _logger.LogError("FileSystemOperations", $"Error writing file {virtualPath}: {ex.Message}", ex);
                        _security.LogAccess(username, virtualPath, FileOperationType.Write, false);
                        throw;
                    }
                }
                else
                {
                    _security.LogAccess(username, virtualPath, FileOperationType.Write, false);
                    throw new UnauthorizedAccessException($"Write operation not allowed on {virtualPath}");
                }
            }
        }
        
        /// <summary>
        /// Lists directory contents using a virtual path
        /// </summary>
        /// <param name="virtualPath">Virtual path to the directory</param>
        /// <param name="username">User performing the operation</param>
        /// <returns>List of file system entries</returns>
        public IEnumerable<FileSystemEntry> ListDirectoryByVirtualPath(string virtualPath, string username = "anonymous")
        {
            // Resolve the virtual path
            var resolved = ResolveVirtualPath(virtualPath, username);
            if (resolved == null)
            {
                _security.LogAccess(username, virtualPath, FileOperationType.Read, false);
                throw new DirectoryNotFoundException($"Directory not found: {virtualPath}");
            }
            
            var (hostPath, sharedFolder, mountPoint) = resolved.Value;
            
            // Check if path is a directory
            if (!Directory.Exists(hostPath))
            {
                _security.LogAccess(username, virtualPath, FileOperationType.Read, false);
                throw new DirectoryNotFoundException($"Path is not a directory: {virtualPath}");
            }
            
            try
            {
                // Get directory contents
                var entries = new List<FileSystemEntry>();
                
                foreach (var directory in Directory.GetDirectories(hostPath))
                {
                    var dirInfo = new DirectoryInfo(directory);
                    
                    // Skip hidden and system directories if not allowed
                    if ((dirInfo.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    {
                        continue;
                    }
                    
                    // Get the virtual path for this entry
                    var entryVirtualPath = Path.Combine(virtualPath, dirInfo.Name).Replace('\\', '/');
                    
                    entries.Add(new FileSystemEntry
                    {
                        Name = dirInfo.Name,
                        Path = dirInfo.FullName,
                        VirtualPath = entryVirtualPath,
                        IsDirectory = true,
                        Size = 0,
                        LastModified = dirInfo.LastWriteTime,
                        LastAccessed = dirInfo.LastAccessTime,
                        CreationTime = dirInfo.CreationTime,
                        Attributes = dirInfo.Attributes,
                        MountPointId = mountPoint.Id,
                        SharedFolderId = sharedFolder.Id
                    });
                }
                
                foreach (var file in Directory.GetFiles(hostPath))
                {
                    var fileInfo = new FileInfo(file);
                    
                    // Skip hidden and system files if not allowed
                    if ((fileInfo.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    {
                        continue;
                    }
                    
                    // Get the virtual path for this entry
                    var entryVirtualPath = Path.Combine(virtualPath, fileInfo.Name).Replace('\\', '/');
                    
                    entries.Add(new FileSystemEntry
                    {
                        Name = fileInfo.Name,
                        Path = fileInfo.FullName,
                        VirtualPath = entryVirtualPath,
                        IsDirectory = false,
                        Size = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTime,
                        LastAccessed = fileInfo.LastAccessTime,
                        CreationTime = fileInfo.CreationTime,
                        Attributes = fileInfo.Attributes,
                        MountPointId = mountPoint.Id,
                        SharedFolderId = sharedFolder.Id
                    });
                }
                
                // Update access info if tracking is enabled
                if (mountPoint.Options.TrackAccess)
                {
                    _metadataManager.UpdateAccessInfo(hostPath, username, FileOperationType.Read, sharedFolder);
                }
                
                // Update last accessed times
                _mountPointManager.UpdateLastAccessed(mountPoint.Id);
                
                // Log access
                _security.LogAccess(username, virtualPath, FileOperationType.Read, true);
                
                return entries;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not UnauthorizedAccessException && 
                                      ex is not DirectoryNotFoundException)
            {
                _logger.LogError("FileSystemOperations", $"Error listing directory {virtualPath}: {ex.Message}", ex);
                _security.LogAccess(username, virtualPath, FileOperationType.Read, false);
                throw;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets a path relative to a base path
        /// </summary>
        private string GetRelativePath(string fullPath, string basePath)
        {
            // Normalize paths
            fullPath = Path.GetFullPath(fullPath);
            basePath = Path.GetFullPath(basePath);
            
            if (!fullPath.StartsWith(basePath))
            {
                return fullPath;
            }
            
            var relativePath = fullPath.Substring(basePath.Length);
            return relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        #endregion
    }
}
