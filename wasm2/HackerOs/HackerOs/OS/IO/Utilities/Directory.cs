using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;

namespace HackerOs.OS.IO.Utilities
{
    /// <summary>
    /// Provides static utility methods for directory operations, similar to System.IO.Directory.
    /// This class offers convenient methods for common directory operations in the virtual file system.
    /// </summary>
    public static class Directory
    {
        /// <summary>
        /// Determines whether the given path refers to an existing directory.
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>true if path refers to an existing directory; otherwise, false</returns>
        public static async Task<bool> ExistsAsync(string path, IVirtualFileSystem fileSystem)
        {
            try
            {
                var node = await fileSystem.GetNodeAsync(path);
                return node != null && node.IsDirectory;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates all directories and subdirectories in the specified path.
        /// </summary>
        /// <param name="path">The directory to create</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task CreateDirectoryAsync(string path, IVirtualFileSystem fileSystem)
        {
            await fileSystem.CreateDirectoryAsync(path);
        }

        /// <summary>
        /// Deletes an empty directory from a specified path.
        /// </summary>
        /// <param name="path">The name of the empty directory to remove</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task DeleteAsync(string path, IVirtualFileSystem fileSystem)
        {
            await fileSystem.DeleteAsync(path, false);
        }

        /// <summary>
        /// Deletes the specified directory and, if indicated, any subdirectories and files in the directory.
        /// </summary>
        /// <param name="path">The name of the directory to remove</param>
        /// <param name="recursive">true to remove directories, subdirectories, and files in path; otherwise, false</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task DeleteAsync(string path, bool recursive, IVirtualFileSystem fileSystem)
        {
            await fileSystem.DeleteAsync(path, recursive);
        }

        /// <summary>
        /// Returns the names of files (including their paths) in the specified directory.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>An array of the full names (including paths) for the files in the specified directory</returns>
        public static async Task<string[]> GetFilesAsync(string path, IVirtualFileSystem fileSystem)
        {
            var nodes = await fileSystem.ListDirectoryAsync(path);
            return nodes.Where(n => !n.IsDirectory)
                       .Select(n => n.FullPath)
                       .ToArray();
        }

        /// <summary>
        /// Returns the names of files (including their paths) that match the specified search pattern in the specified directory.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search</param>
        /// <param name="searchPattern">The search string to match against the names of files in path</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>An array of the full names (including paths) for the files in the specified directory that match the specified search pattern</returns>
        public static async Task<string[]> GetFilesAsync(string path, string searchPattern, IVirtualFileSystem fileSystem)
        {
            var allFiles = await GetFilesAsync(path, fileSystem);
            return allFiles.Where(f => MatchesPattern(System.IO.Path.GetFileName(f), searchPattern))
                          .ToArray();
        }

        /// <summary>
        /// Returns the names of subdirectories (including their paths) in the specified directory.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>An array of the full names (including paths) of subdirectories in the specified path</returns>
        public static async Task<string[]> GetDirectoriesAsync(string path, IVirtualFileSystem fileSystem)
        {
            var nodes = await fileSystem.ListDirectoryAsync(path);
            return nodes.Where(n => n.IsDirectory)
                       .Select(n => n.FullPath)
                       .ToArray();
        }

        /// <summary>
        /// Returns the names of subdirectories (including their paths) that match the specified search pattern in the specified directory.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search</param>
        /// <param name="searchPattern">The search string to match against the names of subdirectories in path</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>An array of the full names (including paths) of the subdirectories in the specified path that match the specified search pattern</returns>
        public static async Task<string[]> GetDirectoriesAsync(string path, string searchPattern, IVirtualFileSystem fileSystem)
        {
            var allDirectories = await GetDirectoriesAsync(path, fileSystem);
            return allDirectories.Where(d => MatchesPattern(System.IO.Path.GetFileName(d), searchPattern))
                               .ToArray();
        }

        /// <summary>
        /// Returns the names of all files and subdirectories in the specified directory.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>An array of the names of files and subdirectories in the specified directory</returns>
        public static async Task<string[]> GetFileSystemEntriesAsync(string path, IVirtualFileSystem fileSystem)
        {
            var nodes = await fileSystem.ListDirectoryAsync(path);
            return nodes.Select(n => n.FullPath).ToArray();
        }

        /// <summary>
        /// Returns an array of file names and directory names that match a search pattern in a specified path.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search</param>
        /// <param name="searchPattern">The search string to match against the names of file and directories in path</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>An array of file names and directory names that match the specified search criteria</returns>
        public static async Task<string[]> GetFileSystemEntriesAsync(string path, string searchPattern, IVirtualFileSystem fileSystem)
        {
            var allEntries = await GetFileSystemEntriesAsync(path, fileSystem);
            return allEntries.Where(e => MatchesPattern(System.IO.Path.GetFileName(e), searchPattern))
                           .ToArray();
        }

        /// <summary>
        /// Moves a file or a directory and its contents to a new location.
        /// </summary>
        /// <param name="sourceDirName">The path of the file or directory to move</param>
        /// <param name="destDirName">The path to the new location for sourceDirName</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task MoveAsync(string sourceDirName, string destDirName, IVirtualFileSystem fileSystem)
        {
            await fileSystem.MoveAsync(sourceDirName, destDirName);
        }

        /// <summary>
        /// Gets the creation time of a directory.
        /// </summary>
        /// <param name="path">The path of the directory</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>The creation time of the directory</returns>
        public static async Task<DateTime> GetCreationTimeAsync(string path, IVirtualFileSystem fileSystem)
        {
            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || !node.IsDirectory)
            {
                throw new System.IO.DirectoryNotFoundException($"Could not find directory '{path}'.");
            }
            return node.CreatedTime;
        }

        /// <summary>
        /// Gets the last access time of a directory.
        /// </summary>
        /// <param name="path">The path of the directory</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>The last access time of the directory</returns>
        public static async Task<DateTime> GetLastAccessTimeAsync(string path, IVirtualFileSystem fileSystem)
        {
            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || !node.IsDirectory)
            {
                throw new System.IO.DirectoryNotFoundException($"Could not find directory '{path}'.");
            }
            return node.LastAccessTime;
        }

