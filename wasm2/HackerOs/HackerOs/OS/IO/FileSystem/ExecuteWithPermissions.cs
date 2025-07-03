using System;
using System.Threading.Tasks;
using HackerOs.OS.User;
using UserEntity = HackerOs.OS.User.User;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Helper class for executing code with elevated permissions based on SetUID and SetGID bits.
    /// Provides secure methods for temporary permission elevation during file execution.
    /// </summary>
    public static class ExecuteWithPermissions
    {
        /// <summary>
        /// Executes an action with the permissions of the file's owner if the SetUID bit is set.
        /// </summary>
        /// <typeparam name="T">The return type of the action</typeparam>
        /// <param name="file">The file with SetUID bit</param>
        /// <param name="action">The action to execute with elevated permissions</param>
        /// <param name="user">The user attempting to execute the file</param>
        /// <returns>The result of the action</returns>
        public static async Task<T> ExecuteAsOwnerAsync<T>(VirtualFile file, Func<Task<T>> action, UserEntity user)
        {
            // Verify this is a valid SetUID operation
            if (!CanElevateToOwnerPermissions(file, user))
            {
                throw new UnauthorizedAccessException($"User {user.Username} cannot execute SetUID file {file.FullPath}");
            }
            
            // Get the file's owner user object
            var ownerUserId = file.OwnerId;
            var userManager = UserManager.GetInstance();
            var ownerUser = await userManager.GetUserByIdAsync(ownerUserId);
            
            if (ownerUser == null)
            {
                throw new InvalidOperationException($"Owner with ID {ownerUserId} not found for file {file.FullPath}");
            }
            
            // Create a permission context for the elevation
            using (var context = PermissionContext.ElevateToUser(
                ownerUser, 
                user, 
                file.FullPath, 
                $"SetUID execution of {file.Name}"))
            {
                // Log the elevation
                LogPermissionElevation(file, user, ownerUser, "SetUID");
                
                // Execute the action with elevated permissions
                return await action();
            }
            // The PermissionContext.Dispose() will restore original permissions
        }
        
        /// <summary>
        /// Executes an action with the permissions of the file's group if the SetGID bit is set.
        /// </summary>
        /// <typeparam name="T">The return type of the action</typeparam>
        /// <param name="file">The file with SetGID bit</param>
        /// <param name="action">The action to execute with elevated permissions</param>
        /// <param name="user">The user attempting to execute the file</param>
        /// <returns>The result of the action</returns>
        public static async Task<T> ExecuteAsGroupAsync<T>(VirtualFile file, Func<Task<T>> action, UserEntity user)
        {
            // Verify this is a valid SetGID operation
            if (!CanElevateToGroupPermissions(file, user))
            {
                throw new UnauthorizedAccessException($"User {user.Username} cannot execute SetGID file {file.FullPath}");
            }
            
            // Get the file's group ID
            var groupId = file.GroupId;
            
            // Create a temporary user with the file's group as primary group
            var elevatedUser = new UserEntity
            {
                UserId = user.UserId,
                Username = user.Username,
                PrimaryGroupId = groupId,
                SecondaryGroups = user.SecondaryGroups,
                HomeDirectory = user.HomeDirectory,
                Shell = user.Shell,
                FullName = user.FullName,
                IsAdmin = user.IsAdmin
            };
            
            // Create a permission context for the elevation
            using (var context = PermissionContext.ElevateToUser(
                elevatedUser, 
                user, 
                file.FullPath, 
                $"SetGID execution of {file.Name}"))
            {
                // Log the elevation
                LogPermissionElevation(file, user, elevatedUser, "SetGID");
                
                // Execute the action with elevated permissions
                return await action();
            }
            // The PermissionContext.Dispose() will restore original permissions
        }
        
        /// <summary>
        /// Verifies if a SetUID elevation is permitted.
        /// </summary>
        /// <param name="file">The file to execute</param>
        /// <param name="user">The user attempting execution</param>
        /// <returns>True if elevation is allowed; otherwise, false</returns>
        private static bool CanElevateToOwnerPermissions(VirtualFile file, UserEntity user)
        {
            // If user is already root, no need for elevation
            if (user.UserId == 0)
            {
                return false;
            }
            
            // The file must have SetUID bit set
            if (!file.Permissions.SetUID)
            {
                return false;
            }
            
            // The file must be executable by the user
            if (!file.CanAccess(user, FileAccessMode.Execute))
            {
                return false;
            }
            
            // Prevent elevation to root from non-trusted files
            // This is a security measure to prevent privilege escalation attacks
            if (file.OwnerId == 0 && !IsTrustedExecutable(file))
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Verifies if a SetGID elevation is permitted.
        /// </summary>
        /// <param name="file">The file to execute</param>
        /// <param name="user">The user attempting execution</param>
        /// <returns>True if elevation is allowed; otherwise, false</returns>
        private static bool CanElevateToGroupPermissions(VirtualFile file, UserEntity user)
        {
            // If user is already root, no need for elevation
            if (user.UserId == 0)
            {
                return false;
            }
            
            // The file must have SetGID bit set
            if (!file.Permissions.SetGID)
            {
                return false;
            }
            
            // The file must be executable by the user
            if (!file.CanAccess(user, FileAccessMode.Execute))
            {
                return false;
            }
            
            // User is already a member of the group
            if (file.GroupId == user.PrimaryGroupId || (user.SecondaryGroups != null && user.SecondaryGroups.Contains(file.GroupId)))
            {
                // No need for elevation if user is already in the group
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Checks if a file is a trusted executable for SetUID operations.
        /// This is a security measure to prevent privilege escalation attacks.
        /// </summary>
        /// <param name="file">The file to check</param>
        /// <returns>True if the file is trusted; otherwise, false</returns>
        private static bool IsTrustedExecutable(VirtualFile file)
        {
            // List of trusted system directories
            string[] trustedPaths = new[]
            {
                "/bin/",
                "/sbin/",
                "/usr/bin/",
                "/usr/sbin/",
                "/usr/local/bin/",
                "/usr/local/sbin/"
            };
            
            // Check if the file is in a trusted system directory
            foreach (var path in trustedPaths)
            {
                if (file.FullPath.StartsWith(path))
                {
                    return true;
                }
            }
            
            // Add additional trust verification here if needed
            // For example, checking digital signatures or checksums
            
            return false;
        }
        
        /// <summary>
        /// Logs a permission elevation event for auditing purposes.
        /// </summary>
        private static void LogPermissionElevation(
            VirtualFile file, 
            UserEntity originalUser, 
            UserEntity elevatedUser, 
            string elevationType)
        {
            // Log to console for now
            Console.WriteLine($"{elevationType} elevation: User {originalUser.Username} (uid={originalUser.UserId}) " +
                             $"executing as {elevatedUser.Username} (uid={elevatedUser.UserId}) " +
                             $"for file {file.FullPath}");
            
            // In a real implementation, this would properly log to the file system
        }
    }
}
