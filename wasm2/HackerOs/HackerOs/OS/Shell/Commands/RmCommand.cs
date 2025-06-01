using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.IO.FileSystem;
using HackerOs.OS.User;

namespace HackerOs.OS.Shell.Commands
{
    /// <summary>
    /// Remove (delete) files and directories
    /// </summary>
    public class RmCommand : CommandBase
    {
        private readonly IVirtualFileSystem _fileSystem;

        public RmCommand(IVirtualFileSystem fileSystem) : base("rm")
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            Description = "Remove files and directories";
            Usage = "rm [OPTIONS] FILE...";
            
            Options = new Dictionary<string, string>
            {
                { "-f", "Force removal, ignore nonexistent files" },
                { "-r", "Remove directories and their contents recursively" },
                { "-rf", "Force recursive removal" },
                { "-v", "Verbose mode, show what is being removed" }
            };
        }

        public override async Task<CommandResult> ExecuteAsync(string[] args, IShellContext context)
        {
            if (args.Length == 0)
            {
                return CommandResult.Error("rm: missing operand");
            }

            var options = ParseOptions(args, out var files);
            
            if (files.Length == 0)
            {
                return CommandResult.Error("rm: missing operand");
            }

            bool force = options.Contains("-f") || options.Contains("-rf");
            bool recursive = options.Contains("-r") || options.Contains("-rf");
            bool verbose = options.Contains("-v");

            var results = new List<string>();
            var errors = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var path = _fileSystem.GetAbsolutePath(file, context.CurrentDirectory);
                    
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

                    if (node.Type == VirtualFileSystemNodeType.Directory)
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

            var output = string.Join(Environment.NewLine, results);
            var errorOutput = string.Join(Environment.NewLine, errors);

            if (errors.Any() && !force)
            {
                return CommandResult.Error(errorOutput, output);
            }

            return CommandResult.Success(output);
        }

        private async Task RemoveDirectoryRecursiveAsync(string path, User user, bool verbose, 
            List<string> results, List<string> errors, bool force)
        {
            try
            {
                var children = await _fileSystem.ListDirectoryAsync(path, user);
                
                foreach (var child in children)
                {
                    var childPath = System.IO.Path.Combine(path, child.Name);
                    
                    if (child.Type == VirtualFileSystemNodeType.Directory)
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

        public override Task<IEnumerable<string>> GetCompletionsAsync(string[] args, int cursorPosition, IShellContext context)
        {
            return GetFileCompletionsAsync(args, cursorPosition, context);
        }
    }
}
