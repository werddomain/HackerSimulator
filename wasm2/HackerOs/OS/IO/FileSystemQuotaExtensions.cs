using System;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;

namespace HackerOs.OS.IO
{
    /// <summary>
    /// Provides extension methods for the VirtualFileSystem to integrate with the quota system
    /// </summary>
    public static class FileSystemQuotaExtensions
    {
        /// <summary>
        /// Checks if a file operation would exceed the group's quota
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="groupId">The group ID</param>
        /// <param name="sizeInBytes">The additional size in bytes</param>
        /// <param name="quotaManager">The quota manager</param>
        /// <returns>True if the operation is allowed; otherwise, false</returns>
        public static async Task<bool> CheckQuotaAsync(
            this VirtualFileSystem fileSystem,
            int groupId,
            long sizeInBytes,
            GroupQuotaManager quotaManager)
        {
            if (quotaManager == null)
                return true;

            return await quotaManager.CheckQuotaAsync(groupId, sizeInBytes);
        }

        /// <summary>
        /// Updates the usage statistics for a group after a file operation
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="groupId">The group ID</param>
        /// <param name="sizeInBytes">The size change in bytes (positive for increase, negative for decrease)</param>
        /// <param name="quotaManager">The quota manager</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task UpdateQuotaUsageAsync(
            this VirtualFileSystem fileSystem,
            int groupId,
            long sizeInBytes,
            GroupQuotaManager quotaManager)
        {
            if (quotaManager == null)
                return;

            await quotaManager.UpdateUsageAsync(groupId, sizeInBytes);
        }

        /// <summary>
        /// Checks quota before a file creation operation
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">The file path</param>
        /// <param name="content">The file content</param>
        /// <param name="user">The user performing the operation</param>
        /// <param name="quotaManager">The quota manager</param>
        /// <returns>True if the operation is allowed; otherwise, false</returns>
        public static async Task<bool> CheckQuotaForCreateAsync(
            this VirtualFileSystem fileSystem,
            string path,
            byte[] content,
            User.User user,
            GroupQuotaManager quotaManager)
        {
            if (quotaManager == null || user == null || content == null)
                return true;

            // Determine the group for the new file
            string dirPath = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "/";
            var parentDir = await fileSystem.GetDirectoryAsync(dirPath);
            if (parentDir == null)
                return true;

            // Use the user's primary group or the directory's group if SetGID
            int groupId = parentDir.Permissions.SetGID
                ? int.TryParse(parentDir.Group, out int dirGroupId) ? dirGroupId : user.PrimaryGroupId
                : user.PrimaryGroupId;

            // Check if the operation would exceed quota
            return await quotaManager.CheckQuotaAsync(groupId, content.Length);
        }

        /// <summary>
        /// Updates quota usage after a file creation operation
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">The file path</param>
        /// <param name="size">The file size</param>
        /// <param name="quotaManager">The quota manager</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task UpdateQuotaForCreateAsync(
            this VirtualFileSystem fileSystem,
            string path,
            long size,
            GroupQuotaManager quotaManager)
        {
            if (quotaManager == null || size <= 0)
                return;

            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || node.IsDirectory)
                return;

            // Get the group ID
            if (int.TryParse(node.Group, out int groupId))
            {
                // Update group usage
                await quotaManager.UpdateUsageAsync(groupId, size);
            }
        }

        /// <summary>
        /// Checks quota before a file modification operation
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">The file path</param>
        /// <param name="newContent">The new file content</param>
        /// <param name="user">The user performing the operation</param>
        /// <param name="quotaManager">The quota manager</param>
        /// <returns>True if the operation is allowed; otherwise, false</returns>
        public static async Task<bool> CheckQuotaForModifyAsync(
            this VirtualFileSystem fileSystem,
            string path,
            byte[] newContent,
            User.User user,
            GroupQuotaManager quotaManager)
        {
            if (quotaManager == null || user == null || newContent == null)
                return true;

            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || node.IsDirectory)
                return true;

            // Get the group ID
            if (!int.TryParse(node.Group, out int groupId))
                return true;

            // Calculate size change
            long sizeChange = newContent.Length - node.Size;
            if (sizeChange <= 0)
                return true; // No need to check quota if size decreases or stays the same

            // Check if the operation would exceed quota
            return await quotaManager.CheckQuotaAsync(groupId, sizeChange);
        }

        /// <summary>
        /// Updates quota usage after a file modification operation
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">The file path</param>
        /// <param name="oldSize">The old file size</param>
        /// <param name="newSize">The new file size</param>
        /// <param name="quotaManager">The quota manager</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task UpdateQuotaForModifyAsync(
            this VirtualFileSystem fileSystem,
            string path,
            long oldSize,
            long newSize,
            GroupQuotaManager quotaManager)
        {
            if (quotaManager == null || oldSize == newSize)
                return;

            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || node.IsDirectory)
                return;

            // Get the group ID
            if (int.TryParse(node.Group, out int groupId))
            {
                // Calculate size change
                long sizeChange = newSize - oldSize;
                
                // Update group usage
                await quotaManager.UpdateUsageAsync(groupId, sizeChange);
            }
        }

        /// <summary>
        /// Updates quota usage after a file deletion operation
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="path">The file path</param>
        /// <param name="size">The file size</param>
        /// <param name="group">The file's group</param>
        /// <param name="quotaManager">The quota manager</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task UpdateQuotaForDeleteAsync(
            this VirtualFileSystem fileSystem,
            string path,
            long size,
            string group,
            GroupQuotaManager quotaManager)
        {
            if (quotaManager == null || size <= 0 || string.IsNullOrEmpty(group))
                return;

            // Get the group ID
            if (int.TryParse(group, out int groupId))
            {
                // Update group usage (negative to decrease)
                await quotaManager.UpdateUsageAsync(groupId, -size);
            }
        }
    }
}
