using System;
using System.Threading.Tasks;
using HackerOs.OS.User;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Utility class for handling sticky bit behavior in the file system.
    /// Provides methods to check permissions according to sticky bit rules.
    /// </summary>
    public static class StickyBitHelper
    {
        /// <summary>
        /// Checks if a user can delete or rename a file in a directory, considering sticky bit rules.
        /// </summary>
        /// <param name="directory">The directory containing the file</param>
        /// <param name="file">The file to check</param>
        /// <param name="user">The user attempting the operation</param>
        /// <returns>True if the operation is allowed; otherwise, false</returns>
        public static bool CanModifyInDirectory(VirtualDirectory directory, VirtualFileSystemNode file, UserEntity user)
        {
            // Root user can always modify files
            if (user.UserId == 0)
            {
                return true;
            }
            
            // If sticky bit is not set, just check write permission on the directory
            if (!directory.Permissions.Sticky)
            {
                return directory.CanAccess(user, FileAccessMode.Write);
            }
            
            // With sticky bit set, user must be one of:
            // 1. Owner of the file
            // 2. Owner of the directory
            // 3. Root user (already checked)
            bool isFileOwner = file.OwnerId == user.UserId;
            bool isDirOwner = directory.OwnerId == user.UserId;
            
            return isFileOwner || isDirOwner;
        }
        
        /// <summary>
        /// Checks if a user can delete a directory, considering sticky bit rules.
        /// </summary>
        /// <param name="parentDir">The parent directory</param>
        /// <param name="directory">The directory to check</param>
        /// <param name="user">The user attempting the deletion</param>
        /// <returns>True if deletion is allowed; otherwise, false</returns>
        public static bool CanDeleteDirectory(VirtualDirectory parentDir, VirtualDirectory directory, UserEntity user)
        {
            // Apply the same rules as for files
            return CanModifyInDirectory(parentDir, directory, user);
        }
        
        /// <summary>
        /// Verifies if a user has permission to move or rename a file in a sticky bit directory.
        /// </summary>
        /// <param name="sourceDir">Source directory</param>
        /// <param name="targetDir">Target directory</param>
        /// <param name="node">Node being moved</param>
        /// <param name="user">User attempting the operation</param>
        /// <returns>True if the operation is allowed; otherwise, false</returns>
        public static bool CanMoveOrRename(
            VirtualDirectory sourceDir,
            VirtualDirectory targetDir, 
            VirtualFileSystemNode node, 
            UserEntity user)
        {
            // Root can always move/rename
            if (user.UserId == 0)
            {
                return true;
            }
            
            // Check source directory sticky bit rules
            bool canRemoveFromSource = !sourceDir.Permissions.Sticky || 
                                       node.OwnerId == user.UserId || 
                                       sourceDir.OwnerId == user.UserId;
                                       
            // Check target directory write permission
            bool canWriteToTarget = targetDir.CanAccess(user, FileAccessMode.Write);
            
            return canRemoveFromSource && canWriteToTarget;
        }
        
        /// <summary>
        /// Updates a file system node's permissions to set the sticky bit.
        /// </summary>
        /// <param name="node">The node to update</param>
        /// <param name="enable">Whether to enable or disable the sticky bit</param>
        public static void SetStickyBit(VirtualFileSystemNode node, bool enable = true)
        {
            if (node.IsDirectory)
            {
                var permissions = node.Permissions;
                permissions.Sticky = enable;
                node.Permissions = permissions;
            }
        }
        
        /// <summary>
        /// Creates a standard temp directory with sticky bit set.
        /// </summary>
        /// <param name="fs">The file system</param>
        /// <param name="path">Path where to create the directory (usually "/tmp")</param>
        /// <param name="permissions">Base permissions (usually 777)</param>
        /// <returns>True if created successfully; otherwise, false</returns>
        public static async Task<bool> CreateTempDirectoryAsync(
            VirtualFileSystem fs, 
            string path = "/tmp",
            int permissions = 0777)
        {
            try
            {
                // Create directory if it doesn't exist
                var node = fs.GetNode(path);
                if (node == null)
                {
                    // Create with root ownership
                    var rootUser = new UserEntity { UserId = 0, Username = "root", PrimaryGroupId = 0 };
                    await fs.CreateDirectoryAsync(path, rootUser);
                    
                    // Get the newly created directory
                    node = fs.GetNode(path);
                    if (node == null || !node.IsDirectory)
                    {
                        return false;
                    }
                }
                else if (!node.IsDirectory)
                {
                    return false;
                }
                
                // Set permissions and sticky bit
                var directory = (VirtualDirectory)node;
                directory.Permissions = new FilePermissions(permissions);
                directory.Permissions.Sticky = true;
                
                // Ensure root ownership
                directory.Owner = "0";
                directory.Group = "0";
                
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
