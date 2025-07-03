using System;
using System.Threading.Tasks;
using HackerOs.OS.User;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Provides methods for handling SetGID directory behavior for file and directory creation
    /// </summary>
    public static class SetGIDDirectoryExtensions
    {
        /// <summary>
        /// Applies SetGID directory inheritance rules to a new file or directory
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="parentPath">The parent directory path</param>
        /// <param name="newNodePath">The path of the new node</param>
        /// <param name="user">The user creating the node</param>
        /// <param name="isDirectory">Whether the new node is a directory</param>
        /// <returns>True if inheritance was applied; otherwise, false</returns>
        public static async Task<bool> ApplySetGIDInheritanceAsync(
            this IVirtualFileSystem fileSystem,
            string parentPath,
            string newNodePath,
            UserEntity user,
            bool isDirectory)
        {
            // Validate parameters
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            if (string.IsNullOrEmpty(parentPath)) throw new ArgumentNullException(nameof(parentPath));
            if (string.IsNullOrEmpty(newNodePath)) throw new ArgumentNullException(nameof(newNodePath));
            if (user == null) throw new ArgumentNullException(nameof(user));
            
            try
            {
                // Get the parent directory
                var parentNode = await fileSystem.GetNodeAsync(parentPath);
                if (parentNode == null || !parentNode.IsDirectory)
                {
                    return false;
                }
                
                var parentDir = (VirtualDirectory)parentNode;
                
                // Check if the parent directory has SetGID bit set
                if (!parentDir.Permissions.SetGID)
                {
                    return false;
                }
                
                // Get the new node
                var newNode = await fileSystem.GetNodeAsync(newNodePath);
                if (newNode == null)
                {
                    return false;
                }
                
                // Set the group of the new node to match the parent directory
                newNode.GroupId = parentDir.GroupId;
                
                // If the new node is a directory, propagate the SetGID bit
                if (isDirectory && newNode.IsDirectory)
                {
                    var newDir = (VirtualDirectory)newNode;
                    var permissions = newDir.Permissions;
                    permissions.SetGID = true;
                    newDir.Permissions = permissions;
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a directory has the SetGID bit set
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="directoryPath">The directory path to check</param>
        /// <returns>True if the directory has SetGID bit set; otherwise, false</returns>
        public static async Task<bool> DirectoryHasSetGIDAsync(
            this IVirtualFileSystem fileSystem,
            string directoryPath)
        {
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            if (string.IsNullOrEmpty(directoryPath)) throw new ArgumentNullException(nameof(directoryPath));
            
            var node = await fileSystem.GetNodeAsync(directoryPath);
            if (node == null || !node.IsDirectory)
            {
                return false;
            }
            
            var dir = (VirtualDirectory)node;
            return dir.Permissions.SetGID;
        }
        
        /// <summary>
        /// Gets the effective group ID for a new file created in a directory, considering SetGID
        /// </summary>
        /// <param name="fileSystem">The virtual file system</param>
        /// <param name="directoryPath">The directory path</param>
        /// <param name="user">The user creating the file</param>
        /// <returns>The effective group ID to use</returns>
        public static async Task<int> GetEffectiveGroupIdForNewFileAsync(
            this IVirtualFileSystem fileSystem,
            string directoryPath,
            UserEntity user)
        {
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            if (string.IsNullOrEmpty(directoryPath)) throw new ArgumentNullException(nameof(directoryPath));
            if (user == null) throw new ArgumentNullException(nameof(user));
            
            var node = await fileSystem.GetNodeAsync(directoryPath);
            if (node == null || !node.IsDirectory)
            {
                return user.PrimaryGroupId;
            }
            
            var dir = (VirtualDirectory)node;
            
            // If SetGID is set, use the directory's group ID
            if (dir.Permissions.SetGID)
            {
                return dir.GroupId;
            }
            
            // Otherwise, use the user's primary group ID
            return user.PrimaryGroupId;
        }
    }
}
