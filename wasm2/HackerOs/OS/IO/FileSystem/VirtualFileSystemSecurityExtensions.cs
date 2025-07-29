using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Extension methods for integrating secure operations with VirtualFileSystem
    /// </summary>
    public static class VirtualFileSystemSecurityExtensions
    {
        /// <summary>
        /// Security context for the virtual file system
        /// </summary>
        private static class SecurityContext
        {
            /// <summary>
            /// The quota manager instance
            /// </summary>
            public static GroupQuotaManager QuotaManager { get; set; }
            
            /// <summary>
            /// The policy manager instance
            /// </summary>
            public static GroupPolicyManager PolicyManager { get; set; }
            
            /// <summary>
            /// Whether security is initialized
            /// </summary>
            public static bool IsInitialized => QuotaManager != null && PolicyManager != null;
            
            /// <summary>
            /// Initializes the security context
            /// </summary>
            /// <param name="quotaManager">The quota manager</param>
            /// <param name="policyManager">The policy manager</param>
            public static void Initialize(GroupQuotaManager quotaManager, GroupPolicyManager policyManager)
            {
                QuotaManager = quotaManager ?? throw new ArgumentNullException(nameof(quotaManager));
                PolicyManager = policyManager ?? throw new ArgumentNullException(nameof(policyManager));
            }
        }
        
        /// <summary>
        /// Initializes security for the virtual file system
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="quotaManager">The quota manager to use</param>
        /// <param name="policyManager">The policy manager to use</param>
        public static void InitializeSecurity(
            this VirtualFileSystem fileSystem,
            GroupQuotaManager quotaManager,
            GroupPolicyManager policyManager)
        {
            SecurityContext.Initialize(quotaManager, policyManager);
        }
        
        /// <summary>
        /// Creates a file securely using the integrated security framework
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the file to create</param>
        /// <param name="content">Initial content of the file</param>
        /// <param name="permissions">File permissions</param>
        /// <param name="user">User performing the operation</param>
        /// <returns>True if the file was created; otherwise, false</returns>
        public static async Task<bool> CreateFileSecurelyAsync(
            this VirtualFileSystem fileSystem,
            string path,
            string content,
            FilePermissions permissions,
            User user)
        {
            EnsureSecurityInitialized();
            
            var result = await fileSystem.CreateFileSecureAsync(
                path, content, permissions, user, 
                SecurityContext.QuotaManager, SecurityContext.PolicyManager);
            
            return result.Success;
        }
        
        /// <summary>
        /// Writes to a file securely using the integrated security framework
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the file to write</param>
        /// <param name="content">Content to write</param>
        /// <param name="user">User performing the operation</param>
        /// <returns>True if the file was written; otherwise, false</returns>
        public static async Task<bool> WriteFileSecurelyAsync(
            this VirtualFileSystem fileSystem,
            string path,
            string content,
            User user)
        {
            EnsureSecurityInitialized();
            
            var result = await fileSystem.WriteFileSecureAsync(
                path, content, user, 
                SecurityContext.QuotaManager, SecurityContext.PolicyManager);
            
            return result.Success;
        }
        
        /// <summary>
        /// Deletes a file securely using the integrated security framework
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the file to delete</param>
        /// <param name="user">User performing the operation</param>
        /// <returns>True if the file was deleted; otherwise, false</returns>
        public static async Task<bool> DeleteFileSecurelyAsync(
            this VirtualFileSystem fileSystem,
            string path,
            User user)
        {
            EnsureSecurityInitialized();
            
            var result = await fileSystem.DeleteFileSecureAsync(
                path, user, 
                SecurityContext.QuotaManager, SecurityContext.PolicyManager);
            
            return result.Success;
        }
        
        /// <summary>
        /// Creates a directory securely using the integrated security framework
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the directory to create</param>
        /// <param name="permissions">Directory permissions</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="recursive">Whether to create parent directories if they don't exist</param>
        /// <returns>True if the directory was created; otherwise, false</returns>
        public static async Task<bool> CreateDirectorySecurelyAsync(
            this VirtualFileSystem fileSystem,
            string path,
            FilePermissions permissions,
            User user,
            bool recursive = false)
        {
            EnsureSecurityInitialized();
            
            var result = await fileSystem.CreateDirectorySecureAsync(
                path, permissions, user, 
                SecurityContext.QuotaManager, SecurityContext.PolicyManager,
                recursive);
            
            return result.Success;
        }
        
        /// <summary>
        /// Deletes a directory securely using the integrated security framework
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the directory to delete</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="recursive">Whether to delete directory contents recursively</param>
        /// <returns>True if the directory was deleted; otherwise, false</returns>
        public static async Task<bool> DeleteDirectorySecurelyAsync(
            this VirtualFileSystem fileSystem,
            string path,
            User user,
            bool recursive = false)
        {
            EnsureSecurityInitialized();
            
            var result = await fileSystem.DeleteDirectorySecureAsync(
                path, user, 
                SecurityContext.QuotaManager, SecurityContext.PolicyManager,
                recursive);
            
            return result.Success;
        }
        
        /// <summary>
        /// Moves a directory securely using the integrated security framework
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="sourcePath">Source directory path</param>
        /// <param name="destinationPath">Destination directory path</param>
        /// <param name="user">User performing the operation</param>
        /// <returns>True if the directory was moved; otherwise, false</returns>
        public static async Task<bool> MoveDirectorySecurelyAsync(
            this VirtualFileSystem fileSystem,
            string sourcePath,
            string destinationPath,
            User user)
        {
            EnsureSecurityInitialized();
            
            var result = await fileSystem.MoveDirectorySecureAsync(
                sourcePath, destinationPath, user, 
                SecurityContext.QuotaManager, SecurityContext.PolicyManager);
            
            return result.Success;
        }
        
        /// <summary>
        /// Enumerates directory contents securely using the integrated security framework
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Directory path to enumerate</param>
        /// <param name="user">User performing the operation</param>
        /// <returns>Directory entries if successful; otherwise, empty collection</returns>
        public static async Task<IEnumerable<VirtualFileSystemNode>> EnumerateDirectorySecurelyAsync(
            this VirtualFileSystem fileSystem,
            string path,
            User user)
        {
            EnsureSecurityInitialized();
            
            var result = await fileSystem.EnumerateDirectorySecureAsync(
                path, user, SecurityContext.PolicyManager);
            
            return result.Entries;
        }
        
        /// <summary>
        /// Changes directory permissions securely using the integrated security framework
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Directory path</param>
        /// <param name="permissions">New permissions to set</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="recursive">Whether to apply recursively to contents</param>
        /// <returns>True if permissions were changed; otherwise, false</returns>
        public static async Task<bool> SetDirectoryPermissionsSecurelyAsync(
            this VirtualFileSystem fileSystem,
            string path,
            FilePermissions permissions,
            User user,
            bool recursive = false)
        {
            EnsureSecurityInitialized();
            
            var result = await fileSystem.SetDirectoryPermissionsSecureAsync(
                path, permissions, user, 
                SecurityContext.PolicyManager, recursive);
            
            return result.Success;
        }
        
        /// <summary>
        /// Changes directory ownership securely using the integrated security framework
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Directory path</param>
        /// <param name="ownerId">New owner ID</param>
        /// <param name="groupId">New group ID</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="recursive">Whether to apply recursively to contents</param>
        /// <returns>True if ownership was changed; otherwise, false</returns>
        public static async Task<bool> SetDirectoryOwnershipSecurelyAsync(
            this VirtualFileSystem fileSystem,
            string path,
            int ownerId,
            int groupId,
            User user,
            bool recursive = false)
        {
            EnsureSecurityInitialized();
            
            var result = await fileSystem.SetDirectoryOwnershipSecureAsync(
                path, ownerId, groupId, user, 
                SecurityContext.QuotaManager, SecurityContext.PolicyManager,
                recursive);
            
            return result.Success;
        }
        
        /// <summary>
        /// Gets detailed result of the last security operation
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the file or directory</param>
        /// <param name="operation">The operation (e.g., "read", "write", "delete")</param>
        /// <param name="user">User performing the operation</param>
        /// <returns>A SecurityCheckResult with detailed information</returns>
        public static async Task<SecurityCheckResult> CheckSecurityAsync(
            this VirtualFileSystem fileSystem,
            string path,
            string operation,
            User user)
        {
            EnsureSecurityInitialized();
            
            return await fileSystem.PerformSecurityCheckAsync(
                path, operation, 0, user, 
                SecurityContext.QuotaManager, SecurityContext.PolicyManager);
        }
        
        /// <summary>
        /// Ensures that security is initialized
        /// </summary>
        private static void EnsureSecurityInitialized()
        {
            if (!SecurityContext.IsInitialized)
            {
                throw new InvalidOperationException(
                    "File system security is not initialized. Call InitializeSecurity first.");
            }
        }
    }
}
