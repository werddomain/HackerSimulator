using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HackerOs.IO.FileSystem;
using HackerOs.OS.User;

namespace HackerOs.OS.Shell.Commands
{
    /// <summary>
    /// Search for files and directories
    /// </summary>
    public class FindCommand : CommandBase
    {
        private readonly IVirtualFileSystem _fileSystem;

        public FindCommand(IVirtualFileSystem fileSystem) : base("find")
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            Description = "Search for files and directories";
            Usage = "find [PATH...] [OPTIONS]";
            
            Options = new Dictionary<string, string>
            {
                { "-name", "Find by name pattern (supports wildcards)" },
                { "-type", "Find by type: f (file), d (directory)" },
                { "-size", "Find by size: +N (larger), -N (smaller), N (exact)" },
                { "-exec", "Execute command on found files" },
                { "-print", "Print found files (default)" },
                { "-maxdepth", "Maximum directory depth to search" },
                { "-mindepth", "Minimum directory depth to search" }
            };
        }

        public override async Task<CommandResult> ExecuteAsync(string[] args, IShellContext context)
        {
            var paths = new List<string>();
            var criteria = new FindCriteria();
            
            // Parse arguments
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                
                if (arg.StartsWith("-"))
                {
                    switch (arg)
                    {
                        case "-name":
                            if (i + 1 < args.Length)
                            {
                                criteria.NamePattern = args[++i];
                            }
                            break;
                        case "-type":
                            if (i + 1 < args.Length)
                            {
                                criteria.Type = args[++i];
                            }
                            break;
                        case "-size":
                            if (i + 1 < args.Length)
                            {
                                criteria.Size = args[++i];
                            }
                            break;
                        case "-maxdepth":
                            if (i + 1 < args.Length && int.TryParse(args[++i], out int maxDepth))
                            {
                                criteria.MaxDepth = maxDepth;
                            }
                            break;
                        case "-mindepth":
                            if (i + 1 < args.Length && int.TryParse(args[++i], out int minDepth))
                            {
                                criteria.MinDepth = minDepth;
                            }
                            break;
                        case "-exec":
                            // Handle -exec command {} \; format
                            var execArgs = new List<string>();
                            i++; // Skip -exec
                            while (i < args.Length && args[i] != "\\;")
                            {
                                execArgs.Add(args[i]);
                                i++;
                            }
                            criteria.ExecCommand = execArgs.ToArray();
                            break;
                        case "-print":
                            criteria.Print = true;
                            break;
                    }
                }
                else
                {
                    // It's a path
                    paths.Add(arg);
                }
            }

            // Default to current directory if no paths specified
            if (paths.Count == 0)
            {
                paths.Add(".");
            }

            // Default to print if no action specified
            if (!criteria.Print && criteria.ExecCommand == null)
            {
                criteria.Print = true;
            }

            var results = new List<string>();
            var errors = new List<string>();

            foreach (var path in paths)
            {
                try
                {
                    var searchPath = _fileSystem.GetAbsolutePath(path, context.CurrentDirectory);
                    await SearchAsync(searchPath, criteria, 0, context.CurrentUser, results, errors);
                }
                catch (Exception ex)
                {
                    errors.Add($"find: {path}: {ex.Message}");
                }
            }

            var output = string.Join(Environment.NewLine, results);
            var errorOutput = string.Join(Environment.NewLine, errors);

            if (errors.Any())
            {
                return CommandResult.Error(errorOutput, output);
            }

