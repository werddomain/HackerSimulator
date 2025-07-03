using System;
using HackerOs.OS.User;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Utility class for handling permission inheritance in the file system.
    /// Provides methods to calculate appropriate permissions for new files and directories.
    /// </summary>
    public static class InheritPermissions
    {
        /// <summary>
        /// Default mode for regular files (rw-rw-r--) - 0664
        /// </summary>
        public const int DefaultFileMode = 436; // Decimal for octal 0664
        
        /// <summary>
        /// Default mode for directories (rwxrwxr-x) - 0775
        /// </summary>
        public const int DefaultDirectoryMode = 509; // Decimal for octal 0775
        
        /// <summary>
        /// Calculates the permissions for a new file or directory based on parent directory,
        /// user's umask, and inheritance rules.
        /// </summary>
        /// <param name="parentDir">The parent directory</param>
        /// <param name="user">The user creating the file/directory</param>
        /// <param name="isDirectory">Whether this is a directory (true) or file (false)</param>
        /// <returns>The calculated FilePermissions object</returns>
        public static FilePermissions Calculate(
            VirtualDirectory parentDir, 
            UserEntity user,
            bool isDirectory)
        {
            // Start with default permissions based on whether it's a file or directory
            int baseMode = isDirectory ? DefaultDirectoryMode : DefaultFileMode;
            var basePermissions = new FilePermissions(baseMode);
            
            // Get user's umask
            int umask = EffectivePermissions.GetUserUmask(user);
            
            // Apply umask (bitwise AND with inverted umask)
            int effectiveMode = baseMode & ~umask;
            var effectivePermissions = new FilePermissions(effectiveMode);
            
            // Handle SetGID inheritance
            if (isDirectory && parentDir.Permissions.SetGID)
            {
                // If parent directory has SetGID, new directory inherits it
                effectivePermissions.SetGID = true;
            }
            
            return effectivePermissions;
        }
        
        /// <summary>
        /// Gets the appropriate group ID for a new file or directory,
        /// taking into account SetGID inheritance.
        /// </summary>
        /// <param name="parentDir">The parent directory</param>
        /// <param name="user">The user creating the file/directory</param>
        /// <param name="isDirectory">Whether this is a directory</param>
        /// <returns>The group ID to use</returns>
        public static int GetEffectiveGroupId(
            VirtualDirectory parentDir,
            UserEntity user,
            bool isDirectory)
        {
            // If parent directory has SetGID and this is a directory, 
            // inherit the parent's group ID
            if (parentDir.Permissions.SetGID)
            {
                return parentDir.GroupId;
            }
            
            // Otherwise use the user's primary group
            return user.PrimaryGroupId;
        }
        
        /// <summary>
        /// Creates the appropriate permissions string representation (e.g., "rwxr-xr-x")
        /// for a new file or directory.
        /// </summary>
        /// <param name="parentDir">The parent directory</param>
        /// <param name="user">The user creating the file/directory</param>
        /// <param name="isDirectory">Whether this is a directory</param>
        /// <returns>A string representing the permissions</returns>
        public static string GetPermissionString(
            VirtualDirectory parentDir,
            UserEntity user,
            bool isDirectory)
        {
            var permissions = Calculate(parentDir, user, isDirectory);
            return permissions.ToString();
        }
    }
}
