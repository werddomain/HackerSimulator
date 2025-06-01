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
    /// Search text patterns in files
    /// </summary>
    public class GrepCommand : CommandBase
    {
        private readonly IVirtualFileSystem _fileSystem;

        public GrepCommand(IVirtualFileSystem fileSystem) : base("grep")
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            Description = "Search text patterns in files";
            Usage = "grep [OPTIONS] PATTERN [FILE...]";
            
            Options = new Dictionary<string, string>
            {
                { "-i", "Ignore case" },
                { "-v", "Invert match, select non-matching lines" },
                { "-n", "Show line numbers" },
                { "-r", "Search directories recursively" },
                { "-l", "Show only filenames with matches" },
                { "-c", "Show only count of matching lines" },
                { "-A", "Show N lines after match" },
                { "-B", "Show N lines before match" },
                { "-C", "Show N lines around match" }
            };
        }

        public override async Task<CommandResult> ExecuteAsync(string[] args, IShellContext context)
        {
            if (args.Length == 0)
            {
                return CommandResult.Error("grep: missing pattern");
            }

            var options = ParseOptions(args, out var remaining);
            
            if (remaining.Length == 0)
            {
                return CommandResult.Error("grep: missing pattern");
            }

            var pattern = remaining[0];
            var files = remaining.Skip(1).ToArray();

            // If no files specified, read from stdin (not implemented in this context)
            if (files.Length == 0)
            {
                return CommandResult.Error("grep: reading from stdin not implemented");
            }

            bool ignoreCase = options.Contains("-i");
            bool invertMatch = options.Contains("-v");
            bool showLineNumbers = options.Contains("-n");
            bool recursive = options.Contains("-r");
            bool showFilenamesOnly = options.Contains("-l");
            bool showCount = options.Contains("-c");

            // Parse context options
            int afterContext = GetContextValue(options, "-A");
            int beforeContext = GetContextValue(options, "-B");
            int aroundContext = GetContextValue(options, "-C");

            if (aroundContext > 0)
            {
                afterContext = beforeContext = aroundContext;
            }

            var regexOptions = RegexOptions.Compiled;
            if (ignoreCase)
            {
                regexOptions |= RegexOptions.IgnoreCase;
            }

            Regex regex;
            try
            {
                regex = new Regex(pattern, regexOptions);
            }
            catch (ArgumentException ex)
            {
                return CommandResult.Error($"grep: invalid pattern: {ex.Message}");
            }

            var results = new List<string>();
            var errors = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var path = _fileSystem.GetAbsolutePath(file, context.CurrentDirectory);
                    await SearchInPathAsync(path, regex, invertMatch, showLineNumbers, recursive, 
                        showFilenamesOnly, showCount, beforeContext, afterContext, 
                        context.CurrentUser, results, errors, files.Length > 1);
                }
                catch (Exception ex)
                {
                    errors.Add($"grep: {file}: {ex.Message}");
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

        private async Task SearchInPathAsync(string path, Regex regex, bool invertMatch, bool showLineNumbers,
            bool recursive, bool showFilenamesOnly, bool showCount, int beforeContext, int afterContext,
            User user, List<string> results, List<string> errors, bool multipleFiles)
        {
            if (await _fileSystem.DirectoryExistsAsync(path, user))
            {
                if (!recursive)
                {
                    errors.Add($"grep: {path}: Is a directory");
                    return;
                }

                var children = await _fileSystem.ListDirectoryAsync(path, user);
                foreach (var child in children.Where(c => c.CanRead(user)))
                {
                    var childPath = System.IO.Path.Combine(path, child.Name);
                    await SearchInPathAsync(childPath, regex, invertMatch, showLineNumbers, recursive,
                        showFilenamesOnly, showCount, beforeContext, afterContext, user, results, errors, true);
                }
                return;
            }

            if (!await _fileSystem.FileExistsAsync(path, user))
            {
                errors.Add($"grep: {path}: No such file or directory");
                return;
            }

            var node = await _fileSystem.GetNodeAsync(path, user);
            if (node == null || !node.CanRead(user))
            {
                errors.Add($"grep: {path}: Permission denied");
                return;
            }

            if (node.Type != VirtualFileSystemNodeType.File)
            {
                return; // Skip non-files
            }

            try
            {
                var content = await _fileSystem.ReadFileAsync(path, user);
                var lines = content.Split('\n');
                
                var matches = new List<(int LineNumber, string Line)>();
                
                for (int i = 0; i < lines.Length; i++)
                {
                    bool isMatch = regex.IsMatch(lines[i]);
                    if (invertMatch)
                    {
                        isMatch = !isMatch;
                    }
                    
                    if (isMatch)
                    {
                        matches.Add((i + 1, lines[i]));
                    }
                }

                if (showCount)
                {
                    var prefix = multipleFiles ? $"{path}:" : "";
                    results.Add($"{prefix}{matches.Count}");
                    return;
                }

                if (showFilenamesOnly)
                {
                    if (matches.Any())
                    {
                        results.Add(path);
                    }
                    return;
                }

                if (beforeContext > 0 || afterContext > 0)
                {
                    var contextResults = GetMatchesWithContext(lines, matches, beforeContext, afterContext, showLineNumbers, multipleFiles ? path : null);
                    results.AddRange(contextResults);
                }
                else
                {
                    foreach (var (lineNumber, line) in matches)
                    {
                        var prefix = "";
                        if (multipleFiles)
                        {
                            prefix += $"{path}:";
                        }
                        if (showLineNumbers)
                        {
                            prefix += $"{lineNumber}:";
                        }
                        results.Add($"{prefix}{line}");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"grep: {path}: {ex.Message}");
            }
        }

        private List<string> GetMatchesWithContext(string[] lines, List<(int LineNumber, string Line)> matches,
            int beforeContext, int afterContext, bool showLineNumbers, string filename)
        {
            var results = new List<string>();
            var processedLines = new HashSet<int>();

            foreach (var (lineNumber, line) in matches)
            {
                var start = Math.Max(0, lineNumber - 1 - beforeContext);
                var end = Math.Min(lines.Length - 1, lineNumber - 1 + afterContext);

                for (int i = start; i <= end; i++)
                {
                    if (processedLines.Contains(i))
                        continue;

                    processedLines.Add(i);
                    var prefix = "";
                    if (!string.IsNullOrEmpty(filename))
                    {
                        prefix += $"{filename}:";
                    }
                    if (showLineNumbers)
                    {
                        prefix += $"{i + 1}:";
                    }

                    // Add separator for context lines vs match lines
                    var separator = matches.Any(m => m.LineNumber == i + 1) ? "" : "-";
                    if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(separator))
                    {
                        prefix = prefix.TrimEnd(':') + separator;
                    }

                    results.Add($"{prefix}{lines[i]}");
                }

                // Add separator between match groups
                if (results.Any() && lineNumber < matches.Last().LineNumber)
                {
                    results.Add("--");
                }
            }

            return results;
        }

        private int GetContextValue(List<string> options, string optionName)
        {
            var option = options.FirstOrDefault(o => o.StartsWith(optionName));
            if (option == null) return 0;

            if (option.Length > optionName.Length)
            {
                if (int.TryParse(option.Substring(optionName.Length), out int value))
                {
                    return value;
                }
            }

            return 1; // Default context of 1 line
        }

        public override Task<IEnumerable<string>> GetCompletionsAsync(string[] args, int cursorPosition, IShellContext context)
        {
            // For grep, we want to complete file names for the second argument onwards
            if (args.Length <= 1)
            {
                return Task.FromResult(Enumerable.Empty<string>());
            }

            return GetFileCompletionsAsync(args, cursorPosition, context);
        }
    }
}
