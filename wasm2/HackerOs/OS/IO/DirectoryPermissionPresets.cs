using System;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.IO
{
    /// <summary>
    /// Provides preset permissions for common directory types
    /// </summary>
    public static class DirectoryPermissionPresets
    {
        /// <summary>
        /// Permission modes for directories
        /// </summary>
        public enum PermissionMode
        {
            /// <summary>
            /// Private directory (700 - owner can read/write/execute, others have no access)
            /// </summary>
            Private = 0700,
            
            /// <summary>
            /// Protected directory (750 - owner can read/write/execute, group can read/execute, others have no access)
            /// </summary>
            Protected = 0750,
            
            /// <summary>
            /// Shared directory (770 - owner and group can read/write/execute, others have no access)
            /// </summary>
            Shared = 0770,
            
            /// <summary>
            /// Standard directory (755 - owner can read/write/execute, others can read/execute)
            /// </summary>
            Standard = 0755,
            
            /// <summary>
            /// Public directory (775 - owner and group can read/write/execute, others can read/execute)
            /// </summary>
            Public = 0775,
            
            /// <summary>
            /// World writable directory (777 - everyone can read/write/execute)
            /// </summary>
            WorldWritable = 0777,
            
            /// <summary>
            /// Sticky directory (1777 - everyone can read/write/execute, but only owner can delete their files)
            /// </summary>
            Sticky = 01777,
            
            /// <summary>
            /// SetGID directory (2775 - files created in this directory inherit its group)
            /// </summary>
            SetGID = 02775
        }

        /// <summary>
        /// Applies a permission preset to a directory
        /// </summary>
        /// <param name="fileSystem">The file system to use</param>
        /// <param name="path">The directory path</param>
        /// <param name="mode">The permission mode to apply</param>
        /// <param name="ownerId">The owner ID to set</param>
        /// <param name="groupId">The group ID to set</param>
        /// <param name="recursive">Whether to apply permissions recursively</param>
        /// <returns>True if successful</returns>
        public static async Task<bool> ApplyPermissionPresetAsync(
            IVirtualFileSystem fileSystem,
            string path,
            PermissionMode mode,
            int ownerId,
            int groupId,
            bool recursive = false)
        {
            if (fileSystem == null)
            {
                throw new ArgumentNullException(nameof(fileSystem));
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be empty", nameof(path));
            }

            // Convert enum to int for permission setting
            int permissions = (int)mode;

            try
            {
                // Set permissions on the directory
                if (!await fileSystem.DirectoryExistsAsync(path))
                {
                    return false;
                }

                bool success = await fileSystem.SetPermissionsAsync(path, permissions, ownerId, groupId);
                if (!success)
                {
                    return false;
                }

                // Apply recursively if requested
                if (recursive)
                {
                    // Get all files and directories
                    var files = await fileSystem.GetFilesAsync(path);
                    var directories = await fileSystem.GetDirectoriesAsync(path);

                    // Calculate file permissions by removing execute bit from directory permissions
                    int filePermissions = permissions & 0666; // Remove execute bits

                    // Set permissions on files
                    foreach (var file in files)
                    {
                        string filePath = $"{path}/{file}";
                        await fileSystem.SetPermissionsAsync(filePath, filePermissions, ownerId, groupId);
                    }

                    // Recursively set permissions on subdirectories
                    foreach (var dir in directories)
                    {
                        string dirPath = $"{path}/{dir}";
                        await ApplyPermissionPresetAsync(fileSystem, dirPath, mode, ownerId, groupId, true);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                // Log the error
                Console.Error.WriteLine($"Error applying permission preset: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies permission presets to standard user directories
        /// </summary>
        /// <param name="fileSystem">The file system to use</param>
        /// <param name="homePath">The user's home directory path</param>
        /// <param name="userId">The user ID</param>
        /// <param name="groupId">The group ID</param>
        /// <returns>True if successful</returns>
        public static async Task<bool> ApplyStandardPermissionsAsync(
            IVirtualFileSystem fileSystem,
            string homePath,
            int userId,
            int groupId)
        {
            if (fileSystem == null)
            {
                throw new ArgumentNullException(nameof(fileSystem));
            }

            if (string.IsNullOrEmpty(homePath))
            {
                throw new ArgumentException("Home path cannot be empty", nameof(homePath));
            }

            try
            {
                // Define standard directory permission mappings
                var directoryPermissions = new Dictionary<string, PermissionMode>
                {
                    // Public directories
                    { "Desktop", PermissionMode.Standard },
                    { "Documents", PermissionMode.Standard },
                    { "Downloads", PermissionMode.Standard },
                    { "Music", PermissionMode.Standard },
                    { "Pictures", PermissionMode.Standard },
                    { "Public", PermissionMode.Public },
                    { "Videos", PermissionMode.Standard },
                    
                    // Private directories
                    { ".ssh", PermissionMode.Private },
                    { ".gnupg", PermissionMode.Private },
                    
                    // Configuration directories
                    { ".config", PermissionMode.Standard },
                    { ".local", PermissionMode.Standard },
                    { ".local/bin", PermissionMode.Standard },
                    { ".local/share", PermissionMode.Standard },
                    { ".cache", PermissionMode.Standard },
                    
                    // Special directories for developers
                    { "Projects", PermissionMode.Standard },
                    { "Workspace", PermissionMode.Standard }
                };

                // Apply permissions to each directory
                bool allSucceeded = true;
                foreach (var (dir, mode) in directoryPermissions)
                {
                    string dirPath = $"{homePath}/{dir}";
                    if (await fileSystem.DirectoryExistsAsync(dirPath))
                    {
                        bool success = await ApplyPermissionPresetAsync(fileSystem, dirPath, mode, userId, groupId, false);
                        if (!success)
                        {
                            allSucceeded = false;
                            Console.Error.WriteLine($"Failed to apply permissions to {dirPath}");
                        }
                    }
                }

                return allSucceeded;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error applying standard permissions: {ex.Message}");
                return false;
            }
        }
    }
}
