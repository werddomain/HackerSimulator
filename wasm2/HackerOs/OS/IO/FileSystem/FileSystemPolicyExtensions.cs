using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Extension methods for integrating policy checks with file system operations
    /// </summary>
    public static class FileSystemPolicyExtensions
    {
        /// <summary>
        /// Checks if a file system operation is allowed by group policies
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the file or directory</param>
        /// <param name="operation">The operation (e.g., "read", "write", "delete")</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <returns>A PolicyResult indicating whether the operation is allowed</returns>
        public static async Task<PolicyResult> CheckPolicyForOperationAsync(
            this VirtualFileSystem fileSystem,
            string path,
            string operation,
            User user,
            GroupPolicyManager policyManager)
        {
            // Create policy context with operation details
            var context = new PolicyContext
            {
                UserId = user.UserId,
                Resource = path,
                Operation = operation,
                Data = new Dictionary<string, object>
                {
                    { "path", path },
                    { "operation", operation }
                }
            };
            
            // Add file metadata if file exists
            var node = await fileSystem.GetNodeAsync(path);
            if (node != null)
            {
                context.Data["isDirectory"] = node.IsDirectory;
                context.Data["size"] = node.Size;
                context.Data["ownerId"] = node.OwnerId;
                context.Data["groupId"] = node.GroupId;
                context.Data["permissions"] = node.Permissions.ToOctalString();
                
                // Add special context for operations
                if (operation == "delete" && node.IsDirectory)
                {
                    // Check if directory is empty
                    var contents = await fileSystem.GetDirectoryContentsAsync(path);
                    context.Data["isEmpty"] = !contents.GetEnumerator().MoveNext();
                }
            }
            
            // If we're checking a new file in a directory, add parent directory context
            if (node == null && operation == "create")
            {
                var parentPath = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(parentPath))
                {
                    parentPath = "/";
                }
                
                var parentNode = await fileSystem.GetNodeAsync(parentPath);
                if (parentNode != null)
                {
                    context.Data["parentPath"] = parentPath;
                    context.Data["parentOwnerId"] = parentNode.OwnerId;
                    context.Data["parentGroupId"] = parentNode.GroupId;
                    context.Data["parentPermissions"] = parentNode.Permissions.ToOctalString();
                }
            }
            
            // Evaluate policies
            return await policyManager.EvaluatePoliciesAsync(user, context);
        }

        /// <summary>
        /// Creates a standard security policy for a group
        /// </summary>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <param name="groupId">The group ID</param>
        /// <param name="name">The policy name</param>
        /// <param name="description">The policy description</param>
        /// <param name="blockedPaths">List of paths that are blocked for this group</param>
        /// <returns>The created policy</returns>
        public static async Task<GroupPolicy> CreateSecurityPolicyAsync(
            this GroupPolicyManager policyManager,
            int groupId,
            string name,
            string description,
            List<string> blockedPaths)
        {
            var policyData = new Dictionary<string, object>
            {
                { "blockedPaths", blockedPaths }
            };
            
            return await policyManager.CreatePolicyAsync(
                "SecurityPolicy",
                groupId,
                name,
                description,
                PolicyScope.GroupOnly,
                PolicyPriority.High,
                policyData);
        }

        /// <summary>
        /// Creates a file system policy for read-only paths
        /// </summary>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <param name="groupId">The group ID</param>
        /// <param name="name">The policy name</param>
        /// <param name="description">The policy description</param>
        /// <param name="readOnlyPaths">List of paths that are read-only for this group</param>
        /// <returns>The created policy</returns>
        public static async Task<GroupPolicy> CreateReadOnlyPathsPolicyAsync(
            this GroupPolicyManager policyManager,
            int groupId,
            string name,
            string description,
            List<string> readOnlyPaths)
        {
            var policyData = new Dictionary<string, object>
            {
                { "readOnlyPaths", readOnlyPaths }
            };
            
            return await policyManager.CreatePolicyAsync(
                "FileSystemPolicy",
                groupId,
                name,
                description,
                PolicyScope.GroupOnly,
                PolicyPriority.Medium,
                policyData);
        }

        /// <summary>
        /// Creates a resource policy for maximum file size
        /// </summary>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <param name="groupId">The group ID</param>
        /// <param name="name">The policy name</param>
        /// <param name="description">The policy description</param>
        /// <param name="maxFileSize">Maximum allowed file size in bytes</param>
        /// <returns>The created policy</returns>
        public static async Task<GroupPolicy> CreateMaxFileSizePolicyAsync(
            this GroupPolicyManager policyManager,
            int groupId,
            string name,
            string description,
            long maxFileSize)
        {
            var policyData = new Dictionary<string, object>
            {
                { "maxFileSize", maxFileSize }
            };
            
            return await policyManager.CreatePolicyAsync(
                "ResourcePolicy",
                groupId,
                name,
                description,
                PolicyScope.GroupOnly,
                PolicyPriority.Medium,
                policyData);
        }

        /// <summary>
        /// Creates an access policy for time-based restrictions
        /// </summary>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <param name="groupId">The group ID</param>
        /// <param name="name">The policy name</param>
        /// <param name="description">The policy description</param>
        /// <param name="allowedHours">List of hours (0-23) when access is allowed</param>
        /// <returns>The created policy</returns>
        public static async Task<GroupPolicy> CreateTimeBasedAccessPolicyAsync(
            this GroupPolicyManager policyManager,
            int groupId,
            string name,
            string description,
            List<int> allowedHours)
        {
            var policyData = new Dictionary<string, object>
            {
                { "allowedHours", allowedHours }
            };
            
            return await policyManager.CreatePolicyAsync(
                "AccessPolicy",
                groupId,
                name,
                description,
                PolicyScope.GroupOnly,
                PolicyPriority.High,
                policyData);
        }

        /// <summary>
        /// Gets all policies affecting a path for a user
        /// </summary>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <param name="path">The file system path</param>
        /// <param name="user">The user</param>
        /// <returns>List of policies affecting the path</returns>
        public static async Task<List<GroupPolicy>> GetPoliciesForPathAsync(
            this GroupPolicyManager policyManager,
            string path,
            User user)
        {
            return await policyManager.GetPoliciesForResourceAsync(path, user);
        }

        /// <summary>
        /// Creates a default set of policies for a new group
        /// </summary>
        /// <param name="policyManager">Group policy manager instance</param>
        /// <param name="groupId">The group ID</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task CreateDefaultPoliciesForGroupAsync(
            this GroupPolicyManager policyManager,
            int groupId)
        {
            // Create a read-only policy for system files
            await CreateReadOnlyPathsPolicyAsync(
                policyManager,
                groupId,
                "System Files Protection",
                "Prevents modification of critical system files",
                new List<string> { "/bin", "/boot", "/lib", "/sbin", "/etc/passwd", "/etc/group" });
            
            // Create a resource policy for maximum file size
            await CreateMaxFileSizePolicyAsync(
                policyManager,
                groupId,
                "Max File Size Limit",
                "Limits the maximum size of files that can be created",
                100 * 1024 * 1024); // 100 MB
            
            // Create time-based access policy (allow access 24/7)
            await CreateTimeBasedAccessPolicyAsync(
                policyManager,
                groupId,
                "Default Access Hours",
                "Default time-based access policy",
                new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 });
        }
    }
}
