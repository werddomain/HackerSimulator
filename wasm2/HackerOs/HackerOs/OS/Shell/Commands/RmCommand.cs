using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.HSystem.IO;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.Shell;
using HackerOs.OS.User;

namespace HackerOs.OS.Shell.Commands
{
    /// <summary>
    /// Remove (delete) files and directories
    /// </summary>
    public class RmCommand : CommandBase
    {
        private readonly IVirtualFileSystem _fileSystem;

        public RmCommand(IVirtualFileSystem fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public override string Name => "rm";
        public override string Description => "Remove files and directories";
        public override string Usage => "rm [OPTIONS] FILE...";

        public override IReadOnlyList<CommandOption> Options => new List<CommandOption>
        {
            new("f", null, "Force removal, ignore nonexistent files"),
            new("r", null, "Remove directories and their contents recursively"),
            new("v", null, "Verbose mode, show what is being removed")
        };        public override async Task<int> ExecuteAsync(
            CommandContext context,
            string[] args,
            Stream stdin,
            Stream stdout,
            Stream stderr,
            CancellationToken cancellationToken = default)
        {
            if (args.Length == 0)
            {
                await WriteLineAsync(stderr, "rm: missing operand");
                return 1;
            }

            var parsedArgs = ParseArguments(args, Options);
            var files = parsedArgs.Parameters;
            
            if (files.Count == 0)
            {
                await WriteLineAsync(stderr, "rm: missing operand");
                return 1;
            }

            bool force = parsedArgs.HasOption("f");
            bool recursive = parsedArgs.HasOption("r");
            bool verbose = parsedArgs.HasOption("v");

            var results = new List<string>();
            var errors = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var path = _fileSystem.GetAbsolutePath(file, context.WorkingDirectory);
                    
                    if (!await _fileSystem.FileExistsAsync(path, context.CurrentUser) && 
                        !await _fileSystem.DirectoryExistsAsync(path, context.CurrentUser))
                    {
                        if (!force)
                        {
                            errors.Add($"rm: cannot remove '{file}': No such file or directory");
                        }
                        continue;
                    }

                    var node = await _fileSystem.GetNodeAsync(path, context.CurrentUser);
                    if (node == null)
                    {
                        if (!force)
                        {
                            errors.Add($"rm: cannot remove '{file}': Permission denied");
                        }
                        continue;
                    }

                    if (!node.CanWrite(context.CurrentUser))
                    {
                        if (!force)
                        {
                            errors.Add($"rm: cannot remove '{file}': Permission denied");
                        }
                        continue;
                    }

                    if (node.IsDirectory)
                    {
                        if (!recursive)
                        {
                            errors.Add($"rm: cannot remove '{file}': Is a directory");
                            continue;
                        }

                        await RemoveDirectoryRecursiveAsync(path, context.CurrentUser, verbose, results, errors, force);
                    }
                    else
                    {
                        await _fileSystem.DeleteFileAsync(path, context.CurrentUser);
                        if (verbose)
                        {
                            results.Add($"removed '{file}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!force)
                    {
                        errors.Add($"rm: cannot remove '{file}': {ex.Message}");
                    }
                }
            }

            // Write output
            foreach (var result in results)
            {
                await WriteLineAsync(stdout, result);
            }

            // Write errors
            foreach (var error in errors)
            {
                await WriteLineAsync(stderr, error);
            }

            if (errors.Any() && !force)
            {
                return 1;
            }

            return 0;        }

        private async Task RemoveDirectoryRecursiveAsync(string path, OS.User.User user, bool verbose, 
            List<string> results, List<string> errors, bool force)
        {
            try
            {
                var children = await _fileSystem.ListDirectoryAsync(path, user);
                
                foreach (var child in children)
                {
                    var childPath = HSystem.IO.HPath.Combine(path, child.Name);
                    
                    if (child.IsDirectory)
                    {
                        await RemoveDirectoryRecursiveAsync(childPath, user, verbose, results, errors, force);
                    }
                    else
                    {
                        if (child.CanWrite(user))
                        {
                            await _fileSystem.DeleteFileAsync(childPath, user);
                            if (verbose)
                            {
                                results.Add($"removed '{childPath}'");
                            }
                        }
                        else if (!force)
                        {
                            errors.Add($"rm: cannot remove '{childPath}': Permission denied");
                        }
                    }
                }

                await _fileSystem.DeleteDirectoryAsync(path, user);
                if (verbose)
                {
                    results.Add($"removed directory '{path}'");
                }
            }
            catch (Exception ex)
            {
                if (!force)
                {
                    errors.Add($"rm: cannot remove '{path}': {ex.Message}");
                }
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
