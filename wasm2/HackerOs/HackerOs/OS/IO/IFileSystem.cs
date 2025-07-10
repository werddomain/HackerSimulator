using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HackerOs.OS.IO;

/// <summary>
/// Interface for basic file system operations
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Gets if a file or directory exists at the specified path
    /// </summary>
    Task<bool> ExistsAsync(string path);

    /// <summary>
    /// Gets if an entry at the specified path is a directory
    /// </summary>
    Task<bool> IsDirectoryAsync(string path);

    /// <summary>
    /// Gets information about a file or directory
    /// </summary>
    Task<FileSystemEntry> GetEntryAsync(string path);

    /// <summary>
    /// Lists entries in a directory
    /// </summary>
    Task<IEnumerable<FileSystemEntry>> ListEntriesAsync(string path);

    /// <summary>
    /// Creates a directory
    /// </summary>
    Task CreateDirectoryAsync(string path);

    /// <summary>
    /// Creates a file
    /// </summary>
    Task CreateFileAsync(string path);

    /// <summary>
    /// Deletes a file or directory
    /// </summary>
    Task DeleteAsync(string path, bool recursive = false);

    /// <summary>
    /// Opens a file stream
    /// </summary>
    Task<Stream> OpenFileAsync(string path, FileMode mode, FileAccess access);

    /// <summary>
    /// Reads all bytes from a file
    /// </summary>
    Task<byte[]?> ReadFileAsync(string path);

    /// <summary>
    /// Writes bytes to a file
    /// </summary>
    Task<bool> WriteFileAsync(string path, byte[] content);

    /// <summary>
    /// Checks if a directory exists, and creates it if it does not
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    Task EnsureDirectoryExistsAsync(string path);

    /// <summary>
    /// Wtites text to a file, optionally appending to it
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="content"></param>
    /// <param name="append"></param>
    /// <returns></returns>
    Task WriteTextAsync(string filePath, string content, bool append = false);

    /// <summary>
    /// Reads text from a file
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    Task<string> ReadTextAsync(string filePath);
    /// <summary>
    /// Checks if a file exists at the specified path and is not a directory
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns>True if the file exists and is not a directory; otherwise, false.</returns>
    Task<bool> FileExistsAsync(string filePath);

    /// <summary>
    /// Return all the files in a directory and optionaly matching the search pattern
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="searchPattern"></param>
    /// <returns>Returns a list of file paths matching the search pattern, if provided.</returns>
    Task<IEnumerable<string>> GetFilesAsync(string directory, string? searchPattern = null);
}
