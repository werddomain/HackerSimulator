using System.IO;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;

namespace HackerOs.OS.Shell
{
    /// <summary>
    /// Context interface for shell command execution
    /// </summary>
    public interface IShellContext
    {
        /// <summary>
        /// Current user executing the command
        /// </summary>
        User.User CurrentUser { get; }

        /// <summary>
        /// Current working directory
        /// </summary>
        string WorkingDirectory { get; set; }

        /// <summary>
        /// Environment variables
        /// </summary>
        IDictionary<string, string> Environment { get; }

        /// <summary>
        /// Virtual file system for file operations
        /// </summary>
        IVirtualFileSystem FileSystem { get; }

        /// <summary>
        /// Standard input stream
        /// </summary>
        Stream StandardInput { get; }

        /// <summary>
        /// Standard output stream
        /// </summary>
        Stream StandardOutput { get; }

        /// <summary>
        /// Standard error stream
        /// </summary>
        Stream StandardError { get; }

        /// <summary>
        /// Session identifier
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// Write text to standard output
        /// </summary>
        Task WriteLineAsync(string text);

        /// <summary>
        /// Write text to standard output without newline
        /// </summary>
        Task WriteAsync(string text);

        /// <summary>
        /// Write text to standard error
        /// </summary>
        Task WriteErrorAsync(string text);

        /// <summary>
        /// Read a line from standard input
        /// </summary>
        Task<string?> ReadLineAsync();

        /// <summary>
        /// Check if user has permission to access a path
        /// </summary>
        Task<bool> HasPermissionAsync(string path, FileAccessMode accessMode);

        /// <summary>
        /// Resolve a path relative to working directory
        /// </summary>
        string ResolvePath(string path);

        /// <summary>
        /// Get user's home directory
        /// </summary>
        string GetHomeDirectory();
    }
}
