using System;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem.Security;
using HackerOs.OS.User;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Service for securely executing files in the virtual file system, 
    /// respecting SetUID and SetGID permissions.
    /// </summary>
    public class FileSystemExecutionService
    {
        private readonly IVirtualFileSystem _fileSystem;
        private readonly FileSystemAuditLogger _auditLogger;
        
        /// <summary>
        /// Creates a new instance of the FileSystemExecutionService
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="auditLogger">The audit logger for security events</param>
        public FileSystemExecutionService(IVirtualFileSystem fileSystem, FileSystemAuditLogger auditLogger = null)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _auditLogger = auditLogger;
        }
        
        /// <summary>
        /// Executes a file securely, respecting SetUID and SetGID permissions
        /// </summary>
        /// <typeparam name="T">The result type of the execution</typeparam>
        /// <param name="filePath">The path to the file to execute</param>
        /// <param name="user">The user attempting to execute the file</param>
        /// <param name="executeFunction">The function to execute with the appropriate permissions</param>
        /// <returns>The result of the execution</returns>
        public async Task<T> ExecuteFileAsync<T>(string filePath, UserEntity user, Func<UserEntity, Task<T>> executeFunction)
        {
            return await _fileSystem.ExecuteFileSecureAsync(filePath, user, executeFunction, _auditLogger);
        }
        
        /// <summary>
        /// Executes a file with no return value
        /// </summary>
        /// <param name="filePath">The path to the file to execute</param>
        /// <param name="user">The user attempting to execute the file</param>
        /// <param name="executeAction">The action to execute</param>
        public async Task ExecuteFileAsync(string filePath, UserEntity user, Func<UserEntity, Task> executeAction)
        {
            await _fileSystem.ExecuteFileSecureAsync(filePath, user, async (u) => 
            {
                await executeAction(u);
                return true;
            }, _auditLogger);
        }
        
        /// <summary>
        /// Creates an executable file with the specified permissions
        /// </summary>
        /// <param name="filePath">The path to create the file</param>
        /// <param name="content">The file content</param>
        /// <param name="user">The user creating the file</param>
        /// <param name="isSetUID">Whether to set the SetUID bit</param>
        /// <param name="isSetGID">Whether to set the SetGID bit</param>
        /// <returns>True if the file was created successfully; otherwise, false</returns>
        public async Task<bool> CreateExecutableFileAsync(
            string filePath, 
            byte[] content, 
            UserEntity user,
            bool isSetUID = false,
            bool isSetGID = false)
        {
            try
            {
                // Create the file
                await _fileSystem.CreateFileAsync(filePath, content, user);
                
                // Get the file node
                var node = await _fileSystem.GetNodeAsync(filePath);
                if (node == null || !node.IsFile)
                {
                    return false;
                }
                
                var file = (VirtualFile)node;
                
                // Set execute permission
                var permissions = file.Permissions;
                permissions.OwnerExecute = true;
                
                // Set special bits if requested
                permissions.SetUID = isSetUID;
                permissions.SetGID = isSetGID;
                
                // Apply permissions
                file.Permissions = permissions;
                
                // Log the creation
                await _auditLogger?.LogAsync(
                    user,
                    AuditEventType.FileSystem,
                    AuditSeverity.Information,
                    filePath,
                    "CreateExecutable",
                    true,
                    $"Created executable file with SetUID={isSetUID}, SetGID={isSetGID}");
                
                return true;
            }
            catch (Exception ex)
            {
                await _auditLogger?.LogAsync(
                    user,
                    AuditEventType.Error,
                    AuditSeverity.Error,
                    filePath,
                    "CreateExecutable",
                    false,
                    $"Error creating executable file: {ex.Message}");
                
                return false;
            }
        }
        
        /// <summary>
        /// Safely changes the special permission bits on a file
        /// </summary>
        /// <param name="filePath">The path to the file</param>
        /// <param name="user">The user changing the permissions</param>
        /// <param name="setUID">Whether to enable/disable the SetUID bit, or null to leave unchanged</param>
        /// <param name="setGID">Whether to enable/disable the SetGID bit, or null to leave unchanged</param>
        /// <param name="sticky">Whether to enable/disable the sticky bit, or null to leave unchanged</param>
        /// <returns>True if permissions were changed successfully; otherwise, false</returns>
        public async Task<bool> SetSpecialPermissionsAsync(
            string filePath, 
            UserEntity user,
            bool? setUID = null,
            bool? setGID = null,
            bool? sticky = null)
        {
            try
            {
                // Get the node
                var node = await _fileSystem.GetNodeAsync(filePath);
                if (node == null)
                {
                    return false;
                }
                
                // Verify the user can modify permissions
                if (node.OwnerId != user.Id && user.Id != 0)
                {
                    await _auditLogger?.LogAsync(
                        user,
                        AuditEventType.Security,
                        AuditSeverity.Warning,
                        filePath,
                        "SetSpecialPermissions",
                        false,
                        "Only the owner or root can change permissions");
                    
                    return false;
                }
                
                // Get current permissions
                var permissions = node.Permissions;
                
                // Update permissions
                if (setUID.HasValue)
                {
                    // SetUID is only meaningful for executable files
                    if (setUID.Value && node.IsFile && !permissions.OwnerExecute)
                    {
                        await _auditLogger?.LogAsync(
                            user,
                            AuditEventType.Security,
                            AuditSeverity.Warning,
                            filePath,
                            "SetSpecialPermissions",
                            false,
                            "SetUID requires executable permission");
                        
                        return false;
                    }
                    
                    permissions.SetUID = setUID.Value;
                }
                
                if (setGID.HasValue)
                {
                    // For files, SetGID requires execute permission
                    if (setGID.Value && node.IsFile && !permissions.OwnerExecute)
                    {
                        await _auditLogger?.LogAsync(
                            user,
                            AuditEventType.Security,
                            AuditSeverity.Warning,
                            filePath,
                            "SetSpecialPermissions",
                            false,
                            "SetGID requires executable permission for files");
                        
                        return false;
                    }
                    
                    permissions.SetGID = setGID.Value;
                }
                
                if (sticky.HasValue)
                {
                    // Sticky bit is only meaningful for directories
                    if (sticky.Value && !node.IsDirectory)
                    {
                        await _auditLogger?.LogAsync(
                            user,
                            AuditEventType.Security,
                            AuditSeverity.Warning,
                            filePath,
                            "SetSpecialPermissions",
                            false,
                            "Sticky bit is only meaningful for directories");
                        
                        return false;
                    }
                    
                    permissions.Sticky = sticky.Value;
                }
                
                // Apply updated permissions
                node.Permissions = permissions;
                
                // Log the change
                await _auditLogger?.LogAsync(
                    user,
                    AuditEventType.FileSystem,
                    AuditSeverity.Information,
                    filePath,
                    "SetSpecialPermissions",
                    true,
                    $"Changed special permissions: SetUID={permissions.SetUID}, SetGID={permissions.SetGID}, Sticky={permissions.Sticky}");
                
                return true;
            }
            catch (Exception ex)
            {
                await _auditLogger?.LogAsync(
                    user,
                    AuditEventType.Error,
                    AuditSeverity.Error,
                    filePath,
                    "SetSpecialPermissions",
                    false,
                    $"Error changing special permissions: {ex.Message}");
                
                return false;
            }
        }
    }
}
