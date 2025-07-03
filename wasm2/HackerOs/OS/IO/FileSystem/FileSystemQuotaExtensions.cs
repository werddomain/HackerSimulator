using System;
using System.Threading.Tasks;
using HackerOs.OS.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Extension methods for integrating quota checks and updates with file system operations
    /// </summary>
    public static class FileSystemQuotaExtensions
    {
        /// <summary>
        /// Checks if a file creation or modification operation would exceed quota limits
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the file to check</param>
        /// <param name="sizeInBytes">Size of the operation in bytes</param>
        /// <param name="user">User performing the operation</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <returns>True if the operation is allowed; otherwise, false</returns>
        public static async Task<bool> CheckQuotaForOperationAsync(
            this VirtualFileSystem fileSystem,
            string path,
            long sizeInBytes,
            User user,
            GroupQuotaManager quotaManager)
        {
            // Get the target node to determine ownership
            var node = await fileSystem.GetNodeAsync(path);
            
            // If the node doesn't exist yet, get the parent directory for ownership info
            if (node == null)
            {
                var parentPath = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(parentPath))
                {
                    parentPath = "/";
                }
                
                node = await fileSystem.GetNodeAsync(parentPath);
                if (node == null)
                {
                    // Can't determine the owner, use the user's primary group
                    return await quotaManager.CheckQuotaAsync(user.PrimaryGroupId, sizeInBytes);
                }
            }
            
            // Check quota for the group that owns the file/directory
            return await quotaManager.CheckQuotaAsync(node.GroupId, sizeInBytes);
        }

        /// <summary>
        /// Updates quota usage after a file operation
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">Path of the file</param>
        /// <param name="sizeInBytes">Size change in bytes (positive for increase, negative for decrease)</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task UpdateQuotaUsageAsync(
            this VirtualFileSystem fileSystem,
            string path,
            long sizeInBytes,
            GroupQuotaManager quotaManager)
        {
            var node = await fileSystem.GetNodeAsync(path);
            if (node == null)
            {
                // Node doesn't exist, cannot update quota
                return;
            }
            
            // Update the quota for the group that owns the file
            await quotaManager.UpdateUsageAsync(node.GroupId, sizeInBytes);
        }

        /// <summary>
        /// Calculates the total size of a directory and its contents
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="directoryPath">Path to the directory</param>
        /// <returns>Total size in bytes</returns>
        public static async Task<long> CalculateDirectorySizeAsync(
            this VirtualFileSystem fileSystem,
            string directoryPath)
        {
            long totalSize = 0;
            
            if (!await fileSystem.DirectoryExistsAsync(directoryPath))
            {
                return 0;
            }
            
            var contents = await fileSystem.GetDirectoryContentsAsync(directoryPath);
            foreach (var item in contents)
            {
                if (item.IsDirectory)
                {
                    totalSize += await CalculateDirectorySizeAsync(fileSystem, 
                        System.IO.Path.Combine(directoryPath, item.Name).Replace('\\', '/'));
                }
                else
                {
                    totalSize += item.Size;
                }
            }
            
            return totalSize;
        }

        /// <summary>
        /// Rebuilds quota usage statistics for all groups
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task RebuildQuotaUsageAsync(
            this VirtualFileSystem fileSystem,
            GroupQuotaManager quotaManager)
        {
            // Reset all usage counters
            await quotaManager.ResetAllUsageStatisticsAsync();
            
            // Calculate usage for each group's home directory
            var groups = await quotaManager.GetAllGroupsWithQuotasAsync();
            foreach (var groupId in groups)
            {
                string groupHomePath = $"/home/group{groupId}";
                if (await fileSystem.DirectoryExistsAsync(groupHomePath))
                {
                    long size = await CalculateDirectorySizeAsync(fileSystem, groupHomePath);
                    await quotaManager.SetUsageAsync(groupId, size);
                }
            }
        }

        /// <summary>
        /// Checks if a quota is in effect for a specific path
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">The path to check</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <returns>True if the path is subject to quota enforcement; otherwise, false</returns>
        public static async Task<bool> IsPathQuotaEnforcedAsync(
            this VirtualFileSystem fileSystem,
            string path,
            GroupQuotaManager quotaManager)
        {
            // Get the node to determine ownership
            var node = await fileSystem.GetNodeAsync(path);
            if (node == null)
            {
                return false;
            }
            
            // Check if the group has a quota configuration
            return await quotaManager.HasQuotaConfigurationAsync(node.GroupId);
        }

        /// <summary>
        /// Gets the available space for a group based on its quota
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="groupId">The group ID</param>
        /// <param name="quotaManager">Group quota manager instance</param>
        /// <returns>Available space in bytes, or -1 if no quota is set</returns>
        public static async Task<long> GetAvailableSpaceForGroupAsync(
            this VirtualFileSystem fileSystem,
            int groupId,
            GroupQuotaManager quotaManager)
        {
            return await quotaManager.GetAvailableSpaceAsync(groupId);
        }
    }
}
