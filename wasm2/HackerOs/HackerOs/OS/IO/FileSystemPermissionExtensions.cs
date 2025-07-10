using System;
using System.Threading.Tasks;
using System.Globalization;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;

namespace HackerOs.OS.IO
{
    /// <summary>
    /// Extension methods for IVirtualFileSystem to provide Unix-like permission operations
    /// </summary>
    public static class FileSystemPermissionExtensions
    {
        /// <summary>
        /// Sets the permissions, owner, and group for a file or directory
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="mode">The permissions in octal format (e.g., 0755, 0644)</param>
        /// <param name="uid">The user ID of the owner</param>
        /// <param name="gid">The group ID</param>
        /// <param name="user">The user performing the operation (requires appropriate permissions)</param>
        /// <returns>True if permissions were set successfully, false otherwise</returns>
        public static async Task<bool> SetPermissionsAsync(
            this IVirtualFileSystem fileSystem, 
            string path, 
            int mode, 
            int uid, 
            int gid, 
            User.User user)
        {
            // Ensure the user has permission to change ownership/permissions
            // Only root or the owner can change permissions
            var node = await fileSystem.GetNodeAsync(path, user);
            if (node == null)
            {
                return false;
            }

            // Only root or the owner can change permissions
            if (user.UserId != 0 && node.Owner != user.Username)
            {
                return false;
            }

            try
            {
                // Convert octal mode to FilePermissions object
                var permissions = new FilePermissions(mode);
                node.Permissions = permissions;

                // Set ownership if the user is root
                if (user.UserId == 0)
                {
                    // Get user and group names from IDs
                    string ownerName = UserModelExtensions.GetGroupName(uid);
                    string groupName = UserModelExtensions.GetGroupName(gid);
                    
                    node.Owner = ownerName;
                    node.Group = groupName;
                }

                node.ModifiedAt = DateTime.UtcNow;
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a user has specific permissions for a file or directory
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="user">The user to check permissions for</param>
        /// <param name="requireRead">Whether read permission is required</param>
        /// <param name="requireWrite">Whether write permission is required</param>
        /// <param name="requireExecute">Whether execute permission is required</param>
        /// <returns>True if the user has the required permissions, false otherwise</returns>
        public static async Task<bool> CheckPermissionsAsync(
            this IVirtualFileSystem fileSystem,
            string path,
            User.User user,
            bool requireRead = false,
            bool requireWrite = false,
            bool requireExecute = false)
        {
            // Root always has all permissions
            if (user.UserId == 0)
            {
                return true;
            }

            var node = await fileSystem.GetNodeAsync(path);
            if (node == null)
            {
                return false;
            }

            // Check if user is the owner
            bool isOwner = node.Owner == user.Username;
            
            // Check if user is in the file's group
            bool isInGroup = node.Group == user.Username || user.BelongsToGroup(UserModelExtensions.GetGroupId(node.Group));

            var permissions = node.Permissions;

            // Check permissions based on user relationship to the file
            if (isOwner)
            {
                if (requireRead && !permissions.OwnerRead) return false;
                if (requireWrite && !permissions.OwnerWrite) return false;
                if (requireExecute && !permissions.OwnerExecute) return false;
            }
            else if (isInGroup)
            {
                if (requireRead && !permissions.GroupRead) return false;
                if (requireWrite && !permissions.GroupWrite) return false;
                if (requireExecute && !permissions.GroupExecute) return false;
            }
            else
            {
                if (requireRead && !permissions.OthersRead) return false;
                if (requireWrite && !permissions.OthersWrite) return false;
                if (requireExecute && !permissions.OthersExecute) return false;
            }

            return true;
        }

        /// <summary>
        /// Copies a file from source to destination
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="sourcePath">Source file path</param>
        /// <param name="destinationPath">Destination file path</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>True if successful, false otherwise</returns>
        public static async Task<bool> CopyFileAsync(
            this IVirtualFileSystem fileSystem,
            string sourcePath,
            string destinationPath,
            User.User user)
        {
            try
            {
                // Check if source exists and user has read permissions
                if (!await fileSystem.ExistsAsync(sourcePath) || 
                    !await CheckPermissionsAsync(fileSystem, sourcePath, user, requireRead: true))
                {
                    return false;
                }

                // Check parent directory write permissions for destination
                string parentDir = HSystem.IO.HPath.GetDirectoryName(destinationPath)?.Replace('\\', '/') ?? "/";
                if (!await CheckPermissionsAsync(fileSystem, parentDir, user, requireWrite: true, requireExecute: true))
                {
                    return false;
                }

                // Use built-in copy functionality
                bool result = await fileSystem.CopyAsync(sourcePath, destinationPath);
                
                if (result)
                {
                    // Ensure correct permissions
                    var destNode = await fileSystem.GetNodeAsync(destinationPath);
                    if (destNode != null && user.UserId != 0)
                    {
                        // Set owner to current user if not root
                        destNode.Owner = user.Username;
                        destNode.Group = UserModelExtensions.GetGroupName(user.PrimaryGroupId);
                    }
                }
                
                return result;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Moves or renames a file 
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="sourcePath">Source file path</param>
        /// <param name="destinationPath">Destination file path</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>True if successful, false otherwise</returns>
        public static async Task<bool> MoveFileAsync(
            this IVirtualFileSystem fileSystem,
            string sourcePath,
            string destinationPath,
            User.User user)
        {
            try
            {
                // Check permissions for source and destination
                if (!await CheckPermissionsAsync(fileSystem, sourcePath, user, requireRead: true, requireWrite: true))
                {
                    return false;
                }
                
                // Check parent directory permissions for destination
                string parentDir = HSystem.IO.HPath.GetDirectoryName(destinationPath)?.Replace('\\', '/') ?? "/";
                if (!await CheckPermissionsAsync(fileSystem, parentDir, user, requireWrite: true, requireExecute: true))
                {
                    return false;
                }
                
                // Use built-in move functionality
                return await fileSystem.MoveAsync(sourcePath, destinationPath);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a file or directory has the SetUID bit set
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path to check</param>
        /// <returns>True if the SetUID bit is set; otherwise, false</returns>
        public static async Task<bool> IsSetUIDAsync(this IVirtualFileSystem fileSystem, string path)
        {
            var node = await fileSystem.GetNodeAsync(path);
            return node?.Permissions.IsSUID ?? false;
        }
        
        /// <summary>
        /// Sets or clears the SetUID bit on a file or directory
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path to modify</param>
        /// <param name="enable">Whether to enable or disable the SetUID bit</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> SetSetUIDAsync(
            this IVirtualFileSystem fileSystem, 
            string path, 
            bool enable, 
            User.User user)
        {
            // Only root or the owner can set special bits
            var node = await fileSystem.GetNodeAsync(path, user);
            if (node == null)
            {
                return false;
            }
            
            if (user.UserId != 0 && node.OwnerId != user.UserId)
            {
                return false;
            }
            
            try
            {
                var permissions = node.Permissions;
                permissions.IsSUID = enable;
                node.Permissions = permissions;
                node.ModifiedAt = DateTime.UtcNow;
                
                // Log the operation
                if (fileSystem is VirtualFileSystem vfs)
                {
                    vfs.LogFileSystemEvent(
                        FileSystemEventType.FileWritten,
                        path,
                        $"SetUID bit {(enable ? "set" : "cleared")} by {user.Username}");
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a file or directory has the SetGID bit set
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path to check</param>
        /// <returns>True if the SetGID bit is set; otherwise, false</returns>
        public static async Task<bool> IsSetGIDAsync(this IVirtualFileSystem fileSystem, string path)
        {
            var node = await fileSystem.GetNodeAsync(path);
            return node?.Permissions.IsSGID ?? false;
        }
        
        /// <summary>
        /// Sets or clears the SetGID bit on a file or directory
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path to modify</param>
        /// <param name="enable">Whether to enable or disable the SetGID bit</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> SetSetGIDAsync(
            this IVirtualFileSystem fileSystem, 
            string path, 
            bool enable, 
            User.User user)
        {
            // Only root or the owner can set special bits
            var node = await fileSystem.GetNodeAsync(path, user);
            if (node == null)
            {
                return false;
            }
            
            if (user.UserId != 0 && node.OwnerId != user.UserId)
            {
                return false;
            }
            
            try
            {
                var permissions = node.Permissions;
                permissions.IsSGID = enable;
                node.Permissions = permissions;
                node.ModifiedAt = DateTime.UtcNow;
                
                // Log the operation
                if (fileSystem is VirtualFileSystem vfs)
                {
                    vfs.LogFileSystemEvent(
                        FileSystemEventType.FileWritten,
                        path,
                        $"SetGID bit {(enable ? "set" : "cleared")} by {user.Username}");
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a directory has the sticky bit set
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path to check</param>
        /// <returns>True if the sticky bit is set; otherwise, false</returns>
        public static async Task<bool> IsStickyAsync(this IVirtualFileSystem fileSystem, string path)
        {
            var node = await fileSystem.GetNodeAsync(path);
            return node?.Permissions.IsSticky ?? false;
        }
        
        /// <summary>
        /// Sets or clears the sticky bit on a directory
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path to modify</param>
        /// <param name="enable">Whether to enable or disable the sticky bit</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> SetStickyAsync(
            this IVirtualFileSystem fileSystem, 
            string path, 
            bool enable, 
            User.User user)
        {
            // Only root or the owner can set special bits
            var node = await fileSystem.GetNodeAsync(path, user);
            if (node == null)
            {
                return false;
            }
            
            // Sticky bit only applies to directories
            if (!node.IsDirectory)
            {
                return false;
            }
            
            if (user.UserId != 0 && node.OwnerId != user.UserId)
            {
                return false;
            }
            
            try
            {
                var permissions = node.Permissions;
                permissions.IsSticky = enable;
                node.Permissions = permissions;
                node.ModifiedAt = DateTime.UtcNow;
                
                // Log the operation
                if (fileSystem is VirtualFileSystem vfs)
                {
                    vfs.LogFileSystemEvent(
                        FileSystemEventType.FileWritten,
                        path,
                        $"Sticky bit {(enable ? "set" : "cleared")} by {user.Username}");
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Changes permissions recursively on a directory and its contents
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path to the directory</param>
        /// <param name="mode">Permission mode in octal (e.g., 0755)</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="applyToFiles">Whether to apply to files</param>
        /// <param name="applyToDirectories">Whether to apply to directories</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> ChmodRecursiveAsync(
            this IVirtualFileSystem fileSystem,
            string path,
            int mode,
            User.User user,
            bool applyToFiles = true,
            bool applyToDirectories = true)
        {
            // Check if the path exists
            var node = await fileSystem.GetNodeAsync(path, user);
            if (node == null)
            {
                return false;
            }
            
            // Apply to this node if appropriate
            bool success = true;
            if ((node.IsDirectory && applyToDirectories) || (!node.IsDirectory && applyToFiles))
            {
                // Set permissions for this node
                var permissions = new FilePermissions(mode);
                
                // Only change if user has permission (root or owner)
                if (user.UserId == 0 || node.OwnerId == user.UserId)
                {
                    node.Permissions = permissions;
                }
                else
                {
                    success = false;
                }
            }
            
            // If it's a directory, recursively apply to contents
            if (node.IsDirectory)
            {
                var directory = (VirtualDirectory)node;
                
                foreach (var child in directory.Children.Values)
                {
                    // Recursively apply to each child
                    bool childSuccess = await ChmodRecursiveAsync(
                        fileSystem,
                        child.FullPath,
                        mode,
                        user,
                        applyToFiles,
                        applyToDirectories);
                    
                    // If any operation fails, track it, but continue processing
                    if (!childSuccess)
                    {
                        success = false;
                    }
                }
            }
            
            return success;
        }
        
        /// <summary>
        /// Makes a file executable by setting the appropriate execute bits
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path to the file</param>
        /// <param name="user">User performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> MakeExecutableAsync(
            this IVirtualFileSystem fileSystem,
            string path,
            User.User user)
        {
            var node = await fileSystem.GetNodeAsync(path, user);
            if (node == null || node.IsDirectory)
            {
                return false;
            }
            
            // Only owner or root can change permissions
            if (user.UserId != 0 && node.OwnerId != user.UserId)
            {
                return false;
            }
            
            try
            {
                var permissions = node.Permissions;
                
                // Add execute permissions where read permissions exist
                if (permissions.OwnerRead) permissions.OwnerExecute = true;
                if (permissions.GroupRead) permissions.GroupExecute = true;
                if (permissions.OthersRead) permissions.OthersExecute = true;
                
                node.Permissions = permissions;
                node.ModifiedAt = DateTime.UtcNow;
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Makes a file read-only by removing all write permissions
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path to the file</param>
        /// <param name="user">User performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> MakeReadOnlyAsync(
            this IVirtualFileSystem fileSystem,
            string path,
            User.User user)
        {
            var node = await fileSystem.GetNodeAsync(path, user);
            if (node == null)
            {
                return false;
            }
            
            // Only owner or root can change permissions
            if (user.UserId != 0 && node.OwnerId != user.UserId)
            {
                return false;
            }
            
            try
            {
                var permissions = node.Permissions;
                
                // Remove all write permissions
                permissions.OwnerWrite = false;
                permissions.GroupWrite = false;
                permissions.OthersWrite = false;
                
                node.Permissions = permissions;
                node.ModifiedAt = DateTime.UtcNow;
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Validates that file permissions are secure and do not introduce security risks
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="permissions">The permissions to validate</param>
        /// <param name="isNewFile">Whether this is for a new file or an existing one</param>
        /// <returns>Tuple of (bool isValid, string message) indicating validation results</returns>
        public static async Task<(bool isValid, string message)> ValidatePermissionsAsync(
            this IVirtualFileSystem fileSystem,
            string path,
            FilePermissions permissions,
            bool isNewFile = false)
        {
            try
            {
                // If the path doesn't exist and this isn't for a new file, validation fails
                var node = await fileSystem.GetNodeAsync(path);
                if (node == null && !isNewFile)
                {
                    return (false, $"Path '{path}' does not exist");
                }
                
                // Check for high-risk permission combinations
                
                // World-writable directories with execute permissions
                if (permissions.OthersWrite && permissions.OthersExecute && (node?.IsDirectory ?? isNewFile))
                {
                    return (false, "World-writable directories with execute permissions are a security risk");
                }
                
                // World-writable files with SetUID
                if (permissions.OthersWrite && permissions.IsSUID && !(node?.IsDirectory ?? isNewFile))
                {
                    return (false, "World-writable files with IsSUID are a severe security risk");
                }
                
                // World-writable files with SetGID
                if (permissions.OthersWrite && permissions.IsSGID && !(node?.IsDirectory ?? isNewFile))
                {
                    return (false, "World-writable files with IsSGID are a severe security risk");
                }
                
                // World-writable root-owned files
                if (permissions.OthersWrite && !isNewFile && node != null && node.OwnerId == 0)
                {
                    return (false, "World-writable root-owned files are a security risk");
                }
                
                // SetUID on shell scripts is dangerous
                if (permissions.IsSUID && !isNewFile && node != null && !node.IsDirectory)
                {
                    // Check if this is a script file
                    if (path.EndsWith(".sh") || path.EndsWith(".bash") || path.EndsWith(".py") || 
                        path.EndsWith(".pl") || path.EndsWith(".rb"))
                    {
                        return (false, "IsSUID on script files is extremely dangerous");
                    }
                }
                
                // Recommend more restrictive permissions for configuration files
                if (!isNewFile && node != null && !node.IsDirectory)
                {
                    bool isConfigFile = path.Contains("/etc/") || 
                                       path.Contains("config") || 
                                       path.Contains(".conf") || 
                                       path.EndsWith(".ini") ||
                                       path.EndsWith(".json");
                    
                    if (isConfigFile && permissions.OthersRead)
                    {
                        return (true, "Warning: Configuration files should not be world-readable");
                    }
                }
                
                return (true, "Permissions are valid");
            }
            catch (Exception ex)
            {
                return (false, $"Validation error: {ex.Message}");
            }
        }
        
        
    }
}