        /// <summary>
        /// Gets the last write time of a directory.
        /// </summary>
        /// <param name="path">The path of the directory</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>The last write time of the directory</returns>
        public static async Task<DateTime> GetLastWriteTimeAsync(string path, IVirtualFileSystem fileSystem)
        {
            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || !node.IsDirectory)
            {
                throw new System.IO.DirectoryNotFoundException($"Could not find directory '{path}'.");
            }
            return node.LastModifiedTime;
        }

        /// <summary>
        /// Gets the current working directory of the application.
        /// </summary>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>The current working directory</returns>
        public static string GetCurrentDirectory(IVirtualFileSystem fileSystem)
        {
            if (fileSystem is VirtualFileSystem vfs)
            {
                return vfs.GetCurrentDirectory();
            }
            return "/";
        }

        /// <summary>
        /// Sets the application's current working directory to the specified directory.
        /// </summary>
        /// <param name="path">The path to which the current working directory is set</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task SetCurrentDirectoryAsync(string path, IVirtualFileSystem fileSystem)
        {
            if (fileSystem is VirtualFileSystem vfs)
            {
                await vfs.ChangeDirectoryAsync(path);
            }
        }

        /// <summary>
        /// Returns the volume information, root information, or both for the specified path.
        /// </summary>
        /// <param name="path">The path of a file or directory</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A string containing the volume information, root information, or both for the specified path</returns>
        public static string GetDirectoryRoot(string path, IVirtualFileSystem fileSystem)
        {
            // In our virtual file system, the root is always "/"
            return "/";
        }

        /// <summary>
        /// Retrieves the parent directory of the specified path.
        /// </summary>
        /// <param name="path">The path for which to retrieve the parent directory</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>The parent directory, or null if path is the root directory</returns>
        public static string? GetParent(string path, IVirtualFileSystem fileSystem)
        {
            if (string.IsNullOrEmpty(path) || path == "/")
                return null;

            var normalizedPath = path.TrimEnd('/');
            var lastSlash = normalizedPath.LastIndexOf('/');
            
            if (lastSlash <= 0)
                return "/";
            
            return normalizedPath.Substring(0, lastSlash);
        }        /// <summary>
        /// Determines whether a filename matches a search pattern using wildcards.
        /// </summary>
        /// <param name="filename">The filename to test</param>
        /// <param name="pattern">The search pattern, which may contain wildcards (* and ?)</param>
        /// <returns>true if the filename matches the pattern; otherwise, false</returns>
        private static bool MatchesPattern(string filename, string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || pattern == "*")
                return true;
                  // Convert pattern to regex
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                                  .Replace("\\*", ".*")
                                  .Replace("\\?", ".") + "$";
            
            // Using IsMatch with just the pattern - no third parameter to avoid the error
            return System.Text.RegularExpressions.Regex.IsMatch(filename, regexPattern);
        }
    }
}
