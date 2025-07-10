using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.Shell;
using HackerOs.OS.User;

namespace HackerOs.OS.Shell.Commands
{
    /// <summary>
    /// Copy files and directories
    /// </summary>
    public class CpCommand : IShellCommandBase
    {
        private readonly IVirtualFileSystem _fileSystem;

        public override string Name => "cp";
        public override string Description => "Copy files and directories";
        public override string Usage => "cp [OPTIONS] SOURCE... DESTINATION";

        public CpCommand(IVirtualFileSystem fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public override async Task<CommandResult> ExecuteAsync(string[] args, IShellContext context)
        {
            if (args.Length < 2)
            {
                return CommandResult.Error("cp: missing destination file operand");
            }

            var (options, files) = ParseOptions(args);
            
            if (files.Length < 2)
            {
                return CommandResult.Error("cp: missing destination file operand");
            }

            bool recursive = options.Contains("-r");
            bool force = options.Contains("-f");
            bool verbose = options.Contains("-v");
            bool interactive = options.Contains("-i");

            var sources = files.Take(files.Length - 1).ToArray();
            var destination = files.Last();

            var destinationPath = context.FileSystem.GetAbsolutePath(destination, context.WorkingDirectory);
            var results = new List<string>();
            var errors = new List<string>();

            // Check if destination is a directory
            bool destIsDirectory = await context.FileSystem.DirectoryExistsAsync(destinationPath, context.CurrentUser);

            if (sources.Length > 1 && !destIsDirectory)
            {
                return CommandResult.Error($"cp: target '{destination}' is not a directory");
            }

            foreach (var source in sources)
            {
                try
                {
                    var sourcePath = context.FileSystem.GetAbsolutePath(source, context.WorkingDirectory);
                    
                    if (!await context.FileSystem.FileExistsAsync(sourcePath, context.CurrentUser) && 
                        !await context.FileSystem.DirectoryExistsAsync(sourcePath, context.CurrentUser))
                    {
                        errors.Add($"cp: cannot stat '{source}': No such file or directory");
                        continue;
                    }

                    var sourceNode = await context.FileSystem.GetNodeAsync(sourcePath, context.CurrentUser);
                    if (sourceNode == null || !sourceNode.CanRead(context.CurrentUser))
                    {
                        errors.Add($"cp: cannot open '{source}' for reading: Permission denied");
                        continue;
                    }

                    string targetPath;
                    if (destIsDirectory)
                    {
                        targetPath = HSystem.IO.HPath.Combine(destinationPath, HSystem.IO.HPath.GetFileName(sourcePath));
                    }
                    else
                    {
                        targetPath = destinationPath;
                    }

                    // Check if target exists and handle overwrite
                    bool targetExists = await context.FileSystem.FileExistsAsync(targetPath, context.CurrentUser) ||
                                      await context.FileSystem.DirectoryExistsAsync(targetPath, context.CurrentUser);

                    if (targetExists && !force)
                    {
                        if (interactive)
                        {
                            // In a real implementation, this would prompt the user
                            // For now, we'll assume "no" to overwrite
                            continue;
                        }
                        else
                        {
                            errors.Add($"cp: '{destination}' already exists (use -f to force)");
                            continue;
                        }
                    }

                    // Check if it's a directory and handle recursion
                    if (await context.FileSystem.DirectoryExistsAsync(sourcePath, context.CurrentUser))
                    {
                        if (!recursive)
                        {
                            errors.Add($"cp: -r not specified; omitting directory '{source}'");
                            continue;
                        }

                        await CopyDirectoryRecursiveAsync(sourcePath, targetPath, context.CurrentUser, verbose, results, errors);
                    }
                    else
                    {
                        await CopyFileAsync(sourcePath, targetPath, context.CurrentUser);
                        if (verbose)
                        {
                            results.Add($"'{source}' -> '{targetPath}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"cp: cannot copy '{source}': {ex.Message}");
                }
            }

            var output = string.Join(global::System.Environment.NewLine, results);
            var errorOutput = string.Join(global::System.Environment.NewLine, errors);

            if (errors.Any())
            {
                return CommandResult.Error(errorOutput, output);
            }

            return CommandResult.Success(output);
        }        private async Task CopyFileAsync(string sourcePath, string targetPath, User.User user)
        {
            var content = await _fileSystem.ReadAllTextAsync(sourcePath, user);
            await _fileSystem.WriteFileAsync(targetPath, content, user);
        }

        private async Task CopyDirectoryRecursiveAsync(string sourcePath, string targetPath, User.User user, 
            bool verbose, List<string> results, List<string> errors)
        {
            try
            {
                // Create target directory
                await _fileSystem.CreateDirectoryAsync(targetPath, user);
                if (verbose)
                {
                    results.Add($"created directory '{targetPath}'");
                }

                var children = await _fileSystem.ListDirectoryAsync(sourcePath, user);
                
                foreach (var child in children)
                {
                    var childSourcePath = HSystem.IO.HPath.Combine(sourcePath, child.Name);
                    var childTargetPath = HSystem.IO.HPath.Combine(targetPath, child.Name);
                    
                    if (await _fileSystem.DirectoryExistsAsync(childSourcePath, user))
                    {
                        await CopyDirectoryRecursiveAsync(childSourcePath, childTargetPath, user, verbose, results, errors);
                    }
                    else
                    {
                        if (child.CanRead(user))
                        {
                            await CopyFileAsync(childSourcePath, childTargetPath, user);
                            if (verbose)
                            {
                                results.Add($"'{childSourcePath}' -> '{childTargetPath}'");
                            }
                        }
                        else
                        {
                            errors.Add($"cp: cannot read '{childSourcePath}': Permission denied");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"cp: cannot copy directory '{sourcePath}': {ex.Message}");
            }
        }

        private (List<string> options, string[] files) ParseOptions(string[] args)
        {
            var options = new List<string>();
            var files = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                {
                    options.Add(args[i]);
                }
                else
                {
                    files.Add(args[i]);
                }
            }

            return (options, files.ToArray());
        }

        public override Task<IEnumerable<string>> GetCompletionsAsync(string[] args, int cursorPosition, IShellContext context)
        {
            // Simple file completion for cp command
            return Task.FromResult<IEnumerable<string>>(new List<string>());
        }
    }
}
