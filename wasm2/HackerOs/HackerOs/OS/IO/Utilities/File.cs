using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace HackerOs.IO.Utilities
{
    /// <summary>
    /// Provides static utility methods for file operations, similar to System.IO.File.
    /// This class offers convenient methods for common file operations in the virtual file system.
    /// </summary>
    public static class File
    {
        /// <summary>
        /// Determines whether the specified file exists.
        /// </summary>
        /// <param name="path">The file to check</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>true if the file exists; otherwise, false</returns>
        public static async Task<bool> ExistsAsync(string path, FileSystem.IVirtualFileSystem fileSystem)
        {
            try
            {
                var node = await fileSystem.GetNodeAsync(path);
                return node != null && !node.IsDirectory;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates or overwrites a file in the specified path.
        /// </summary>
        /// <param name="path">The path and name of the file to create</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task CreateAsync(string path, FileSystem.IVirtualFileSystem fileSystem)
        {
            await fileSystem.CreateFileAsync(path, Array.Empty<byte>());
        }

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="path">The name of the file to be deleted</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task DeleteAsync(string path, FileSystem.IVirtualFileSystem fileSystem)
        {
            await fileSystem.DeleteAsync(path, false);
        }

        /// <summary>
        /// Copies an existing file to a new file.
        /// </summary>
        /// <param name="sourceFileName">The file to copy</param>
        /// <param name="destFileName">The name of the destination file</param>
        /// <param name="overwrite">true if the destination file can be overwritten; otherwise, false</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task CopyAsync(string sourceFileName, string destFileName, bool overwrite, FileSystem.IVirtualFileSystem fileSystem)
        {
            if (!overwrite && await ExistsAsync(destFileName, fileSystem))
            {
                throw new IOException($"The file '{destFileName}' already exists.");
            }

            if (overwrite && await ExistsAsync(destFileName, fileSystem))
            {
                await DeleteAsync(destFileName, fileSystem);
            }

            await fileSystem.CopyAsync(sourceFileName, destFileName);
        }

        /// <summary>
        /// Copies an existing file to a new file.
        /// </summary>
        /// <param name="sourceFileName">The file to copy</param>
        /// <param name="destFileName">The name of the destination file</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task CopyAsync(string sourceFileName, string destFileName, FileSystem.IVirtualFileSystem fileSystem)
        {
            await CopyAsync(sourceFileName, destFileName, false, fileSystem);
        }

        /// <summary>
        /// Moves a specified file to a new location, providing the option to specify a new file name.
        /// </summary>
        /// <param name="sourceFileName">The name of the file to move</param>
        /// <param name="destFileName">The new path and name for the file</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task MoveAsync(string sourceFileName, string destFileName, FileSystem.IVirtualFileSystem fileSystem)
        {
            await fileSystem.MoveAsync(sourceFileName, destFileName);
        }

        /// <summary>
        /// Opens a text file, reads all lines of the file, and then closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A string array containing all lines of the file</returns>
        public static async Task<string[]> ReadAllLinesAsync(string path, FileSystem.IVirtualFileSystem fileSystem)
        {
            var text = await ReadAllTextAsync(path, fileSystem);
            return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        }

        /// <summary>
        /// Opens a text file, reads all lines of the file with the specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading</param>
        /// <param name="encoding">The encoding applied to the contents of the file</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A string array containing all lines of the file</returns>
        public static async Task<string[]> ReadAllLinesAsync(string path, Encoding encoding, FileSystem.IVirtualFileSystem fileSystem)
        {
            var text = await ReadAllTextAsync(path, encoding, fileSystem);
            return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        }

        /// <summary>
        /// Opens a text file, reads all the text in the file, and then closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A string containing all the text in the file</returns>
        public static async Task<string> ReadAllTextAsync(string path, FileSystem.IVirtualFileSystem fileSystem)
        {
            return await ReadAllTextAsync(path, Encoding.UTF8, fileSystem);
        }

        /// <summary>
        /// Opens a file, reads all the text in the file with the specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading</param>
        /// <param name="encoding">The encoding applied to the contents of the file</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A string containing all the text in the file</returns>
        public static async Task<string> ReadAllTextAsync(string path, Encoding encoding, FileSystem.IVirtualFileSystem fileSystem)
        {
            var content = await fileSystem.ReadFileAsync(path);
            if (content == null)
            {
                throw new FileNotFoundException($"Could not find file '{path}'.");
            }
            return encoding.GetString(content);
        }

        /// <summary>
        /// Opens a binary file, reads the contents of the file into a byte array, and then closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A byte array containing the contents of the file</returns>
        public static async Task<byte[]> ReadAllBytesAsync(string path, FileSystem.IVirtualFileSystem fileSystem)
        {
            var content = await fileSystem.ReadFileAsync(path);
            if (content == null)
            {
                throw new FileNotFoundException($"Could not find file '{path}'.");
            }
            return content;
        }

        /// <summary>
        /// Creates a new file, writes the specified string to the file, and then closes the file.
        /// </summary>
        /// <param name="path">The file to write to</param>
        /// <param name="contents">The string to write to the file</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task WriteAllTextAsync(string path, string contents, FileSystem.IVirtualFileSystem fileSystem)
        {
            await WriteAllTextAsync(path, contents, Encoding.UTF8, fileSystem);
        }

        /// <summary>
        /// Creates a new file, writes the specified string to the file using the specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path">The file to write to</param>
        /// <param name="contents">The string to write to the file</param>
        /// <param name="encoding">The encoding to apply to the string</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task WriteAllTextAsync(string path, string contents, Encoding encoding, FileSystem.IVirtualFileSystem fileSystem)
        {
            var bytes = encoding.GetBytes(contents ?? string.Empty);
            await fileSystem.WriteFileAsync(path, bytes);
        }

        /// <summary>
        /// Creates a new file, writes a collection of strings to the file, and then closes the file.
        /// </summary>
        /// <param name="path">The file to write to</param>
        /// <param name="contents">The lines to write to the file</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task WriteAllLinesAsync(string path, IEnumerable<string> contents, FileSystem.IVirtualFileSystem fileSystem)
        {
            await WriteAllLinesAsync(path, contents, Encoding.UTF8, fileSystem);
        }

        /// <summary>
        /// Creates a new file, writes a collection of strings to the file using the specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path">The file to write to</param>
        /// <param name="contents">The lines to write to the file</param>
        /// <param name="encoding">The encoding to apply to the string</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task WriteAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding, FileSystem.IVirtualFileSystem fileSystem)
        {
            var text = string.Join(Environment.NewLine, contents);
            await WriteAllTextAsync(path, text, encoding, fileSystem);
        }

        /// <summary>
        /// Creates a new file, writes the specified byte array to the file, and then closes the file.
        /// </summary>
        /// <param name="path">The file to write to</param>
        /// <param name="bytes">The bytes to write to the file</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task WriteAllBytesAsync(string path, byte[] bytes, FileSystem.IVirtualFileSystem fileSystem)
        {
            await fileSystem.WriteFileAsync(path, bytes ?? Array.Empty<byte>());
        }

        /// <summary>
        /// Opens a text file, appends the specified string to the file, and then closes the file.
        /// </summary>
        /// <param name="path">The file to append to</param>
        /// <param name="contents">The string to append to the file</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task AppendAllTextAsync(string path, string contents, FileSystem.IVirtualFileSystem fileSystem)
        {
            await AppendAllTextAsync(path, contents, Encoding.UTF8, fileSystem);
        }

        /// <summary>
        /// Opens a file, appends the specified string to the file using the specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path">The file to append to</param>
        /// <param name="contents">The string to append to the file</param>
        /// <param name="encoding">The encoding to apply to the string</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task AppendAllTextAsync(string path, string contents, Encoding encoding, FileSystem.IVirtualFileSystem fileSystem)
        {
            string existingContent = "";
            try
            {
                existingContent = await ReadAllTextAsync(path, encoding, fileSystem);
            }
            catch (FileNotFoundException)
            {
                // File doesn't exist, that's fine for append
            }

            var newContent = existingContent + (contents ?? string.Empty);
            await WriteAllTextAsync(path, newContent, encoding, fileSystem);
        }

        /// <summary>
        /// Appends lines to a file, and then closes the file.
        /// </summary>
        /// <param name="path">The file to append to</param>
        /// <param name="contents">The lines to append to the file</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task AppendAllLinesAsync(string path, IEnumerable<string> contents, FileSystem.IVirtualFileSystem fileSystem)
        {
            await AppendAllLinesAsync(path, contents, Encoding.UTF8, fileSystem);
        }

        /// <summary>
        /// Appends lines to a file using a specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path">The file to append to</param>
        /// <param name="contents">The lines to append to the file</param>
        /// <param name="encoding">The encoding to apply to the string</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task AppendAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding, FileSystem.IVirtualFileSystem fileSystem)
        {
            var linesToAppend = contents?.ToArray() ?? Array.Empty<string>();
            if (linesToAppend.Length == 0)
                return;

            var textToAppend = Environment.NewLine + string.Join(Environment.NewLine, linesToAppend);
            await AppendAllTextAsync(path, textToAppend, encoding, fileSystem);
        }

        /// <summary>
        /// Gets the creation time of the specified file.
        /// </summary>
        /// <param name="path">The file for which to get creation time</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>The creation time of the file</returns>
        public static async Task<DateTime> GetCreationTimeAsync(string path, FileSystem.IVirtualFileSystem fileSystem)
        {
            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || node.IsDirectory)
            {
                throw new FileNotFoundException($"Could not find file '{path}'.");
            }
            return node.CreatedTime;
        }

        /// <summary>
        /// Gets the last access time of the specified file.
        /// </summary>
        /// <param name="path">The file for which to get last access time</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>The last access time of the file</returns>
        public static async Task<DateTime> GetLastAccessTimeAsync(string path, FileSystem.IVirtualFileSystem fileSystem)
        {
            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || node.IsDirectory)
            {
                throw new FileNotFoundException($"Could not find file '{path}'.");
            }
            return node.LastAccessTime;
        }

        /// <summary>
        /// Gets the last write time of the specified file.
        /// </summary>
        /// <param name="path">The file for which to get last write time</param>
        /// <param name="fileSystem">The virtual file system instance</param>
        /// <returns>The last write time of the file</returns>
        public static async Task<DateTime> GetLastWriteTimeAsync(string path, FileSystem.IVirtualFileSystem fileSystem)
        {
            var node = await fileSystem.GetNodeAsync(path);
            if (node == null || node.IsDirectory)
            {
                throw new FileNotFoundException($"Could not find file '{path}'.");
            }
            return node.LastModifiedTime;
        }
    }
}
