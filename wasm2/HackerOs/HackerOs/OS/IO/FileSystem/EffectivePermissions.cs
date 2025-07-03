using System;
using HackerOs.OS.User;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Helper class for calculating effective permissions based on file permissions,
    /// user context, umask, and directory inheritance.
    /// </summary>
    public class EffectivePermissions
    {
        // Default umask (022 - removes write permission for group and others)
        private const int DefaultUmask = 18; // Octal 022

        /// <summary>
        /// Gets the effective permission bits for a new file or directory.
        /// </summary>
        /// <param name="basePermissions">Base permissions (e.g., 666 for files, 777 for directories)</param>
        /// <param name="umask">User's umask value</param>
        /// <param name="parentDir">Parent directory for inheritance (optional)</param>
        /// <param name="isDirectory">Whether this is for a directory</param>
        /// <returns>Effective FilePermissions object</returns>
        public static FilePermissions CalculateEffectivePermissions(
            FilePermissions basePermissions,
            int umask = DefaultUmask,
            VirtualDirectory? parentDir = null,
            bool isDirectory = false)
        {
            // Start with the base permissions
            int permissionBits = basePermissions.ToOctal();
            
            // Apply umask (bitwise AND with inverted umask)
            int effectivePermissionBits = permissionBits & ~umask;
            
            // Special case: If parent has setgid bit set, inherit it for directories
            if (isDirectory && parentDir != null && parentDir.Permissions.SetGID)
            {
                // Set the setgid bit in the effective permissions (octal 2000)
                effectivePermissionBits |= 1024;
            }
            
            return new FilePermissions(effectivePermissionBits);
        }

        /// <summary>
        /// Calculates the effective permissions for a file or directory considering
        /// user identity, group membership, and special bits.
        /// </summary>
        /// <param name="node">The file system node</param>
        /// <param name="user">The user accessing the node</param>
        /// <returns>A FileAccessType value representing the effective permissions</returns>
        public static FileAccessType CalculateEffectiveAccess(
            VirtualFileSystemNode node,
            UserEntity user)
        {
            // Root always has full access
            if (user.UserId == 0)
            {
                return FileAccessType.ReadWriteExecute;
            }
            
            bool canRead = false;
            bool canWrite = false;
            bool canExecute = false;
            
            // Check user, group, and other permissions
            if (node.OwnerId == user.UserId)
            {
                // User is the owner
                canRead = node.Permissions.OwnerRead;
                canWrite = node.Permissions.OwnerWrite;
                canExecute = node.Permissions.OwnerExecute;
            }
            else if (user.PrimaryGroupId == node.GroupId || user.AdditionalGroups.Contains(node.GroupId))
            {
                // User is in the file's group
                canRead = node.Permissions.GroupRead;
                canWrite = node.Permissions.GroupWrite;
                canExecute = node.Permissions.GroupExecute;
            }
            else
            {
                // User is "other"
                canRead = node.Permissions.OtherRead;
                canWrite = node.Permissions.OtherWrite;
                canExecute = node.Permissions.OtherExecute;
            }
            
            // Consider setuid/setgid for executable files
            if (canExecute && !node.IsDirectory)
            {
                // For setuid executables, execution happens with owner's privileges
                if (node.Permissions.SetUID)
                {
                    // We already determined execute permission, so no change needed here
                    // The actual privilege elevation happens at execution time
                }
                
                // For setgid executables, execution happens with group's privileges
                if (node.Permissions.SetGID)
                {
                    // We already determined execute permission, so no change needed here
                    // The actual privilege elevation happens at execution time
                }
            }
            
            // Return the appropriate FileAccessType
            if (canRead && canWrite && canExecute)
                return FileAccessType.ReadWriteExecute;
            if (canRead && canWrite)
                return FileAccessType.ReadWrite;
            if (canRead && canExecute)
                return FileAccessType.ReadExecute;
            if (canWrite && canExecute)
                return FileAccessType.WriteExecute;
            if (canRead)
                return FileAccessType.Read;
            if (canWrite)
                return FileAccessType.Write;
            if (canExecute)
                return FileAccessType.Execute;
                
            return 0; // No access
        }

        /// <summary>
        /// Calculates the umask for a user based on their default settings.
        /// </summary>
        /// <param name="user">The user</param>
        /// <returns>The user's umask value</returns>
        public static int GetUserUmask(UserEntity user)
        {
            // In a real implementation, this would be stored in user preferences
            // For now, return a default value based on user type
            if (user.UserId == 0)
            {
                return 18; // Root: 022 (rwxr-xr-x)
            }
            else if (user.IsAdmin)
            {
                return 18; // Admin: 022 (rwxr-xr-x)
            }
            else
            {
                return 2; // Regular user: 002 (rwxrwxr-x)
            }
        }
    }
}
