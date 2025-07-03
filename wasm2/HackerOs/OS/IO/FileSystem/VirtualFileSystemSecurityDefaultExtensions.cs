using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Extension methods for VirtualFileSystem to use secure operations by default
    /// </summary>
    public static class VirtualFileSystemSecurityDefaultExtensions
    {
        /// <summary>
        /// Updates the VirtualFileSystem class to use secure operations by default
        /// </summary>
        /// <param name="fileSystem">The file system to update</param>
        /// <param name="quotaManager">The quota manager to use</param>
        /// <param name="policyManager">The policy manager to use</param>
        /// <returns>True if the update was successful</returns>
        public static async Task<bool> UseSecureOperationsByDefaultAsync(
            this VirtualFileSystem fileSystem,
            GroupQuotaManager quotaManager,
            GroupPolicyManager policyManager)
        {
            // Initialize security
            await VirtualFileSystemIntegration.InitializeSecurityFrameworkAsync(
                fileSystem, quotaManager, policyManager);
                
            return true;
        }
        
        /// <summary>
        /// Gets the file system's audit logger
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <returns>The audit logger</returns>
        public static FileSystemAuditLogger GetAuditLogger(this VirtualFileSystem fileSystem)
        {
            return VirtualFileSystemIntegration.GetAuditLogger();
        }
        
        /// <summary>
        /// Creates a file with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The file path</param>
        /// <param name="content">The file content</param>
        /// <param name="user">The user performing the operation</param>
        /// <param name="permissions">Optional permissions to set</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> CreateFileAsync(
            this VirtualFileSystem fileSystem,
            string path,
            string content,
            User user,
            FilePermissions permissions = null)
        {
            // Use default permissions if not specified
            if (permissions == null)
            {
                permissions = new FilePermissions(6, 4, 4); // rw-r--r--
            }
            
            return await fileSystem.CreateFileSecurelyAsync(path, content, permissions, user);
        }
        
        /// <summary>
        /// Writes to a file with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The file path</param>
        /// <param name="content">The file content</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> WriteFileAsync(
            this VirtualFileSystem fileSystem,
            string path,
            string content,
            User user)
        {
            return await fileSystem.WriteFileSecurelyAsync(path, content, user);
        }
        
        /// <summary>
        /// Deletes a file with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The file path</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> DeleteFileAsync(
            this VirtualFileSystem fileSystem,
            string path,
            User user)
        {
            return await fileSystem.DeleteFileSecurelyAsync(path, user);
        }
        
        /// <summary>
        /// Creates a directory with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The directory path</param>
        /// <param name="user">The user performing the operation</param>
        /// <param name="permissions">Optional permissions to set</param>
        /// <param name="recursive">Whether to create parent directories</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> CreateDirectoryAsync(
            this VirtualFileSystem fileSystem,
            string path,
            User user,
            FilePermissions permissions = null,
            bool recursive = false)
        {
            // Use default permissions if not specified
            if (permissions == null)
            {
                permissions = new FilePermissions(7, 5, 5); // rwxr-xr-x
            }
            
            return await fileSystem.CreateDirectorySecurelyAsync(path, permissions, user, recursive);
        }
        
        /// <summary>
        /// Deletes a directory with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The directory path</param>
        /// <param name="user">The user performing the operation</param>
        /// <param name="recursive">Whether to delete recursively</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> DeleteDirectoryAsync(
            this VirtualFileSystem fileSystem,
            string path,
            User user,
            bool recursive = false)
        {
            return await fileSystem.DeleteDirectorySecurelyAsync(path, user, recursive);
        }
        
        /// <summary>
        /// Moves a directory with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="sourcePath">The source directory path</param>
        /// <param name="destinationPath">The destination directory path</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> MoveDirectoryAsync(
            this VirtualFileSystem fileSystem,
            string sourcePath,
            string destinationPath,
            User user)
        {
            return await fileSystem.MoveDirectorySecurelyAsync(sourcePath, destinationPath, user);
        }
        
        /// <summary>
        /// Lists directory contents with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The directory path</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>Directory entries</returns>
        public static async Task<IEnumerable<VirtualFileSystemNode>> ListDirectoryAsync(
            this VirtualFileSystem fileSystem,
            string path,
            User user)
        {
            return await fileSystem.EnumerateDirectorySecurelyAsync(path, user);
        }
        
        /// <summary>
        /// Sets file permissions with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The file path</param>
        /// <param name="permissions">The permissions to set</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> SetFilePermissionsAsync(
            this VirtualFileSystem fileSystem,
            string path,
            FilePermissions permissions,
            User user)
        {
            return await fileSystem.SetFilePermissionsSecurelyAsync(path, permissions, user);
        }
        
        /// <summary>
        /// Sets directory permissions with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The directory path</param>
        /// <param name="permissions">The permissions to set</param>
        /// <param name="user">The user performing the operation</param>
        /// <param name="recursive">Whether to set recursively</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> SetDirectoryPermissionsAsync(
            this VirtualFileSystem fileSystem,
            string path,
            FilePermissions permissions,
            User user,
            bool recursive = false)
        {
            return await fileSystem.SetDirectoryPermissionsSecurelyAsync(path, permissions, user, recursive);
        }
        
        /// <summary>
        /// Sets file ownership with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The file path</param>
        /// <param name="ownerId">The owner ID</param>
        /// <param name="groupId">The group ID</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> SetFileOwnershipAsync(
            this VirtualFileSystem fileSystem,
            string path,
            int ownerId,
            int groupId,
            User user)
        {
            return await fileSystem.SetFileOwnershipSecurelyAsync(path, ownerId, groupId, user);
        }
        
        /// <summary>
        /// Sets directory ownership with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The directory path</param>
        /// <param name="ownerId">The owner ID</param>
        /// <param name="groupId">The group ID</param>
        /// <param name="user">The user performing the operation</param>
        /// <param name="recursive">Whether to set recursively</param>
        /// <returns>True if successful; otherwise, false</returns>
        public static async Task<bool> SetDirectoryOwnershipAsync(
            this VirtualFileSystem fileSystem,
            string path,
            int ownerId,
            int groupId,
            User user,
            bool recursive = false)
        {
            return await fileSystem.SetDirectoryOwnershipSecurelyAsync(path, ownerId, groupId, user, recursive);
        }
        
        /// <summary>
        /// Reads a file with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The file path</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>The file content and security result</returns>
        public static async Task<SecureFileReadResult> ReadFileAsync(
            this VirtualFileSystem fileSystem,
            string path,
            User user)
        {
            return await fileSystem.ReadFileSecureAsync(path, user);
        }
        
        /// <summary>
        /// Reads a file as a string with secure operations
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The file path</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>The file content as a string and security result</returns>
        public static async Task<string> ReadFileTextAsync(
            this VirtualFileSystem fileSystem,
            string path,
            User user)
        {
            var result = await fileSystem.ReadFileSecureAsync(path, user);
            if (result.Success && result.Content != null)
            {
                return System.Text.Encoding.UTF8.GetString(result.Content);
            }
            
            return null;
        }
        
        /// <summary>
        /// Performs a security check on a path
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="path">The path to check</param>
        /// <param name="operation">The operation to check</param>
        /// <param name="user">The user performing the operation</param>
        /// <returns>Security check result</returns>
        public static async Task<SecurityCheckResult> CheckSecurityAsync(
            this VirtualFileSystem fileSystem,
            string path,
            string operation,
            User user)
        {
            return await fileSystem.CheckSecurityAsync(path, operation, user);
        }
    }
}
