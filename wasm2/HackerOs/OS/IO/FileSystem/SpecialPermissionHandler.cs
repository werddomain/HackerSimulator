using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem.Security;
using HackerOs.OS.User;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Integrates special permission handling with the VirtualFileSystem
    /// </summary>
    public static class SpecialPermissionHandler
    {
        /// <summary>
        /// Initializes the special permission handling in the VirtualFileSystem
        /// </summary>
        /// <param name="fileSystem">The file system to initialize</param>
        /// <param name="auditLogger">The audit logger to use</param>
        public static void Initialize(VirtualFileSystem fileSystem, FileSystemAuditLogger auditLogger)
        {
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            
            // Subscribe to file and directory creation events to handle SetGID behavior
            fileSystem.FileCreated += async (sender, args) => 
            {
                await HandleFileCreatedAsync(fileSystem, args.Path, args.User, auditLogger);
            };
            
            fileSystem.DirectoryCreated += async (sender, args) => 
            {
                await HandleDirectoryCreatedAsync(fileSystem, args.Path, args.User, auditLogger);
            };
            
            // Log initialization
            auditLogger?.LogAsync(
                new UserEntity { Id = 0, Username = "system" },
                AuditEventType.System,
                AuditSeverity.Information,
                "/",
                "SpecialPermissionHandler.Initialize",
                true,
                "Special permission handling initialized");
        }
        
        /// <summary>
        /// Handles a file creation event to apply SetGID inheritance
        /// </summary>
        private static async Task HandleFileCreatedAsync(
            VirtualFileSystem fileSystem, 
            string filePath, 
            UserEntity user,
            FileSystemAuditLogger auditLogger)
        {
            try
            {
                string parentPath = GetParentPath(filePath);
                
                // Apply SetGID inheritance if the parent directory has SetGID set
                bool applied = await fileSystem.ApplySetGIDInheritanceAsync(parentPath, filePath, user, false);
                
                if (applied)
                {
                    await auditLogger?.LogAsync(
                        user,
                        AuditEventType.FileSystem,
                        AuditSeverity.Information,
                        filePath,
                        "SetGIDInheritance",
                        true,
                        $"SetGID inheritance applied from {parentPath}");
                }
            }
            catch (Exception ex)
            {
                await auditLogger?.LogAsync(
                    user,
                    AuditEventType.Error,
                    AuditSeverity.Error,
                    filePath,
                    "SetGIDInheritance",
                    false,
                    $"Error applying SetGID inheritance: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handles a directory creation event to apply SetGID inheritance
        /// </summary>
        private static async Task HandleDirectoryCreatedAsync(
            VirtualFileSystem fileSystem, 
            string dirPath, 
            UserEntity user,
            FileSystemAuditLogger auditLogger)
        {
            try
            {
                string parentPath = GetParentPath(dirPath);
                
                // Apply SetGID inheritance if the parent directory has SetGID set
                bool applied = await fileSystem.ApplySetGIDInheritanceAsync(parentPath, dirPath, user, true);
                
                if (applied)
                {
                    await auditLogger?.LogAsync(
                        user,
                        AuditEventType.FileSystem,
                        AuditSeverity.Information,
                        dirPath,
                        "SetGIDInheritance",
                        true,
                        $"SetGID inheritance applied from {parentPath}");
                }
            }
            catch (Exception ex)
            {
                await auditLogger?.LogAsync(
                    user,
                    AuditEventType.Error,
                    AuditSeverity.Error,
                    dirPath,
                    "SetGIDInheritance",
                    false,
                    $"Error applying SetGID inheritance: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the parent path of a file or directory
        /// </summary>
        private static string GetParentPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "/";
            }
            
            int lastSlash = path.LastIndexOf('/');
            if (lastSlash <= 0)
            {
                return "/";
            }
            
            return path.Substring(0, lastSlash);
        }
        
        /// <summary>
        /// Checks if a sticky bit directory allows the specified operation
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="directoryPath">The directory path</param>
        /// <param name="targetPath">The target path for the operation</param>
        /// <param name="user">The user performing the operation</param>
        /// <param name="auditLogger">The audit logger</param>
        /// <returns>True if the operation is allowed; otherwise, false</returns>
        public static async Task<bool> CheckStickyBitPermissionAsync(
            IVirtualFileSystem fileSystem,
            string directoryPath,
            string targetPath,
            UserEntity user,
            FileSystemAuditLogger auditLogger)
        {
            try
            {
                // Get the directory node
                var dirNode = await fileSystem.GetNodeAsync(directoryPath);
                if (dirNode == null || !dirNode.IsDirectory)
                {
                    return true; // No directory to check
                }
                
                var directory = (VirtualDirectory)dirNode;
                
                // If sticky bit is not set, no special checks needed
                if (!directory.Permissions.Sticky)
                {
                    return true;
                }
                
                // Get the target node
                var targetNode = await fileSystem.GetNodeAsync(targetPath);
                if (targetNode == null)
                {
                    return true; // Target doesn't exist, so sticky bit isn't relevant
                }
                
                // Root can always perform operations
                if (user.Id == 0)
                {
                    return true;
                }
                
                // With sticky bit set, user must either:
                // 1. Own the target file/directory
                // 2. Own the directory
                bool isTargetOwner = targetNode.OwnerId == user.Id;
                bool isDirOwner = directory.OwnerId == user.Id;
                
                bool allowed = isTargetOwner || isDirOwner;
                
                // Log the check
                if (!allowed)
                {
                    await auditLogger?.LogAsync(
                        user,
                        AuditEventType.Security,
                        AuditSeverity.Warning,
                        targetPath,
                        "StickyBitCheck",
                        false,
                        $"Operation denied by sticky bit on {directoryPath}");
                }
                
                return allowed;
            }
            catch (Exception ex)
            {
                await auditLogger?.LogAsync(
                    user,
                    AuditEventType.Error,
                    AuditSeverity.Error,
                    targetPath,
                    "StickyBitCheck",
                    false,
                    $"Error checking sticky bit permission: {ex.Message}");
                
                // Fail closed - deny if there's an error
                return false;
            }
        }
    }
}
