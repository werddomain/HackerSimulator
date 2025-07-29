using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.Shell;
using HackerOs.OS.User;

namespace HackerOs.OS.Shell.Commands
{
    /// <summary>
    /// Search text patterns in files
    /// </summary>
    public class GrepCommand : CommandBase
    {
        private readonly IVirtualFileSystem _fileSystem;

        public GrepCommand(IVirtualFileSystem fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public override string Name => "grep";
        public override string Description => "Search text patterns in files";
        public override string Usage => "grep [OPTIONS] PATTERN [FILE...]";

        public override IReadOnlyList<CommandOption> Options => new List<CommandOption>
        {
            new("i", null, "Ignore case"),
            new("v", null, "Invert match, select non-matching lines"),
            new("n", null, "Show line numbers"),
            new("r", null, "Search directories recursively"),
            new("l", null, "Show only filenames with matches"),
            new("c", null, "Show only count of matching lines")
        };

        public override async Task<int> ExecuteAsync(
            CommandContext context,
            string[] args,
            Stream stdin,
            Stream stdout,
            Stream stderr,
            CancellationToken cancellationToken = default)
        {
            if (args.Length == 0)
            {
                await WriteLineAsync(stderr, "grep: missing pattern", cancellationToken);
                return 1;
            }

            // Simple argument parsing - first arg is pattern, rest are files
            var pattern = args[0];
            var files = args.Skip(1).ToArray();

            if (files.Length == 0)
            {
                await WriteLineAsync(stderr, "grep: no files specified", cancellationToken);
                return 1;
            }

            var results = new List<string>();
            var errors = new List<string>();
            bool hasMatches = false;

            try
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);

                foreach (var file in files)
                {
                    try
                    {
                        var filePath = _fileSystem.GetAbsolutePath(file, context.WorkingDirectory);
                        
                        if (!await _fileSystem.FileExistsAsync(filePath, context.CurrentUser))
                        {
                            errors.Add($"grep: {file}: No such file or directory");
                            continue;
                        }

                        var node = await _fileSystem.GetNodeAsync(filePath, context.CurrentUser);
                        if (node == null || !node.CanRead(context.CurrentUser))
                        {
                            errors.Add($"grep: {file}: Permission denied");
                            continue;
                        }

                        if (node.IsDirectory)
                        {
                            errors.Add($"grep: {file}: Is a directory");
                            continue;
                        }                        var content = await _fileSystem.ReadAllTextAsync(filePath, context.CurrentUser);
                        if (content == null) continue;
                        
                        var lines = content.Split('\n');

                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (regex.IsMatch(lines[i]))
                            {
                                hasMatches = true;
                                var lineNumber = i + 1;
                                var output = files.Length > 1 ? $"{file}:{lineNumber}:{lines[i]}" : $"{lineNumber}:{lines[i]}";
                                results.Add(output);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"grep: {file}: {ex.Message}");
                    }
                }

                // Write results to stdout
                if (results.Any())
                {
                    var output = string.Join(global::System.Environment.NewLine, results);
                    await WriteLineAsync(stdout, output, cancellationToken);
                }

                // Write errors to stderr
                if (errors.Any())
                {
                    var errorOutput = string.Join(global::System.Environment.NewLine, errors);
                    await WriteLineAsync(stderr, errorOutput, cancellationToken);
                }

                return hasMatches ? 0 : 1;
            }
            catch (Exception ex)
            {
                await WriteLineAsync(stderr, $"grep: {ex.Message}", cancellationToken);
                return 1;
            }
        }

        public override Task<IEnumerable<string>> GetCompletionsAsync(
            CommandContext context,
            string[] args,
            string currentArg)
        {
            return GetFileCompletionsAsync(context, currentArg);
        }
    }
}
