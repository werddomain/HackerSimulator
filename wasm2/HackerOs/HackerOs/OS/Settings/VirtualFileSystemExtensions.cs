using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HackerOs.IO.FileSystem;

namespace HackerOs.OS.Settings
{
    /// <summary>
    /// Extension methods for IVirtualFileSystem to provide text-based operations
    /// that are commonly needed by configuration management services.
    /// </summary>
    public static class VirtualFileSystemExtensions
    {
        /// <summary>
        /// Reads all text from a file using UTF-8 encoding.
        /// </summary>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <param name="path">The path to the file</param>
        /// <returns>The file content as a string</returns>
        public static async Task<string> ReadAllTextAsync(this IVirtualFileSystem fileSystem, string path)
        {
            var content = await fileSystem.ReadFileAsync(path);
            if (content == null)
            {
                throw new FileNotFoundException($"Could not find file '{path}'.");
            }
            return Encoding.UTF8.GetString(content);
        }

        /// <summary>
        /// Writes all text to a file using UTF-8 encoding.
        /// </summary>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <param name="path">The path to the file</param>
        /// <param name="content">The text content to write</param>
        public static async Task WriteAllTextAsync(this IVirtualFileSystem fileSystem, string path, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            await fileSystem.WriteFileAsync(path, bytes);
        }

        /// <summary>
        /// Gets all files in a directory.
        /// </summary>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <param name="path">The directory path</param>
        /// <returns>A collection of file information</returns>
        public static async Task<IEnumerable<VirtualFileInfo>> GetFilesAsync(this IVirtualFileSystem fileSystem, string path)
        {
            var entries = await fileSystem.ListDirectoryAsync(path);
            return entries
                .Where(e => !e.IsDirectory)
                .Select(e => new VirtualFileInfo
                {
                    Name = e.Name,
                    FullPath = e.FullPath,
                    Size = e is VirtualFile file ? file.Content.Length : 0,
                    LastModifiedTime = e.ModifiedAt
                });
        }

        /// <summary>
        /// Gets all directories in a directory.
        /// </summary>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <param name="path">The directory path</param>
        /// <returns>A collection of directory information</returns>
        public static async Task<IEnumerable<VirtualDirectoryInfo>> GetDirectoriesAsync(this IVirtualFileSystem fileSystem, string path)
        {
            var entries = await fileSystem.ListDirectoryAsync(path);
            return entries
                .Where(e => e.IsDirectory)
                .Select(e => new VirtualDirectoryInfo
                {
                    Name = e.Name,
                    FullPath = e.FullPath,
                    LastModifiedTime = e.ModifiedAt
                });
        }
    }

    /// <summary>
    /// Represents file information for configuration backup operations.
    /// </summary>
    public class VirtualFileInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModifiedTime { get; set; }
    }

    /// <summary>
    /// Represents directory information for configuration backup operations.
    /// </summary>
    public class VirtualDirectoryInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime LastModifiedTime { get; set; }
    }
}
