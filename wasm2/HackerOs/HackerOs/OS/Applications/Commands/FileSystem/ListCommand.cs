using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.Applications.Core;
using HackerOs.OS.IO;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.Commands.FileSystem
{
    /// <summary>
    /// Command for listing directory contents
    /// This is a sample implementation using the new command application architecture
    /// </summary>
    [App("ListCommand", "system.list", 
        Description = "Lists files and directories",
        Type = ApplicationType.CommandApplication,
        IconPath = "/images/icons/terminal.png",
        Aliases = new[] { "ls", "dir", "list" })]
    public class ListCommand : CommandBase
    {
        #region Dependencies

        private readonly IVirtualFileSystem _fileSystem;
        private readonly ILogger<ListCommand> _logger;

        #endregion

        #region Command Options

        /// <summary>
        /// Display detailed information
        /// </summary>
        private bool _longFormat;

        /// <summary>
        /// Display hidden files
        /// </summary>
        private bool _showHidden;

        /// <summary>
        /// List directories recursively
        /// </summary>
        private bool _recursive;

        /// <summary>
        /// Sort by options
        /// </summary>
        private SortBy _sortBy = SortBy.Name;

        /// <summary>
        /// Sort in reverse order
        /// </summary>
        private bool _reverseSort;

        /// <summary>
        /// Output as one entry per line
        /// </summary>
        private bool _onePerLine;

        /// <summary>
        /// The target path to list
        /// </summary>
        private string _targetPath = ".";

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new ListCommand
        /// </summary>
        /// <param name="fileSystem">Virtual file system service</param>
        /// <param name="logger">Logger</param>
        public ListCommand(IVirtualFileSystem fileSystem, ILogger<ListCommand> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region Command Execution

        /// <summary>
        /// Execute the command
        /// </summary>
        /// <param name="args">Command arguments</param>
        protected override async Task<int> ExecuteAsync(string[] args)
        {
            try
            {
                // Parse command arguments
                if (!ParseArguments(args))
                {
                    // If argument parsing failed, it already printed help
                    return 1;
                }

                // Resolve the target path
                string resolvedPath = await ResolvePathAsync(_targetPath);
                if (string.IsNullOrEmpty(resolvedPath))
                {
                    await WriteErrorLineAsync($"Path not found: {_targetPath}");
                    return 1;
                }

                // Check if path exists
                bool isDirectory = await _fileSystem.DirectoryExistsAsync(resolvedPath);
                bool isFile = !isDirectory && await _fileSystem.FileExistsAsync(resolvedPath);

                if (!isDirectory && !isFile)
                {
                    await WriteErrorLineAsync($"Path not found: {resolvedPath}");
                    return 1;
                }

                // List a single file
                if (isFile)
                {
                    var fileInfo = await _fileSystem.GetFileInfoAsync(resolvedPath);
                    await ListSingleFileAsync(fileInfo);
                    return 0;
                }

                // List directory contents
                await ListDirectoryAsync(resolvedPath, _recursive ? 0 : -1);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing list command");
                await WriteErrorLineAsync($"Error: {ex.Message}");
                return 1;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Parse command arguments
        /// </summary>
        /// <param name="args">Arguments array</param>
        /// <returns>True if successful, false if help was shown</returns>
        private bool ParseArguments(string[] args)
        {
            List<string> nonOptions = new();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.StartsWith("-"))
                {
                    // Process options
                    if (arg == "-h" || arg == "--help")
                    {
                        ShowHelp();
                        return false;
                    }
                    else if (arg == "-l" || arg == "--long")
                    {
                        _longFormat = true;
                    }
                    else if (arg == "-a" || arg == "--all")
                    {
                        _showHidden = true;
                    }
                    else if (arg == "-r" || arg == "--recursive")
                    {
                        _recursive = true;
                    }
                    else if (arg == "--reverse")
                    {
                        _reverseSort = true;
                    }
                    else if (arg == "-1")
                    {
                        _onePerLine = true;
                    }
                    else if (arg.StartsWith("--sort="))
                    {
                        string sortOption = arg.Substring("--sort=".Length).ToLower();
                        switch (sortOption)
                        {
                            case "name":
                                _sortBy = SortBy.Name;
                                break;
                            case "size":
                                _sortBy = SortBy.Size;
                                break;
                            case "time":
                            case "date":
                                _sortBy = SortBy.Time;
                                break;
                            case "type":
                                _sortBy = SortBy.Type;
                                break;
                            default:
                                WriteErrorLineAsync($"Invalid sort option: {sortOption}").Wait();
                                ShowHelp();
                                return false;
                        }
                    }
                    else
                    {
                        // Handle combined short options like -la
                        if (arg.Length > 1)
                        {
                            for (int j = 1; j < arg.Length; j++)
                            {
                                switch (arg[j])
                                {
                                    case 'l':
                                        _longFormat = true;
                                        break;
                                    case 'a':
                                        _showHidden = true;
                                        break;
                                    case 'r':
                                        _recursive = true;
                                        break;
                                    case '1':
                                        _onePerLine = true;
                                        break;
                                    default:
                                        WriteErrorLineAsync($"Invalid option: -{arg[j]}").Wait();
                                        ShowHelp();
                                        return false;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Non-option argument (path)
                    nonOptions.Add(arg);
                }
            }

            // Set target path if provided
            if (nonOptions.Count > 0)
            {
                _targetPath = nonOptions[0];
            }

            return true;
        }

        /// <summary>
        /// Show command help
        /// </summary>
        private void ShowHelp()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Usage: ls [OPTIONS] [PATH]");
            sb.AppendLine();
            sb.AppendLine("List information about files and directories.");
            sb.AppendLine();
            sb.AppendLine("Options:");
            sb.AppendLine("  -l, --long      use a long listing format");
            sb.AppendLine("  -a, --all       do not ignore entries starting with .");
            sb.AppendLine("  -r, --recursive list subdirectories recursively");
            sb.AppendLine("  -1              list one file per line");
            sb.AppendLine("  --reverse       reverse order while sorting");
            sb.AppendLine("  --sort=WORD     sort by WORD: name, size, time, type");
            sb.AppendLine("  -h, --help      display this help and exit");
            sb.AppendLine();
            sb.AppendLine("Examples:");
            sb.AppendLine("  ls              List files in the current directory");
            sb.AppendLine("  ls -la          List all files including hidden ones with details");
            sb.AppendLine("  ls -r /home     List all files in /home and subdirectories");
            sb.AppendLine("  ls --sort=size  List files sorted by size");

            WriteLineAsync(sb.ToString()).Wait();
        }

        /// <summary>
        /// Resolve a path relative to the current directory
        /// </summary>
        /// <param name="path">Path to resolve</param>
        /// <returns>Resolved absolute path</returns>
        private async Task<string> ResolvePathAsync(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || path == ".")
                {
                    return WorkingDirectory;
                }

                if (path.StartsWith("/"))
                {
                    return path;
                }

                // Resolve relative path
                string resolvedPath = System.IO.Path.Combine(WorkingDirectory, path)
                    .Replace("\\", "/"); // Ensure forward slashes for virtual file system

                return resolvedPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving path: {Path}", path);
                return string.Empty;
            }
        }

        /// <summary>
        /// List a single file
        /// </summary>
        /// <param name="fileInfo">File information</param>
        private async Task ListSingleFileAsync(FileInfo fileInfo)
        {
            if (_longFormat)
            {
                await WriteLineAsync(FormatFileDetails(fileInfo));
            }
            else
            {
                await WriteLineAsync(fileInfo.Name);
            }
        }

        /// <summary>
        /// List directory contents
        /// </summary>
        /// <param name="directoryPath">Directory path</param>
        /// <param name="recursionLevel">Recursion level, -1 for no recursion, 0+ for recursion depth</param>
        private async Task ListDirectoryAsync(string directoryPath, int recursionLevel)
        {
            try
            {
                // Get directory entries
                var entries = await _fileSystem.GetDirectoryEntriesAsync(directoryPath);

                // Filter hidden files if needed
                if (!_showHidden)
                {
                    entries = entries.Where(e => !e.Name.StartsWith(".")).ToList();
                }

                // Sort entries
                entries = SortEntries(entries).ToList();

                // If not at top level, print directory name
                if (recursionLevel >= 0)
                {
                    await WriteLineAsync();
                    await WriteLineAsync($"{directoryPath}:");
                }

                // List directories first, then files
                var directories = entries.Where(e => e.IsDirectory).ToList();
                var files = entries.Where(e => !e.IsDirectory).ToList();

                // Display entries
                if (_longFormat)
                {
                    // Header for long format
                    if (files.Count > 0 || directories.Count > 0)
                    {
                        await WriteLineAsync("Permissions  Size      Modified             Name");
                        await WriteLineAsync("----------- --------- ------------------- ----------");
                    }

                    // List directories
                    foreach (var dir in directories)
                    {
                        await WriteLineAsync(FormatDirectoryDetails(dir));
                    }

                    // List files
                    foreach (var file in files)
                    {
                        await WriteLineAsync(FormatFileDetails(file));
                    }
                }
                else if (_onePerLine)
                {
                    // One entry per line
                    foreach (var dir in directories)
                    {
                        await WriteLineAsync($"{FormatDirectoryName(dir.Name)}/");
                    }

                    foreach (var file in files)
                    {
                        await WriteLineAsync(file.Name);
                    }
                }
                else
                {
                    // Compact format (multiple entries per line)
                    int terminalWidth = TerminalWidth;
                    int maxNameLength = Math.Max(
                        directories.Count > 0 ? directories.Max(d => d.Name.Length) + 1 : 0, // +1 for slash
                        files.Count > 0 ? files.Max(f => f.Name.Length) : 0
                    );

                    // Ensure minimum spacing
                    maxNameLength = Math.Max(maxNameLength + 2, 12);
                    maxNameLength = Math.Min(maxNameLength, 30); // Cap at 30 chars

                    int columns = Math.Max(1, terminalWidth / maxNameLength);
                    int currentColumn = 0;

                    // List directories
                    foreach (var dir in directories)
                    {
                        await WriteAsync($"{FormatDirectoryName(dir.Name)}/".PadRight(maxNameLength));
                        currentColumn++;

                        if (currentColumn >= columns)
                        {
                            await WriteLineAsync();
                            currentColumn = 0;
                        }
                    }

                    // List files
                    foreach (var file in files)
                    {
                        await WriteAsync(file.Name.PadRight(maxNameLength));
                        currentColumn++;

                        if (currentColumn >= columns)
                        {
                            await WriteLineAsync();
                            currentColumn = 0;
                        }
                    }

                    // Final newline if needed
                    if (currentColumn > 0)
                    {
                        await WriteLineAsync();
                    }
                }

                // Recursively list subdirectories if requested
                if (recursionLevel != -1)
                {
                    foreach (var dir in directories)
                    {
                        await ListDirectoryAsync(dir.FullPath, recursionLevel + 1);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing directory: {DirectoryPath}", directoryPath);
                await WriteErrorLineAsync($"Error accessing {directoryPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Sort directory entries according to options
        /// </summary>
        /// <param name="entries">Directory entries</param>
        /// <returns>Sorted entries</returns>
        private IEnumerable<FileSystemEntry> SortEntries(IEnumerable<FileSystemEntry> entries)
        {
            IOrderedEnumerable<FileSystemEntry> sorted;

            switch (_sortBy)
            {
                case SortBy.Name:
                    sorted = entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);
                    break;
                case SortBy.Size:
                    sorted = entries.OrderBy(e => e.IsDirectory ? 0 : e.Size);
                    break;
                case SortBy.Time:
                    sorted = entries.OrderBy(e => e.LastModified);
                    break;
                case SortBy.Type:
                    sorted = entries.OrderBy(e => e.IsDirectory ? "" : GetFileExtension(e.Name))
                            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    sorted = entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);
                    break;
            }

            return _reverseSort ? sorted.Reverse() : sorted;
        }

        /// <summary>
        /// Format directory details for long listing
        /// </summary>
        /// <param name="entry">Directory entry</param>
        /// <returns>Formatted string</returns>
        private string FormatDirectoryDetails(FileSystemEntry entry)
        {
            string permissions = "drwxr-xr-x"; // Default directory permissions
            string size = "<DIR>".PadLeft(9);
            string modified = entry.LastModified.ToString("yyyy-MM-dd HH:mm");
            string name = FormatDirectoryName(entry.Name);

            return $"{permissions} {size} {modified} {name}/";
        }

        /// <summary>
        /// Format file details for long listing
        /// </summary>
        /// <param name="entry">File entry</param>
        /// <returns>Formatted string</returns>
        private string FormatFileDetails(FileSystemEntry entry)
        {
            string permissions = "-rw-r--r--"; // Default file permissions
            string size = FormatFileSize(entry.Size);
            string modified = entry.LastModified.ToString("yyyy-MM-dd HH:mm");
            string name = entry.Name;

            return $"{permissions} {size} {modified} {name}";
        }

        /// <summary>
        /// Format directory name with color (if supported)
        /// </summary>
        /// <param name="name">Directory name</param>
        /// <returns>Formatted name</returns>
        private string FormatDirectoryName(string name)
        {
            // Use ANSI color codes if supported
            if (SupportsAnsiColor)
            {
                return $"\u001b[1;34m{name}\u001b[0m"; // Bold blue
            }
            return name;
        }

        /// <summary>
        /// Format file size with appropriate units
        /// </summary>
        /// <param name="bytes">Size in bytes</param>
        /// <returns>Formatted size</returns>
        private string FormatFileSize(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            string size;
            if (bytes >= GB)
            {
                size = $"{bytes / (double)GB:F1}G";
            }
            else if (bytes >= MB)
            {
                size = $"{bytes / (double)MB:F1}M";
            }
            else if (bytes >= KB)
            {
                size = $"{bytes / (double)KB:F1}K";
            }
            else
            {
                size = $"{bytes}B";
            }

            return size.PadLeft(9);
        }

        /// <summary>
        /// Get file extension
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>File extension or empty string</returns>
        private string GetFileExtension(string fileName)
        {
            int lastDot = fileName.LastIndexOf('.');
            if (lastDot > 0 && lastDot < fileName.Length - 1)
            {
                return fileName.Substring(lastDot + 1).ToLower();
            }
            return string.Empty;
        }

        #endregion
    }

    /// <summary>
    /// Sort options for list command
    /// </summary>
    public enum SortBy
    {
        /// <summary>
        /// Sort by name
        /// </summary>
        Name,

        /// <summary>
        /// Sort by size
        /// </summary>
        Size,

        /// <summary>
        /// Sort by modification time
        /// </summary>
        Time,

        /// <summary>
        /// Sort by file type/extension
        /// </summary>
        Type
    }
}