            return CommandResult.Success(output);
        }

        private async Task SearchAsync(string path, FindCriteria criteria, int currentDepth, User user,
            List<string> results, List<string> errors)
        {
            try
            {
                // Check depth constraints
                if (criteria.MaxDepth.HasValue && currentDepth > criteria.MaxDepth.Value)
                {
                    return;
                }

                bool pathExists = await _fileSystem.FileExistsAsync(path, user) || 
                                 await _fileSystem.DirectoryExistsAsync(path, user);
                
                if (!pathExists)
                {
                    errors.Add($"find: {path}: No such file or directory");
                    return;
                }

                var node = await _fileSystem.GetNodeAsync(path, user);
                if (node == null || !node.CanRead(user))
                {
                    errors.Add($"find: {path}: Permission denied");
                    return;
                }

                // Check if current item matches criteria
                bool matches = MatchesCriteria(node, path, criteria, currentDepth);
                
                if (matches)
                {
                    if (criteria.Print)
                    {
                        results.Add(path);
                    }

                    if (criteria.ExecCommand != null)
                    {
                        // Execute command on matched file
                        var expandedCommand = ExpandExecCommand(criteria.ExecCommand, path);
                        // In a real implementation, this would execute the command
                        results.Add($"Would execute: {string.Join(" ", expandedCommand)}");
                    }
                }

                // If it's a directory, recurse into it
                if (node.Type == VirtualFileSystemNodeType.Directory)
                {
                    try
                    {
                        var children = await _fileSystem.ListDirectoryAsync(path, user);
                        
                        foreach (var child in children.Where(c => c.CanRead(user)))
                        {
                            var childPath = System.IO.Path.Combine(path, child.Name);
                            await SearchAsync(childPath, criteria, currentDepth + 1, user, results, errors);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"find: {path}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"find: {path}: {ex.Message}");
            }
        }

        private bool MatchesCriteria(VirtualFileSystemNode node, string path, FindCriteria criteria, int currentDepth)
        {
            // Check depth constraints
            if (criteria.MinDepth.HasValue && currentDepth < criteria.MinDepth.Value)
            {
                return false;
            }

            // Check type filter
            if (!string.IsNullOrEmpty(criteria.Type))
            {
                switch (criteria.Type.ToLower())
                {
                    case "f":
                        if (node.Type != VirtualFileSystemNodeType.File)
                            return false;
                        break;
                    case "d":
                        if (node.Type != VirtualFileSystemNodeType.Directory)
                            return false;
                        break;
                }
            }

            // Check name pattern
            if (!string.IsNullOrEmpty(criteria.NamePattern))
            {
                var fileName = System.IO.Path.GetFileName(path);
                if (!MatchesPattern(fileName, criteria.NamePattern))
                {
                    return false;
                }
            }

            // Check size (for files only)
            if (!string.IsNullOrEmpty(criteria.Size) && node.Type == VirtualFileSystemNodeType.File)
            {
                if (!MatchesSize(node.Size, criteria.Size))
                {
                    return false;
                }
            }

            return true;
        }

        private bool MatchesPattern(string fileName, string pattern)
        {
            // Convert shell wildcards to regex
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            
            return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
        }

        private bool MatchesSize(long fileSize, string sizePattern)
        {
            // Parse size pattern: +N (larger), -N (smaller), N (exact)
            if (string.IsNullOrEmpty(sizePattern))
                return true;

            char modifier = sizePattern[0];
            string sizeStr;

            if (modifier == '+' || modifier == '-')
            {
                sizeStr = sizePattern.Substring(1);
            }
            else
            {
                modifier = '=';
                sizeStr = sizePattern;
            }

            if (!long.TryParse(sizeStr, out long targetSize))
            {
                return true; // Invalid pattern, ignore
            }

            switch (modifier)
            {
                case '+':
                    return fileSize > targetSize;
                case '-':
                    return fileSize < targetSize;
                case '=':
                default:
                    return fileSize == targetSize;
            }
        }

        private string[] ExpandExecCommand(string[] command, string filePath)
        {
            var expanded = new List<string>();
            
            foreach (var arg in command)
            {
                if (arg == "{}")
                {
                    expanded.Add(filePath);
                }
                else
                {
                    expanded.Add(arg);
                }
            }

            return expanded.ToArray();
        }

        public override Task<IEnumerable<string>> GetCompletionsAsync(string[] args, int cursorPosition, IShellContext context)
        {
            return GetFileCompletionsAsync(args, cursorPosition, context);
        }

        private class FindCriteria
        {
            public string? NamePattern { get; set; }
            public string? Type { get; set; }
            public string? Size { get; set; }
            public int? MaxDepth { get; set; }
            public int? MinDepth { get; set; }
            public string[]? ExecCommand { get; set; }
            public bool Print { get; set; }
        }
    }
}
