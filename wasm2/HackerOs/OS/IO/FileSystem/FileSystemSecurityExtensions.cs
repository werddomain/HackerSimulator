using System;
using System.Threading.Tasks;
using HackerOs.OS.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Extension methods for integrating group-based security features (quotas and policies)
    /// with file system operations
    /// </summary>
    public static class FileSystemSecurityExtensions
    {
        /// <summary>
        /// Performs comprehensive security checks for a file operation, including
        /// permissions, quotas, and policies
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the file or directory</param>
        /// <param name="operation">The operation type (read, write, create, delete, etc.)</param>
        /// <param name="sizeChange">Size change for the operation in bytes (for quota checks)</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <returns>A SecurityCheckResult indicating whether the operation is allowed</returns>
        public static async Task<SecurityCheckResult> PerformSecurityCheckAsync(
            this VirtualFileSystem fileSystem,
            string path,
            string operation,
            long sizeChange,
            User user,
            GroupQuotaManager quotaManager,
            GroupPolicyManager policyManager)
        {
            // Skip all checks for root/admin users if they have enforcement disabled
            if (user.IsAdmin && user.BypassSecurity)
            {
                return SecurityCheckResult.CreateAllowed("Administrative override");
            }
            
            try
            {
                // 1. Check file system permissions (already implemented in VirtualFileSystem)
                var permissionCheck = await fileSystem.CheckAccessAsync(path, operation, user);
                if (!permissionCheck)
                {
                    return SecurityCheckResult.CreateDenied(
                        "Permission denied",
                        SecurityDenialReason.PermissionDenied);
                }
                
                // 2. Check quota if operation would increase space usage
                if (sizeChange > 0)
                {
                    bool quotaAllowed = await fileSystem.CheckQuotaForOperationAsync(
                        path, sizeChange, user, quotaManager);
                    
                    if (!quotaAllowed)
                    {
                        return SecurityCheckResult.CreateDenied(
                            "Quota exceeded",
                            SecurityDenialReason.QuotaExceeded);
                    }
                }
                
                // 3. Check policy constraints
                var policyResult = await fileSystem.CheckPolicyForOperationAsync(
                    path, operation, user, policyManager);
                
                if (!policyResult.IsAllowed)
                {
                    return SecurityCheckResult.CreateDenied(
                        policyResult.Message,
                        SecurityDenialReason.PolicyDenied);
                }
                
                // All checks passed
                return SecurityCheckResult.CreateAllowed("Operation allowed");
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error during security check: {ex.Message}");
                
                // Deny by default on error
                return SecurityCheckResult.CreateDenied(
                    "Security check error",
                    SecurityDenialReason.Error);
            }
        }
        
        /// <summary>
        /// Updates quota and logs policy information after a successful file operation
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the file or directory</param>
        /// <param name="operation">The operation that was performed</param>
        /// <param name="sizeChange">Size change in bytes</param>
        /// <param name="user">User who performed the operation</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task UpdateSecurityStateAsync(
            this VirtualFileSystem fileSystem,
            string path,
            string operation,
            long sizeChange,
            User user,
            GroupQuotaManager quotaManager,
            GroupPolicyManager policyManager)
        {
            try
            {
                // Update quota usage if size changed
                if (sizeChange != 0)
                {
                    await fileSystem.UpdateQuotaUsageAsync(path, sizeChange, quotaManager);
                }
                
                // Log the operation for auditing
                var node = await fileSystem.GetNodeAsync(path);
                if (node != null)
                {
                    // Create policy context for the completed operation
                    var context = new PolicyContext
                    {
                        UserId = user.UserId,
                        Resource = path,
                        Operation = operation,
                        Data = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "path", path },
                            { "operation", operation },
                            { "isDirectory", node.IsDirectory },
                            { "size", node.Size },
                            { "sizeChange", sizeChange },
                            { "ownerId", node.OwnerId },
                            { "groupId", node.GroupId },
                            { "completed", true }
                        }
                    };
                    
                    // Log the operation (could send to an audit log system)
                    await policyManager.LogOperationAsync(context);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - this is post-operation cleanup
                Console.WriteLine($"Error updating security state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Creates a file with all security checks and updates
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the file to create</param>
        /// <param name="content">Initial content of the file</param>
        /// <param name="permissions">File permissions</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <returns>Result indicating success or failure</returns>
        public static async Task<SecurityOperationResult> CreateFileSecureAsync(
            this VirtualFileSystem fileSystem,
            string path,
            string content,
            FilePermissions permissions,
            User user,
            GroupQuotaManager quotaManager,
            GroupPolicyManager policyManager)
        {
            // Calculate the size change
            long sizeChange = content?.Length ?? 0;
            
            // Perform security checks
            var securityCheck = await PerformSecurityCheckAsync(
                fileSystem, path, "create", sizeChange, user, quotaManager, policyManager);
            
            if (!securityCheck.IsAllowed)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = securityCheck.Message,
                    DenialReason = securityCheck.DenialReason
                };
            }
            
            try
            {
                // Perform the operation
                await fileSystem.CreateFileAsync(path, content, permissions, user.UserId, user.PrimaryGroupId);
                
                // Update security state
                await UpdateSecurityStateAsync(
                    fileSystem, path, "create", sizeChange, user, quotaManager, policyManager);
                
                return new SecurityOperationResult
                {
                    Success = true,
                    Message = "File created successfully"
                };
            }
            catch (Exception ex)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = $"Error creating file: {ex.Message}",
                    DenialReason = SecurityDenialReason.Error
                };
            }
        }
        
        /// <summary>
        /// Writes to a file with all security checks and updates
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the file to write</param>
        /// <param name="content">Content to write</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <returns>Result indicating success or failure</returns>
        public static async Task<SecurityOperationResult> WriteFileSecureAsync(
            this VirtualFileSystem fileSystem,
            string path,
            string content,
            User user,
            GroupQuotaManager quotaManager,
            GroupPolicyManager policyManager)
        {
            // Get the current file size
            long currentSize = 0;
            var node = await fileSystem.GetNodeAsync(path);
            if (node != null && !node.IsDirectory)
            {
                currentSize = node.Size;
            }
            
            // Calculate the size change
            long newSize = content?.Length ?? 0;
            long sizeChange = newSize - currentSize;
            
            // Perform security checks
            var securityCheck = await PerformSecurityCheckAsync(
                fileSystem, path, "write", sizeChange, user, quotaManager, policyManager);
            
            if (!securityCheck.IsAllowed)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = securityCheck.Message,
                    DenialReason = securityCheck.DenialReason
                };
            }
            
            try
            {
                // Perform the operation
                await fileSystem.WriteFileAsync(path, content);
                
                // Update security state
                await UpdateSecurityStateAsync(
                    fileSystem, path, "write", sizeChange, user, quotaManager, policyManager);
                
                return new SecurityOperationResult
                {
                    Success = true,
                    Message = "File written successfully"
                };
            }
            catch (Exception ex)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = $"Error writing file: {ex.Message}",
                    DenialReason = SecurityDenialReason.Error
                };
            }
        }
        
        /// <summary>
        /// Deletes a file with all security checks and updates
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the file to delete</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <returns>Result indicating success or failure</returns>
        public static async Task<SecurityOperationResult> DeleteFileSecureAsync(
            this VirtualFileSystem fileSystem,
            string path,
            User user,
            GroupQuotaManager quotaManager,
            GroupPolicyManager policyManager)
        {
            // Get the current file size
            long currentSize = 0;
            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || node.IsDirectory)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = "File not found or is a directory",
                    DenialReason = SecurityDenialReason.NotFound
                };
            }
            
            currentSize = node.Size;
            
            // Calculate the size change (negative for deletion)
            long sizeChange = -currentSize;
            
            // Perform security checks
            var securityCheck = await PerformSecurityCheckAsync(
                fileSystem, path, "delete", 0, user, quotaManager, policyManager);
            
            if (!securityCheck.IsAllowed)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = securityCheck.Message,
                    DenialReason = securityCheck.DenialReason
                };
            }
            
            try
            {
                // Perform the operation
                await fileSystem.DeleteFileAsync(path);
                
                // Update security state
                await UpdateSecurityStateAsync(
                    fileSystem, path, "delete", sizeChange, user, quotaManager, policyManager);
                
                return new SecurityOperationResult
                {
                    Success = true,
                    Message = "File deleted successfully"
                };
            }
            catch (Exception ex)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = $"Error deleting file: {ex.Message}",
                    DenialReason = SecurityDenialReason.Error
                };
            }
        }
        
        /// <summary>
        /// Creates a directory with all security checks and updates
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the directory to create</param>
        /// <param name="permissions">Directory permissions</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <param name="recursive">Whether to create parent directories if they don't exist</param>
        /// <returns>Result indicating success or failure</returns>
        public static async Task<SecurityOperationResult> CreateDirectorySecureAsync(
            this VirtualFileSystem fileSystem,
            string path,
            FilePermissions permissions,
            User user,
            GroupQuotaManager quotaManager,
            GroupPolicyManager policyManager,
            bool recursive = false)
        {
            // Directory creation uses minimal space for metadata, typically a few KB
            // This is a conservative estimate for directory entry size
            const long DIRECTORY_METADATA_SIZE = 4096;
            
            // Perform security checks
            var securityCheck = await PerformSecurityCheckAsync(
                fileSystem, path, "create", DIRECTORY_METADATA_SIZE, user, quotaManager, policyManager);
            
            if (!securityCheck.IsAllowed)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = securityCheck.Message,
                    DenialReason = securityCheck.DenialReason
                };
            }

            try
            {
                // Handle recursive creation if needed
                if (recursive)
                {
                    string parentPath = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(parentPath) && !await fileSystem.DirectoryExistsAsync(parentPath))
                    {
                        // Create parent directory first
                        var parentResult = await CreateDirectorySecureAsync(
                            fileSystem, parentPath, permissions, user, quotaManager, policyManager, true);
                        
                        if (!parentResult.Success)
                        {
                            return parentResult;
                        }
                    }
                }
                
                // Perform the operation
                await fileSystem.CreateDirectoryAsync(path, permissions, user.UserId, user.PrimaryGroupId);
                
                // Update security state
                await UpdateSecurityStateAsync(
                    fileSystem, path, "create", DIRECTORY_METADATA_SIZE, user, quotaManager, policyManager);
                
                return new SecurityOperationResult
                {
                    Success = true,
                    Message = "Directory created successfully"
                };
            }
            catch (Exception ex)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = $"Error creating directory: {ex.Message}",
                    DenialReason = SecurityDenialReason.Error
                };
            }
        }
        
        /// <summary>
        /// Deletes a directory with all security checks and updates
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the directory to delete</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <param name="recursive">Whether to delete directory contents recursively</param>
        /// <returns>Result indicating success or failure</returns>
        public static async Task<SecurityOperationResult> DeleteDirectorySecureAsync(
            this VirtualFileSystem fileSystem,
            string path,
            User user,
            GroupQuotaManager quotaManager,
            GroupPolicyManager policyManager,
            bool recursive = false)
        {
            // Check if directory exists
            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || !node.IsDirectory)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = "Directory not found",
                    DenialReason = SecurityDenialReason.NotFound
                };
            }
            
            // Calculate size change for quota tracking
            long sizeChange = 0;
            
            try
            {
                // If recursive, calculate total size to be freed
                if (recursive)
                {
                    sizeChange = -await fileSystem.CalculateDirectorySizeAsync(path);
                }
                else
                {
                    // For non-recursive, check if directory is empty
                    var contents = await fileSystem.GetDirectoryContentsAsync(path);
                    if (contents.GetEnumerator().MoveNext())
                    {
                        return new SecurityOperationResult
                        {
                            Success = false,
                            Message = "Directory is not empty",
                            DenialReason = SecurityDenialReason.DirectoryNotEmpty
                        };
                    }
                    
                    // Directory metadata size
                    sizeChange = -4096;
                }
                
                // Special context for directory deletion
                var context = new PolicyContext
                {
                    UserId = user.UserId,
                    Resource = path,
                    Operation = "delete",
                    Data = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "path", path },
                        { "operation", "delete" },
                        { "isDirectory", true },
                        { "recursive", recursive },
                        { "ownerId", node.OwnerId },
                        { "groupId", node.GroupId }
                    }
                };
                
                // Add sticky bit context for parent directory
                var parentPath = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parentNode = await fileSystem.GetNodeAsync(parentPath);
                    if (parentNode != null)
                    {
                        context.Data["parentStickyBit"] = parentNode.Permissions.IsSticky;
                        context.Data["parentOwnerId"] = parentNode.OwnerId;
                    }
                }
                
                // Perform security checks - with custom context for sticky bit
                var policyResult = await policyManager.EvaluatePoliciesAsync(user, context);
                if (!policyResult.IsAllowed)
                {
                    return new SecurityOperationResult
                    {
                        Success = false,
                        Message = policyResult.Message,
                        DenialReason = SecurityDenialReason.PolicyDenied
                    };
                }
                
                // Check standard permissions
                var permissionCheck = await fileSystem.CheckAccessAsync(path, "delete", user);
                if (!permissionCheck)
                {
                    return new SecurityOperationResult
                    {
                        Success = false,
                        Message = "Permission denied",
                        DenialReason = SecurityDenialReason.PermissionDenied
                    };
                }
                
                // Perform the operation
                await fileSystem.DeleteDirectoryAsync(path, recursive);
                
                // Update security state
                await UpdateSecurityStateAsync(
                    fileSystem, path, "delete", sizeChange, user, quotaManager, policyManager);
                
                return new SecurityOperationResult
                {
                    Success = true,
                    Message = "Directory deleted successfully"
                };
            }
            catch (Exception ex)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = $"Error deleting directory: {ex.Message}",
                    DenialReason = SecurityDenialReason.Error
                };
            }
        }
        
        /// <summary>
        /// Moves a directory with all security checks and updates
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="sourcePath">Source directory path</param>
        /// <param name="destinationPath">Destination directory path</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <returns>Result indicating success or failure</returns>
        public static async Task<SecurityOperationResult> MoveDirectorySecureAsync(
            this VirtualFileSystem fileSystem,
            string sourcePath,
            string destinationPath,
            User user,
            GroupQuotaManager quotaManager,
            GroupPolicyManager policyManager)
        {
            // Check if source directory exists
            var sourceNode = await fileSystem.GetNodeAsync(sourcePath);
            if (sourceNode == null || !sourceNode.IsDirectory)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = "Source directory not found",
                    DenialReason = SecurityDenialReason.NotFound
                };
            }
            
            // Check if destination exists
            if (await fileSystem.FileExistsAsync(destinationPath) || await fileSystem.DirectoryExistsAsync(destinationPath))
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = "Destination already exists",
                    DenialReason = SecurityDenialReason.TargetExists
                };
            }
            
            // Check if destination is a subdirectory of source (which would create a cycle)
            if (destinationPath.StartsWith(sourcePath + "/"))
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = "Cannot move a directory to its own subdirectory",
                    DenialReason = SecurityDenialReason.CircularReference
                };
            }
            
            // Get source and destination parent directories
            string sourceParentPath = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? "/";
            string destParentPath = System.IO.Path.GetDirectoryName(destinationPath)?.Replace('\\', '/') ?? "/";
            
            // Calculate quota impact - only relevant if moving between different group quotas
            long sizeChange = 0;
            var sourceGroupId = sourceNode.GroupId;
            var destParentNode = await fileSystem.GetNodeAsync(destParentPath);
            
            if (destParentNode != null && destParentNode.GroupId != sourceGroupId)
            {
                // Moving to a different group - need to calculate size
                sizeChange = await fileSystem.CalculateDirectorySizeAsync(sourcePath);
                
                // Check quota for destination group
                bool quotaAllowed = await quotaManager.CheckQuotaAsync(destParentNode.GroupId, sizeChange);
                if (!quotaAllowed)
                {
                    return new SecurityOperationResult
                    {
                        Success = false,
                        Message = "Quota exceeded for destination",
                        DenialReason = SecurityDenialReason.QuotaExceeded
                    };
                }
            }
            
            // Check permissions for source (delete permission)
            var sourcePermCheck = await fileSystem.CheckAccessAsync(sourcePath, "delete", user);
            if (!sourcePermCheck)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = "Permission denied for source directory",
                    DenialReason = SecurityDenialReason.PermissionDenied
                };
            }
            
            // Check permissions for destination parent (write permission)
            var destPermCheck = await fileSystem.CheckAccessAsync(destParentPath, "write", user);
            if (!destPermCheck)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = "Permission denied for destination directory",
                    DenialReason = SecurityDenialReason.PermissionDenied
                };
            }
            
            // Create policy context with move details
            var context = new PolicyContext
            {
                UserId = user.UserId,
                Resource = sourcePath,
                Operation = "move",
                Data = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "sourcePath", sourcePath },
                    { "destinationPath", destinationPath },
                    { "isDirectory", true },
                    { "sourceOwnerId", sourceNode.OwnerId },
                    { "sourceGroupId", sourceNode.GroupId }
                }
            };
            
            // Add sticky bit check for source parent
            var sourceParentNode = await fileSystem.GetNodeAsync(sourceParentPath);
            if (sourceParentNode != null)
            {
                bool stickyBitSet = sourceParentNode.Permissions.IsSticky;
                if (stickyBitSet && sourceParentNode.OwnerId != user.UserId && sourceNode.OwnerId != user.UserId && !user.IsAdmin)
                {
                    return new SecurityOperationResult
                    {
                        Success = false,
                        Message = "Cannot remove directory: sticky bit is set on parent directory",
                        DenialReason = SecurityDenialReason.PermissionDenied
                    };
                }
                
                context.Data["sourceParentStickyBit"] = stickyBitSet;
            }
            
            // Evaluate policies
            var policyResult = await policyManager.EvaluatePoliciesAsync(user, context);
            if (!policyResult.IsAllowed)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = policyResult.Message,
                    DenialReason = SecurityDenialReason.PolicyDenied
                };
            }
            
            try
            {
                // Perform the move operation
                await fileSystem.MoveAsync(sourcePath, destinationPath);
                
                // If the move changed group ownership, update quotas
                if (sizeChange != 0)
                {
                    // Reduce quota for source group
                    await quotaManager.UpdateUsageAsync(sourceGroupId, -sizeChange);
                    
                    // Increase quota for destination group
                    await quotaManager.UpdateUsageAsync(destParentNode.GroupId, sizeChange);
                }
                
                // Log the operation
                await policyManager.LogOperationAsync(context);
                
                return new SecurityOperationResult
                {
                    Success = true,
                    Message = "Directory moved successfully"
                };
            }
            catch (Exception ex)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = $"Error moving directory: {ex.Message}",
                    DenialReason = SecurityDenialReason.Error
                };
            }
        }
        
        /// <summary>
        /// Enumerates directory contents with security filtering
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Directory path to enumerate</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <returns>Result containing the filtered directory entries</returns>
        public static async Task<SecureDirectoryListResult> EnumerateDirectorySecureAsync(
            this VirtualFileSystem fileSystem,
            string path,
            User user,
            GroupPolicyManager policyManager)
        {
            // Check if directory exists
            if (!await fileSystem.DirectoryExistsAsync(path))
            {
                return new SecureDirectoryListResult
                {
                    Success = false,
                    Message = "Directory not found",
                    DenialReason = SecurityDenialReason.NotFound,
                    Entries = new VirtualFileSystemNode[0]
                };
            }
            
            // Check read permission on directory
            var permissionCheck = await fileSystem.CheckAccessAsync(path, "read", user);
            if (!permissionCheck)
            {
                return new SecureDirectoryListResult
                {
                    Success = false,
                    Message = "Permission denied",
                    DenialReason = SecurityDenialReason.PermissionDenied,
                    Entries = new VirtualFileSystemNode[0]
                };
            }
            
            // Create policy context for directory listing
            var context = new PolicyContext
            {
                UserId = user.UserId,
                Resource = path,
                Operation = "list",
                Data = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "path", path },
                    { "operation", "list" }
                }
            };
            
            // Evaluate policies
            var policyResult = await policyManager.EvaluatePoliciesAsync(user, context);
            if (!policyResult.IsAllowed)
            {
                return new SecureDirectoryListResult
                {
                    Success = false,
                    Message = policyResult.Message,
                    DenialReason = SecurityDenialReason.PolicyDenied,
                    Entries = new VirtualFileSystemNode[0]
                };
            }
            
            try
            {
                // Perform the directory listing
                var entries = await fileSystem.ListDirectoryAsync(path, user);
                
                // Log the operation
                await policyManager.LogOperationAsync(context);
                
                return new SecureDirectoryListResult
                {
                    Success = true,
                    Message = "Directory enumerated successfully",
                    Entries = entries,
                    DenialReason = SecurityDenialReason.None
                };
            }
            catch (Exception ex)
            {
                return new SecureDirectoryListResult
                {
                    Success = false,
                    Message = $"Error enumerating directory: {ex.Message}",
                    DenialReason = SecurityDenialReason.Error,
                    Entries = new VirtualFileSystemNode[0]
                };
            }
        }
        
        /// <summary>
        /// Changes directory permissions securely
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Directory path</param>
        /// <param name="permissions">New permissions to set</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <param name="recursive">Whether to apply recursively to contents</param>
        /// <returns>Result indicating success or failure</returns>
        public static async Task<SecurityOperationResult> SetDirectoryPermissionsSecureAsync(
            this VirtualFileSystem fileSystem,
            string path,
            FilePermissions permissions,
            User user,
            GroupPolicyManager policyManager,
            bool recursive = false)
        {
            // Check if directory exists
            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || !node.IsDirectory)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = "Directory not found",
                    DenialReason = SecurityDenialReason.NotFound
                };
            }
            
            // Check permission to change permissions (owner or root)
            if (node.OwnerId != user.UserId && !user.IsAdmin)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = "Only the owner can change permissions",
                    DenialReason = SecurityDenialReason.PermissionDenied
                };
            }
            
            // Create policy context for permission change
            var context = new PolicyContext
            {
                UserId = user.UserId,
                Resource = path,
                Operation = "chmod",
                Data = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "path", path },
                    { "operation", "chmod" },
                    { "isDirectory", true },
                    { "recursive", recursive },
                    { "currentPermissions", node.Permissions.ToOctalString() },
                    { "newPermissions", permissions.ToOctalString() },
                    { "ownerId", node.OwnerId },
                    { "groupId", node.GroupId }
                }
            };
            
            // Evaluate policies
            var policyResult = await policyManager.EvaluatePoliciesAsync(user, context);
            if (!policyResult.IsAllowed)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = policyResult.Message,
                    DenialReason = SecurityDenialReason.PolicyDenied
                };
            }
            
            try
            {
                // Perform the permission change
                if (recursive)
                {
                    await fileSystem.SetPermissionsRecursiveAsync(path, permissions);
                }
                else
                {
                    await fileSystem.SetPermissionsAsync(path, permissions);
                }
                
                // Log the operation
                await policyManager.LogOperationAsync(context);
                
                return new SecurityOperationResult
                {
                    Success = true,
                    Message = "Permissions changed successfully"
                };
            }
            catch (Exception ex)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = $"Error changing permissions: {ex.Message}",
                    DenialReason = SecurityDenialReason.Error
                };
            }
        }
        
        /// <summary>
        /// Changes directory ownership securely
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Directory path</param>
        /// <param name="ownerId">New owner ID</param>
        /// <param name="groupId">New group ID</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <param name="recursive">Whether to apply recursively to contents</param>
        /// <returns>Result indicating success or failure</returns>
        public static async Task<SecurityOperationResult> SetDirectoryOwnershipSecureAsync(
            this VirtualFileSystem fileSystem,
            string path,
            int ownerId,
            int groupId,
            User user,
            GroupQuotaManager quotaManager,
            GroupPolicyManager policyManager,
            bool recursive = false)
        {
            // Check if directory exists
            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || !node.IsDirectory)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = "Directory not found",
                    DenialReason = SecurityDenialReason.NotFound
                };
            }
            
            // Only root/admin can change ownership
            if (!user.IsAdmin)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = "Only administrators can change ownership",
                    DenialReason = SecurityDenialReason.PermissionDenied
                };
            }
            
            // Calculate quota impact if changing group
            long sizeChange = 0;
            if (node.GroupId != groupId)
            {
                sizeChange = await fileSystem.CalculateDirectorySizeAsync(path);
                
                // Check quota for new group
                bool quotaAllowed = await quotaManager.CheckQuotaAsync(groupId, sizeChange);
                if (!quotaAllowed)
                {
                    return new SecurityOperationResult
                    {
                        Success = false,
                        Message = "Quota exceeded for destination group",
                        DenialReason = SecurityDenialReason.QuotaExceeded
                    };
                }
            }
            
            // Create policy context for ownership change
            var context = new PolicyContext
            {
                UserId = user.UserId,
                Resource = path,
                Operation = "chown",
                Data = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "path", path },
                    { "operation", "chown" },
                    { "isDirectory", true },
                    { "recursive", recursive },
                    { "currentOwnerId", node.OwnerId },
                    { "currentGroupId", node.GroupId },
                    { "newOwnerId", ownerId },
                    { "newGroupId", groupId }
                }
            };
            
            // Evaluate policies
            var policyResult = await policyManager.EvaluatePoliciesAsync(user, context);
            if (!policyResult.IsAllowed)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = policyResult.Message,
                    DenialReason = SecurityDenialReason.PolicyDenied
                };
            }
            
            try
            {
                // Track old group ID for quota updates
                int oldGroupId = node.GroupId;
                
                // Perform the ownership change
                if (recursive)
                {
                    await fileSystem.SetOwnershipRecursiveAsync(path, ownerId, groupId);
                }
                else
                {
                    await fileSystem.SetOwnershipAsync(path, ownerId, groupId);
                }
                
                // Update quotas if group changed
                if (oldGroupId != groupId && sizeChange > 0)
                {
                    // Reduce quota for old group
                    await quotaManager.UpdateUsageAsync(oldGroupId, -sizeChange);
                    
                    // Increase quota for new group
                    await quotaManager.UpdateUsageAsync(groupId, sizeChange);
                }
                
                // Log the operation
                await policyManager.LogOperationAsync(context);
                
                return new SecurityOperationResult
                {
                    Success = true,
                    Message = "Ownership changed successfully"
                };
            }
            catch (Exception ex)
            {
                return new SecurityOperationResult
                {
                    Success = false,
                    Message = $"Error changing ownership: {ex.Message}",
                    DenialReason = SecurityDenialReason.Error
                };
            }
        }
    }

    /// <summary>
    /// Result of a security check operation
    /// </summary>
    public class SecurityCheckResult
    {
        /// <summary>
        /// Whether the operation is allowed
        /// </summary>
        public bool IsAllowed { get; private set; }
        
        /// <summary>
        /// Message explaining the result
        /// </summary>
        public string Message { get; private set; }
        
        /// <summary>
        /// Reason for denial if not allowed
        /// </summary>
        public SecurityDenialReason DenialReason { get; private set; }
        
        /// <summary>
        /// Creates an allowed result
        /// </summary>
        /// <param name="message">Explanation message</param>
        /// <returns>SecurityCheckResult instance</returns>
        public static SecurityCheckResult CreateAllowed(string message)
        {
            return new SecurityCheckResult
            {
                IsAllowed = true,
                Message = message,
                DenialReason = SecurityDenialReason.None
            };
        }
        
        /// <summary>
        /// Creates a denied result
        /// </summary>
        /// <param name="message">Explanation message</param>
        /// <param name="reason">Reason for denial</param>
        /// <returns>SecurityCheckResult instance</returns>
        public static SecurityCheckResult CreateDenied(string message, SecurityDenialReason reason)
        {
            return new SecurityCheckResult
            {
                IsAllowed = false,
                Message = message,
                DenialReason = reason
            };
        }
    }

    /// <summary>
    /// Result of a file system operation with security checks
    /// </summary>
    public class SecurityOperationResult
    {
        /// <summary>
        /// Whether the operation succeeded
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Message explaining the result
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// Reason for denial if not successful
        /// </summary>
        public SecurityDenialReason DenialReason { get; set; }
    }

    /// <summary>
    /// Reasons for security denial
    /// </summary>
    public enum SecurityDenialReason
    {
        /// <summary>
        /// No denial (operation allowed)
        /// </summary>
        None,
        
        /// <summary>
        /// File system permission denied
        /// </summary>
        PermissionDenied,
        
        /// <summary>
        /// Group quota exceeded
        /// </summary>
        QuotaExceeded,
        
        /// <summary>
        /// Policy constraint violation
        /// </summary>
        PolicyDenied,
        
        /// <summary>
        /// File or directory not found
        /// </summary>
        NotFound,
        
        /// <summary>
        /// Error during security check
        /// </summary>
        Error,
        
        /// <summary>
        /// Operation would result in a non-empty directory deletion
        /// </summary>
        DirectoryNotEmpty,
        
        /// <summary>
        /// Operation would result in a circular directory structure
        /// </summary>
        CircularReference,
        
        /// <summary>
        /// Target already exists
        /// </summary>
        TargetExists
    }
    
    /// <summary>
    /// Result of a secure directory listing operation
    /// </summary>
    public class SecureDirectoryListResult : SecurityOperationResult
    {
        /// <summary>
        /// The directory entries retrieved
        /// </summary>
        public System.Collections.Generic.IEnumerable<VirtualFileSystemNode> Entries { get; set; } = 
            new VirtualFileSystemNode[0];
    }
}
