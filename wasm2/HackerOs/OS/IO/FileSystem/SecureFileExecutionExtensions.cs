using System;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem.Security;
using HackerOs.OS.User;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Provides extension methods for secure file execution with special permission handling
    /// </summary>
    public static class SecureFileExecutionExtensions
    {
        /// <summary>
        /// Executes a file securely, respecting SetUID and SetGID permissions
        /// </summary>
        /// <typeparam name="T">The result type of the execution</typeparam>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="filePath">The path to the file to execute</param>
        /// <param name="user">The user attempting to execute the file</param>
        /// <param name="executeFunction">The function to execute with the appropriate permissions</param>
        /// <param name="auditLogger">Optional audit logger for security events</param>
        /// <returns>The result of the execution</returns>
        public static async Task<T> ExecuteFileSecureAsync<T>(
            this IVirtualFileSystem fileSystem, 
            string filePath, 
            UserEntity user, 
            Func<UserEntity, Task<T>> executeFunction,
            FileSystemAuditLogger auditLogger = null)
        {
            // Validate parameters
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (executeFunction == null) throw new ArgumentNullException(nameof(executeFunction));
            
            // Get the file node
            var node = await fileSystem.GetNodeAsync(filePath);
            if (node == null)
            {
                await LogSecurityEventAsync(auditLogger, user, "ExecuteFile", filePath, false, "File not found");
                throw new FileNotFoundException($"File not found: {filePath}");
            }
            
            // Check if the node is a file
            if (!node.IsFile)
            {
                await LogSecurityEventAsync(auditLogger, user, "ExecuteFile", filePath, false, "Not a file");
                throw new InvalidOperationException($"Not a file: {filePath}");
            }
            
            var file = (VirtualFile)node;
            
            // Check if the file is executable
            if (!file.CanAccess(user, FileAccessMode.Execute))
            {
                await LogSecurityEventAsync(auditLogger, user, "ExecuteFile", filePath, false, "Permission denied - Execute");
                throw new UnauthorizedAccessException($"Cannot execute file: {filePath}");
            }
            
            // Create a security context
            var securityContext = new UserSecurityContext(user, auditLogger);
            
            try
            {
                // Check for SetUID bit
                if (file.Permissions.SetUID && file.OwnerId != user.UserId)
                {
                    // Get the file owner
                    var fileOwner = UserManager.Instance.GetUserById(file.OwnerId);
                    if (fileOwner == null)
                    {
                        await LogSecurityEventAsync(auditLogger, user, "ExecuteSetUID", filePath, false, $"Owner with ID {file.OwnerId} not found");
                        throw new InvalidOperationException($"Owner with ID {file.OwnerId} not found for file {filePath}");
                    }
                    
                    // Check if elevation to owner is allowed
                    if (!await IsSetUIDElevationAllowedAsync(file, user, fileOwner, auditLogger))
                    {
                        await LogSecurityEventAsync(auditLogger, user, "ExecuteSetUID", filePath, false, "SetUID elevation not allowed");
                        throw new UnauthorizedAccessException($"SetUID elevation not allowed for {filePath}");
                    }
                    
                    // Elevate to the file owner
                    bool elevated = await securityContext.ElevateToUserAsync(
                        fileOwner.Id, 
                        $"SetUID execution of {filePath}", 
                        filePath);
                    
                    if (!elevated)
                    {
                        await LogSecurityEventAsync(auditLogger, user, "ExecuteSetUID", filePath, false, "Failed to elevate to owner");
                        throw new UnauthorizedAccessException($"Failed to elevate to owner for SetUID file: {filePath}");
                    }
                    
                    try
                    {
                        // Execute with elevated permissions
                        return await executeFunction(securityContext.EffectiveUser);
                    }
                    finally
                    {
                        // Restore original permissions
                        await securityContext.RestoreUserAsync(filePath);
                    }
                }
                // Check for SetGID bit
                else if (file.Permissions.SetGID && file.GroupId != user.PrimaryGroupId && 
                        (user.SecondaryGroups == null || !user.SecondaryGroups.Contains(file.GroupId)))
                {
                    // Check if elevation to group is allowed
                    if (!await IsSetGIDElevationAllowedAsync(file, user, auditLogger))
                    {
                        await LogSecurityEventAsync(auditLogger, user, "ExecuteSetGID", filePath, false, "SetGID elevation not allowed");
                        throw new UnauthorizedAccessException($"SetGID elevation not allowed for {filePath}");
                    }
                    
                    // Elevate to the file group
                    bool elevated = await securityContext.ElevateToGroupAsync(
                        file.GroupId, 
                        $"SetGID execution of {filePath}", 
                        filePath);
                    
                    if (!elevated)
                    {
                        await LogSecurityEventAsync(auditLogger, user, "ExecuteSetGID", filePath, false, "Failed to elevate to group");
                        throw new UnauthorizedAccessException($"Failed to elevate to group for SetGID file: {filePath}");
                    }
                    
                    try
                    {
                        // Execute with elevated permissions
                        return await executeFunction(securityContext.EffectiveUser);
                    }
                    finally
                    {
                        // Restore original permissions
                        await securityContext.RestoreGroupAsync(filePath);
                    }
                }
                else
                {
                    // Execute normally with user's permissions
                    await LogSecurityEventAsync(auditLogger, user, "ExecuteFile", filePath, true, null);
                    return await executeFunction(user);
                }
            }
            catch (Exception ex)
            {
                await LogSecurityEventAsync(auditLogger, user, "ExecuteFile", filePath, false, ex.Message);
                throw;
            }
        }
        
        /// <summary>
        /// Checks if SetUID elevation is allowed for a file
        /// </summary>
        private static async Task<bool> IsSetUIDElevationAllowedAsync(
            VirtualFile file, 
            UserEntity user, 
            UserEntity fileOwner,
            FileSystemAuditLogger auditLogger)
        {
            // Root can always elevate
            if (user.Id == 0)
            {
                return true;
            }
            
            // Don't allow elevation to root unless from a trusted path
            if (fileOwner.Id == 0 && !IsTrustedExecutablePath(file.FullPath))
            {
                await LogSecurityEventAsync(auditLogger, user, "SetUIDCheck", file.FullPath, false, 
                    "Elevation to root from untrusted path denied");
                return false;
            }
            
            // Check for risky permission combinations
            if (file.Permissions.OwnerWrite && file.OwnerId != user.Id)
            {
                await LogSecurityEventAsync(auditLogger, user, "SetUIDCheck", file.FullPath, false, 
                    "Risky SetUID with owner write permission");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Checks if SetGID elevation is allowed for a file
        /// </summary>
        private static async Task<bool> IsSetGIDElevationAllowedAsync(
            VirtualFile file, 
            UserEntity user,
            FileSystemAuditLogger auditLogger)
        {
            // Root can always elevate
            if (user.Id == 0)
            {
                return true;
            }
            
            // Check for risky permission combinations
            if (file.Permissions.GroupWrite && file.GroupId != user.PrimaryGroupId && 
                (user.SecondaryGroups == null || !user.SecondaryGroups.Contains(file.GroupId)))
            {
                await LogSecurityEventAsync(auditLogger, user, "SetGIDCheck", file.FullPath, false, 
                    "Risky SetGID with group write permission");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Checks if a path is a trusted executable path for SetUID to root
        /// </summary>
        private static bool IsTrustedExecutablePath(string path)
        {
            string[] trustedPaths = new[]
            {
                "/bin/",
                "/sbin/",
                "/usr/bin/",
                "/usr/sbin/",
                "/usr/local/bin/"
            };
            
            foreach (var trustedPath in trustedPaths)
            {
                if (path.StartsWith(trustedPath))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Logs a security event
        /// </summary>
        private static async Task LogSecurityEventAsync(
            FileSystemAuditLogger auditLogger,
            UserEntity user,
            string operation,
            string path,
            bool success,
            string message)
        {
            if (auditLogger == null)
            {
                return;
            }
            
            await auditLogger.LogAsync(
                user, 
                AuditEventType.Security, 
                success ? AuditSeverity.Information : AuditSeverity.Warning,
                path,
                operation,
                success,
                message);
        }
    }
}
