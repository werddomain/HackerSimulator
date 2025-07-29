using ProxyServer.FileSystem.Management;
using ProxyServer.FileSystem.Models;
using ProxyServer.FileSystem.Security;

namespace ProxyServer.FileSystem.Operations
{
    /// <summary>
    /// Extension methods for FileSystemOperations to add support for virtual paths
    /// </summary>
    public static class FileSystemOperationsExtensions
    {
        /// <summary>
        /// Deletes a file using a virtual path
        /// </summary>
        /// <param name="operations">The FileSystemOperations instance</param>
        /// <param name="virtualPath">Virtual path of the file to delete</param>
        /// <param name="username">User performing the operation</param>
        /// <returns>True if deletion was successful</returns>
        public static async Task<bool> DeleteFileByVirtualPathAsync(
            this FileSystemOperations operations,
            string virtualPath, 
            string username = "anonymous")
        {
            // Resolve the virtual path
            var resolved = operations.ResolveVirtualPath(virtualPath, username);
            if (resolved == null)
            {
                throw new FileNotFoundException($"File not found: {virtualPath}");
            }

            var (hostPath, sharedFolder, mountPoint) = resolved.Value;

            // Check if the file exists
            if (!File.Exists(hostPath))
            {
                throw new FileNotFoundException($"File not found: {virtualPath}");
            }

            // Check if operation is allowed based on mount options
            var effectivePermission = mountPoint.Options.GetEffectivePermission(sharedFolder.Permission);
            if (effectivePermission != SharedFolderPermission.ReadWrite)
            {
                throw new UnauthorizedAccessException($"Delete operation not allowed on {virtualPath}");
            }

            try
            {
                // Delete the file
                File.Delete(hostPath);

                return true;
            }
            catch (Exception ex)
            {
                throw new IOException($"Error deleting file {virtualPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deletes a directory using a virtual path
        /// </summary>
        /// <param name="operations">The FileSystemOperations instance</param>
        /// <param name="virtualPath">Virtual path of the directory to delete</param>
        /// <param name="recursive">Whether to recursively delete subdirectories and files</param>
        /// <param name="username">User performing the operation</param>
        /// <returns>True if deletion was successful</returns>
        public static async Task<bool> DeleteDirectoryByVirtualPathAsync(
            this FileSystemOperations operations,
            string virtualPath,
            bool recursive,
            string username = "anonymous")
        {
            // Resolve the virtual path
            var resolved = operations.ResolveVirtualPath(virtualPath, username);
            if (resolved == null)
            {
                throw new DirectoryNotFoundException($"Directory not found: {virtualPath}");
            }

            var (hostPath, sharedFolder, mountPoint) = resolved.Value;

            // Check if the directory exists
            if (!Directory.Exists(hostPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {virtualPath}");
            }

            // Check if operation is allowed based on mount options
            var effectivePermission = mountPoint.Options.GetEffectivePermission(sharedFolder.Permission);
            if (effectivePermission != SharedFolderPermission.ReadWrite)
            {
                throw new UnauthorizedAccessException($"Delete operation not allowed on {virtualPath}");
            }

            try
            {
                // Delete the directory
                Directory.Delete(hostPath, recursive);

                return true;
            }
            catch (Exception ex)
            {
                throw new IOException($"Error deleting directory {virtualPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a directory using a virtual path
        /// </summary>
        /// <param name="operations">The FileSystemOperations instance</param>
        /// <param name="virtualPath">Virtual path of the directory to create</param>
        /// <param name="createParents">Whether to create parent directories if they don't exist</param>
        /// <param name="username">User performing the operation</param>
        /// <returns>True if creation was successful</returns>
        public static async Task<bool> CreateDirectoryByVirtualPathAsync(
            this FileSystemOperations operations,
            string virtualPath,
            bool createParents,
            string username = "anonymous")
        {
            // For creating directories, we need to find the closest existing parent directory
            string currentPath = virtualPath;
            string? parentPath = Path.GetDirectoryName(currentPath.Replace('/', Path.DirectorySeparatorChar))?.Replace(Path.DirectorySeparatorChar, '/');
            
            // If creating parent directories is requested, traverse up until we find an existing directory
            var mountPoint = operations.ResolveVirtualPath(virtualPath, username);
            if (mountPoint == null && createParents && !string.IsNullOrEmpty(parentPath))
            {
                // Find the closest existing parent directory
                while (mountPoint == null && !string.IsNullOrEmpty(parentPath))
                {
                    mountPoint = operations.ResolveVirtualPath(parentPath, username);
                    if (mountPoint == null)
                    {
                        parentPath = Path.GetDirectoryName(parentPath.Replace('/', Path.DirectorySeparatorChar))?.Replace(Path.DirectorySeparatorChar, '/');
                    }
                }

                if (mountPoint == null)
                {
                    throw new DirectoryNotFoundException($"Could not find a valid parent directory for: {virtualPath}");
                }

                // We found an existing parent directory, now create all subdirectories
                var (hostPathParent, sharedFolder, mountPointObj) = mountPoint.Value;

                // Check if operation is allowed based on mount options
                var effectivePermission = mountPointObj.Options.GetEffectivePermission(sharedFolder.Permission);
                if (effectivePermission != SharedFolderPermission.ReadWrite)
                {
                    throw new UnauthorizedAccessException($"Create operation not allowed on {parentPath}");
                }

                // Create the directories from the parent path to the target path
                string relativePath = virtualPath.Substring(parentPath.Length).TrimStart('/');
                string fullPath = Path.Combine(hostPathParent, relativePath.Replace('/', Path.DirectorySeparatorChar));

                try
                {
                    Directory.CreateDirectory(fullPath);
                    return true;
                }
                catch (Exception ex)
                {
                    throw new IOException($"Error creating directory {virtualPath}: {ex.Message}", ex);
                }
            }
            else if (mountPoint != null)
            {
                // Direct path resolution worked, create the directory directly
                var (hostPath, sharedFolder, mountPointObj) = mountPoint.Value;

                // Check if operation is allowed based on mount options
                var effectivePermission = mountPointObj.Options.GetEffectivePermission(sharedFolder.Permission);
                if (effectivePermission != SharedFolderPermission.ReadWrite)
                {
                    throw new UnauthorizedAccessException($"Create operation not allowed on {virtualPath}");
                }

                try
                {
                    if (!Directory.Exists(hostPath))
                    {
                        Directory.CreateDirectory(hostPath);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    throw new IOException($"Error creating directory {virtualPath}: {ex.Message}", ex);
                }
            }
            else
            {
                throw new DirectoryNotFoundException($"Could not resolve path: {virtualPath}");
            }
        }

        /// <summary>
        /// Copies a file or directory using virtual paths
        /// </summary>
        /// <param name="operations">The FileSystemOperations instance</param>
        /// <param name="sourcePath">Virtual source path</param>
        /// <param name="destinationPath">Virtual destination path</param>
        /// <param name="overwrite">Whether to overwrite if destination exists</param>
        /// <param name="recursive">Whether to recursively copy subdirectories</param>
        /// <param name="username">User performing the operation</param>
        /// <returns>True if copy was successful</returns>
        public static async Task<bool> CopyByVirtualPathAsync(
            this FileSystemOperations operations,
            string sourcePath,
            string destinationPath,
            bool overwrite,
            bool recursive,
            string username = "anonymous")
        {
            // Resolve the source path
            var sourceResolved = operations.ResolveVirtualPath(sourcePath, username);
            if (sourceResolved == null)
            {
                throw new FileNotFoundException($"Source path not found: {sourcePath}");
            }

            var (sourceHostPath, sourceSharedFolder, sourceMountPoint) = sourceResolved.Value;

            // Resolve the destination path
            var destResolved = operations.ResolveVirtualPath(destinationPath, username);
            
            // If destination doesn't exist, resolve the parent directory
            string? destDirPath = null;
            if (destResolved == null)
            {
                destDirPath = Path.GetDirectoryName(destinationPath.Replace('/', Path.DirectorySeparatorChar))?.Replace(Path.DirectorySeparatorChar, '/');
                if (string.IsNullOrEmpty(destDirPath))
                {
                    throw new DirectoryNotFoundException($"Destination parent directory not found for: {destinationPath}");
                }

                var destDirResolved = operations.ResolveVirtualPath(destDirPath, username);
                if (destDirResolved == null)
                {
                    throw new DirectoryNotFoundException($"Destination directory not found: {destDirPath}");
                }

                var (destDirHostPath, destSharedFolder, destMountPoint) = destDirResolved.Value;

                // Check if write operation is allowed on destination
                var effectivePermission = destMountPoint.Options.GetEffectivePermission(destSharedFolder.Permission);
                if (effectivePermission != SharedFolderPermission.ReadWrite)
                {
                    throw new UnauthorizedAccessException($"Write operation not allowed on {destDirPath}");
                }

                // Construct the destination host path
                string destFileName = Path.GetFileName(destinationPath.Replace('/', Path.DirectorySeparatorChar));
                string destHostPath = Path.Combine(destDirHostPath, destFileName);

                // Check if source is a file or directory
                if (File.Exists(sourceHostPath))
                {
                    try
                    {
                        // Copy the file
                        File.Copy(sourceHostPath, destHostPath, overwrite);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Error copying file: {ex.Message}", ex);
                    }
                }
                else if (Directory.Exists(sourceHostPath))
                {
                    if (!recursive)
                    {
                        throw new IOException("Directory copying requires recursive flag to be true");
                    }

                    try
                    {
                        // Create the destination directory if it doesn't exist
                        if (!Directory.Exists(destHostPath))
                        {
                            Directory.CreateDirectory(destHostPath);
                        }

                        // Copy all files and subdirectories recursively
                        CopyDirectoryRecursive(sourceHostPath, destHostPath, overwrite);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Error copying directory: {ex.Message}", ex);
                    }
                }
                else
                {
                    throw new FileNotFoundException($"Source path not found: {sourcePath}");
                }
            }
            else
            {
                // Destination already exists
                var (destHostPath, destSharedFolder, destMountPoint) = destResolved.Value;

                // Check if write operation is allowed on destination
                var effectivePermission = destMountPoint.Options.GetEffectivePermission(destSharedFolder.Permission);
                if (effectivePermission != SharedFolderPermission.ReadWrite)
                {
                    throw new UnauthorizedAccessException($"Write operation not allowed on {destinationPath}");
                }

                // Check if source is a file or directory
                if (File.Exists(sourceHostPath))
                {
                    // If destination is a directory, append the file name
                    if (Directory.Exists(destHostPath))
                    {
                        string fileName = Path.GetFileName(sourceHostPath);
                        destHostPath = Path.Combine(destHostPath, fileName);
                    }

                    try
                    {
                        // Copy the file
                        File.Copy(sourceHostPath, destHostPath, overwrite);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Error copying file: {ex.Message}", ex);
                    }
                }
                else if (Directory.Exists(sourceHostPath))
                {
                    if (!recursive)
                    {
                        throw new IOException("Directory copying requires recursive flag to be true");
                    }

                    try
                    {
                        // Create the destination directory if it doesn't exist
                        if (File.Exists(destHostPath))
                        {
                            if (overwrite)
                            {
                                File.Delete(destHostPath);
                            }
                            else
                            {
                                throw new IOException($"Destination exists and is a file: {destinationPath}");
                            }
                        }

                        if (!Directory.Exists(destHostPath))
                        {
                            Directory.CreateDirectory(destHostPath);
                        }

                        // Copy all files and subdirectories recursively
                        CopyDirectoryRecursive(sourceHostPath, destHostPath, overwrite);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Error copying directory: {ex.Message}", ex);
                    }
                }
                else
                {
                    throw new FileNotFoundException($"Source path not found: {sourcePath}");
                }
            }
        }

        /// <summary>
        /// Moves a file or directory using virtual paths
        /// </summary>
        /// <param name="operations">The FileSystemOperations instance</param>
        /// <param name="sourcePath">Virtual source path</param>
        /// <param name="destinationPath">Virtual destination path</param>
        /// <param name="overwrite">Whether to overwrite if destination exists</param>
        /// <param name="username">User performing the operation</param>
        /// <returns>True if move was successful</returns>
        public static async Task<bool> MoveByVirtualPathAsync(
            this FileSystemOperations operations,
            string sourcePath,
            string destinationPath,
            bool overwrite,
            string username = "anonymous")
        {
            // First copy the file/directory
            await CopyByVirtualPathAsync(operations, sourcePath, destinationPath, overwrite, true, username);

            // Then delete the source
            var sourceResolved = operations.ResolveVirtualPath(sourcePath, username);
            if (sourceResolved == null)
            {
                throw new FileNotFoundException($"Source path not found: {sourcePath}");
            }

            var (sourceHostPath, sourceSharedFolder, sourceMountPoint) = sourceResolved.Value;

            if (File.Exists(sourceHostPath))
            {
                await DeleteFileByVirtualPathAsync(operations, sourcePath, username);
            }
            else if (Directory.Exists(sourceHostPath))
            {
                await DeleteDirectoryByVirtualPathAsync(operations, sourcePath, true, username);
            }

            return true;
        }

        /// <summary>
        /// Helper method to recursively copy a directory
        /// </summary>
        private static void CopyDirectoryRecursive(string sourceDirPath, string destDirPath, bool overwrite)
        {
            // Create the destination directory if it doesn't exist
            Directory.CreateDirectory(destDirPath);

            // Get all files in the source directory and copy them to the destination directory
            foreach (string filePath in Directory.GetFiles(sourceDirPath))
            {
                string fileName = Path.GetFileName(filePath);
                string destFilePath = Path.Combine(destDirPath, fileName);
                File.Copy(filePath, destFilePath, overwrite);
            }

            // Get all subdirectories in the source directory and copy them to the destination directory
            foreach (string dirPath in Directory.GetDirectories(sourceDirPath))
            {
                string dirName = Path.GetFileName(dirPath);
                string destSubDirPath = Path.Combine(destDirPath, dirName);
                CopyDirectoryRecursive(dirPath, destSubDirPath, overwrite);
            }
        }
    }
}
