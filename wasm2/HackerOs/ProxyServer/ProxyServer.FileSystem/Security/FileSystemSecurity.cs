using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ProxyServer.FileSystem.Models;

namespace ProxyServer.FileSystem.Security
{
    /// <summary>
    /// Type of file system operation being performed
    /// </summary>
    public enum FileOperationType
    {
        Read,
        Write,
        Delete,
        Create,
        List,
        Move,
        Copy
    }

    /// <summary>
    /// Handles security validation for file system operations
    /// </summary>
    public class FileSystemSecurity
    {
        private readonly ILogger _logger;
        
        public FileSystemSecurity(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validates if a requested path is within the boundaries of a shared folder
        /// </summary>
        /// <param name="requestedPath">Path to validate</param>
        /// <param name="sharedFolder">Shared folder that should contain the path</param>
        /// <param name="normalizedPath">Returns the normalized absolute path if valid</param>
        /// <returns>True if the path is valid and within the shared folder</returns>
        public bool ValidatePath(string requestedPath, SharedFolderInfo sharedFolder, out string normalizedPath)
        {
            normalizedPath = string.Empty;
            
            if (string.IsNullOrEmpty(requestedPath))
            {
                _logger.LogWarning("FileSystemSecurity", "Empty path requested");
                return false;
            }

            if (sharedFolder == null || !sharedFolder.Exists)
            {
                _logger.LogWarning("FileSystemSecurity", $"Invalid or non-existent shared folder for path: {requestedPath}");
                return false;
            }

            try
            {
                // Normalize the path to handle ".." and "." elements
                var combinedPath = Path.GetFullPath(Path.Combine(sharedFolder.HostPath, requestedPath));
                
                // Check if the path is within the shared folder (prevent path traversal)
                if (!combinedPath.StartsWith(Path.GetFullPath(sharedFolder.HostPath)))
                {
                    _logger.LogWarning("FileSystemSecurity", $"Path traversal attempt detected: {requestedPath}");
                    return false;
                }
                
                normalizedPath = combinedPath;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("FileSystemSecurity", $"Error validating path: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Validates if a file operation is allowed based on the shared folder permissions
        /// </summary>
        /// <param name="operation">The type of operation being performed</param>
        /// <param name="path">The path of the file/directory</param>
        /// <param name="sharedFolder">The shared folder containing the path</param>
        /// <returns>True if the operation is allowed</returns>
        public bool IsOperationAllowed(FileOperationType operation, string path, SharedFolderInfo sharedFolder)
        {
            // If the shared folder doesn't exist, no operations are allowed
            if (sharedFolder == null || !sharedFolder.Exists)
            {
                _logger.LogWarning("FileSystemSecurity", $"Operation not allowed - Invalid shared folder for path: {path}");
                return false;
            }

            // If it's a read-only share, only read operations are allowed
            if (sharedFolder.Permission == SharedFolderPermission.ReadOnly)
            {
                if (operation != FileOperationType.Read && 
                    operation != FileOperationType.List)
                {
                    _logger.LogWarning("FileSystemSecurity", 
                        $"Write operation not allowed on read-only shared folder: {operation} on {path}");
                    return false;
                }
            }

            // Check file extensions if specified
            if (Path.HasExtension(path))
            {
                var extension = Path.GetExtension(path).ToLowerInvariant();

                // Check blocked extensions
                if (sharedFolder.BlockedExtensions != null && 
                    sharedFolder.BlockedExtensions.Any(ext => ext.ToLowerInvariant() == extension))
                {
                    _logger.LogWarning("FileSystemSecurity", $"File extension blocked: {extension}");
                    return false;
                }

                // Check allowed extensions (if defined, only allow those in the list)
                if (sharedFolder.AllowedExtensions != null && 
                    sharedFolder.AllowedExtensions.Count > 0 && 
                    !sharedFolder.AllowedExtensions.Any(ext => ext.ToLowerInvariant() == extension))
                {
                    _logger.LogWarning("FileSystemSecurity", $"File extension not allowed: {extension}");
                    return false;
                }
            }

            // Check for hidden metadata file
            if (Path.GetFileName(path) == sharedFolder.MetadataFileName)
            {
                _logger.LogWarning("FileSystemSecurity", 
                    $"Direct access to metadata file not allowed: {path}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Logs an access attempt to a file or directory
        /// </summary>
        /// <param name="user">The user attempting access</param>
        /// <param name="path">The path being accessed</param>
        /// <param name="operation">The operation being performed</param>
        /// <param name="success">Whether the access was successful</param>
        public void LogAccess(string user, string path, FileOperationType operation, bool success)
        {
            var logLevel = success ? LogLevel.Information : LogLevel.Warning;
            var result = success ? "succeeded" : "failed";
            
            _logger.Log(logLevel, "FileSystemSecurity", 
                $"File access {result}: User '{user}' {operation} on '{path}'");
        }

        /// <summary>
        /// Checks if a file extension is valid based on the shared folder configuration
        /// </summary>
        /// <param name="fileName">The file name to check</param>
        /// <param name="sharedFolder">The shared folder configuration</param>
        /// <returns>True if the extension is valid</returns>
        public bool IsValidFileExtension(string fileName, SharedFolderInfo sharedFolder)
        {
            if (!Path.HasExtension(fileName))
                return true;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            // Check blocked extensions
            if (sharedFolder.BlockedExtensions != null && 
                sharedFolder.BlockedExtensions.Any(ext => ext.ToLowerInvariant() == extension))
            {
                return false;
            }

            // Check allowed extensions (if defined, only allow those in the list)
            if (sharedFolder.AllowedExtensions != null && 
                sharedFolder.AllowedExtensions.Count > 0 && 
                !sharedFolder.AllowedExtensions.Any(ext => ext.ToLowerInvariant() == extension))
            {
                return false;
            }

            return true;
        }        /// <summary>
        /// Checks for potentially dangerous file names
        /// </summary>
        /// <param name="fileName">The file name to validate</param>
        /// <returns>True if the file name is safe</returns>
        public bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            // Check for invalid characters
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            // Check for potential directory traversal
            if (fileName.Contains(".."))
                return false;
            
            // Check for absolute paths
            if (Path.IsPathRooted(fileName))
                return false;

            // Check for potential command injection patterns
            var dangerousPatterns = new[] { 
                "&", "|", ";", "$", "%", "@", "!", "#", "(", ")",
                "<", ">", "?", "*", "\"", "'", "`", "\\"
            };
            
            if (dangerousPatterns.Any(pattern => fileName.Contains(pattern)))
                return false;

            return true;
        }
        
        /// <summary>
        /// Validates if an operation is allowed based on mount point permissions and options
        /// </summary>
        /// <param name="operation">The type of operation being performed</param>
        /// <param name="mountPoint">The mount point through which the file system is accessed</param>
        /// <param name="sharedFolder">The shared folder containing the path</param>
        /// <returns>True if the operation is allowed</returns>
        public bool IsOperationAllowed(FileOperationType operation, MountPoint mountPoint, SharedFolderInfo sharedFolder)
        {
            // If the shared folder or mount point doesn't exist, no operations are allowed
            if (sharedFolder == null || !sharedFolder.Exists || mountPoint == null || !mountPoint.IsActive)
            {
                _logger.LogWarning("FileSystemSecurity", "Operation not allowed - Invalid mount point or shared folder");
                return false;
            }

            // Get effective permission based on mount options and shared folder
            var effectivePermission = mountPoint.Options.GetEffectivePermission(sharedFolder.Permission);
            
            // If it's a read-only mount point, only read operations are allowed
            if (effectivePermission == SharedFolderPermission.ReadOnly)
            {
                if (operation != FileOperationType.Read && 
                    operation != FileOperationType.List)
                {
                    _logger.LogWarning("FileSystemSecurity", 
                        $"Write operation not allowed on read-only mount point: {operation} on {mountPoint.VirtualPath}");
                    return false;
                }
            }

            return true;
        }
        
        /// <summary>
        /// Validates if a virtual path format is valid (not checking existence)
        /// </summary>
        /// <param name="virtualPath">Virtual path to validate</param>
        /// <returns>True if the path format is valid</returns>
        public bool IsValidVirtualPath(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath))
            {
                _logger.LogWarning("FileSystemSecurity", "Empty virtual path requested");
                return false;
            }
            
            try
            {
                // Virtual paths should start with /
                if (!virtualPath.StartsWith("/"))
                {
                    _logger.LogWarning("FileSystemSecurity", $"Invalid virtual path format (should start with /): {virtualPath}");
                    return false;
                }
                
                // Check for path traversal in virtual path
                if (virtualPath.Contains(".."))
                {
                    _logger.LogWarning("FileSystemSecurity", $"Path traversal attempt detected in virtual path: {virtualPath}");
                    return false;
                }
                
                // Check for invalid characters
                var invalidChars = new[] { '*', '?', '"', '<', '>', '|' };
                if (virtualPath.IndexOfAny(invalidChars) >= 0)
                {
                    _logger.LogWarning("FileSystemSecurity", $"Invalid characters in virtual path: {virtualPath}");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("FileSystemSecurity", $"Error validating virtual path: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Normalizes a virtual path
        /// </summary>
        /// <param name="virtualPath">The virtual path to normalize</param>
        /// <returns>The normalized path</returns>
        public string NormalizeVirtualPath(string virtualPath)
        {
            // Replace backslashes with forward slashes
            virtualPath = virtualPath.Replace('\\', '/');
            
            // Ensure path starts with /
            if (!virtualPath.StartsWith("/"))
            {
                virtualPath = "/" + virtualPath;
            }
            
            // Remove trailing slash if present (unless root path)
            if (virtualPath.Length > 1 && virtualPath.EndsWith("/"))
            {
                virtualPath = virtualPath.TrimEnd('/');
            }
            
            return virtualPath;
        }
    }
}
