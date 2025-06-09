using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using IOPath = HackerOs.OS.IO.Utilities.Path;

namespace HackerOs.OS.Shell
{
    /// <summary>
    /// Implementation of shell context for command execution
    /// </summary>
    public class ShellContext : IShellContext
    {
        public User.User CurrentUser { get; init; } = null!;
        public string WorkingDirectory { get; set; } = "/";
        public IDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
        public IVirtualFileSystem FileSystem { get; init; } = null!;
        public Stream StandardInput { get; init; } = Stream.Null;
        public Stream StandardOutput { get; init; } = Stream.Null;
        public Stream StandardError { get; init; } = Stream.Null;
        public string SessionId { get; init; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Creates a new shell context
        /// </summary>
        public ShellContext()
        {
            InitializeDefaultEnvironment();
        }

        /// <summary>
        /// Creates a shell context with specified parameters
        /// </summary>
        public ShellContext(User.User user, string workingDirectory, IVirtualFileSystem fileSystem,
            Stream stdin, Stream stdout, Stream stderr)
        {
            CurrentUser = user;
            WorkingDirectory = workingDirectory;
            FileSystem = fileSystem;
            StandardInput = stdin;
            StandardOutput = stdout;
            StandardError = stderr;
            InitializeDefaultEnvironment();
        }

        private void InitializeDefaultEnvironment()
        {
            if (CurrentUser != null)
            {
                Environment["USER"] = CurrentUser.Username;
                Environment["HOME"] = GetHomeDirectory();
            }
            Environment["PWD"] = WorkingDirectory;
            Environment["PATH"] = "/bin:/usr/bin:/usr/local/bin";            Environment["SHELL"] = "/bin/bash";
        }        public async Task WriteLineAsync(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text + Environment.NewLine);
            await StandardOutput.WriteAsync(bytes);
            await StandardOutput.FlushAsync();
        }

        public async Task WriteAsync(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await StandardOutput.WriteAsync(bytes);
            await StandardOutput.FlushAsync();
        }        public async Task WriteErrorAsync(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text + Environment.NewLine);
            await StandardError.WriteAsync(bytes);
            await StandardError.FlushAsync();
        }

        public async Task<string?> ReadLineAsync()
        {
            using var reader = new StreamReader(StandardInput, Encoding.UTF8, leaveOpen: true);
            return await reader.ReadLineAsync();
        }

        public async Task<bool> HasPermissionAsync(string path, FileAccessMode accessMode)
        {
            try
            {
                var resolvedPath = ResolvePath(path);
                // Delegate to file system for permission checking
                return await FileSystem.FileExistsAsync(resolvedPath, CurrentUser) || 
                       await FileSystem.DirectoryExistsAsync(resolvedPath, CurrentUser);
            }
            catch
            {                return false;
            }
        }

        public string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return WorkingDirectory;

            // Handle absolute paths
            if (path.StartsWith("/"))
                return IOPath.GetFullPath(path);

            // Handle home directory shortcut
            if (path.StartsWith("~"))
            {
                if (path.Length == 1)
                    return GetHomeDirectory();
                
                if (path.StartsWith("~/"))
                    return IOPath.GetFullPath(IOPath.Combine(GetHomeDirectory(), path.Substring(2)));
            }

            // Handle relative paths
            return IOPath.GetFullPath(IOPath.Combine(WorkingDirectory, path));
        }

        public string GetHomeDirectory()
        {
            return CurrentUser != null ? $"/home/{CurrentUser.Username}" : "/home/anonymous";
        }
    }
}
