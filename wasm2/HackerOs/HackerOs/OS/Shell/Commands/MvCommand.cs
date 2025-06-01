using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.IO.FileSystem;
using HackerOs.OS.User;

namespace HackerOs.OS.Shell.Commands
{
    /// <summary>
    /// Move (rename) files and directories
    /// </summary>
    public class MvCommand : CommandBase
    {
        private readonly IVirtualFileSystem _fileSystem;

        public MvCommand(IVirtualFileSystem fileSystem) : base("mv")
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            Description = "Move (rename) files and directories";
            Usage = "mv [OPTIONS] SOURCE... DESTINATION";
            
            Options = new Dictionary<string, string>
            {
                { "-f", "Force move, overwrite existing files" },
                { "-v", "Verbose mode, show files being moved" },
                { "-i", "Interactive mode, prompt before overwrite" }
            };
        }

        public override async Task<CommandResult> ExecuteAsync(string[] args, IShellContext context)
        {
            if (args.Length < 2)
            {
                return CommandResult.Error("mv: missing destination file operand");
            }

            var options = ParseOptions(args, out var files);
            
            if (files.Length < 2)
            {
                return CommandResult.Error("mv: missing destination file operand");
            }

            bool force = options.Contains("-f");
            bool verbose = options.Contains("-v");
            bool interactive = options.Contains("-i");

            var sources = files.Take(files.Length - 1).ToArray();
            var destination = files.Last();

            var destinationPath = _fileSystem.GetAbsolutePath(destination, context.CurrentDirectory);
            var results = new List<string>();
            var errors = new List<string>();

            // Check if destination is a directory
            bool destIsDirectory = await _fileSystem.DirectoryExistsAsync(destinationPath, context.CurrentUser);

            if (sources.Length > 1 && !destIsDirectory)
            {
                return CommandResult.Error($"mv: target '{destination}' is not a directory");
            }

            foreach (var source in sources)
            {
                try
                {
                    var sourcePath = _fileSystem.GetAbsolutePath(source, context.CurrentDirectory);
                    
                    if (!await _fileSystem.FileExistsAsync(sourcePath, context.CurrentUser) && 
                        !await _fileSystem.DirectoryExistsAsync(sourcePath, context.CurrentUser))
                    {
                        errors.Add($"mv: cannot stat '{source}': No such file or directory");
                        continue;
                    }

                    var sourceNode = await _fileSystem.GetNodeAsync(sourcePath, context.CurrentUser);
                    if (sourceNode == null || !sourceNode.CanWrite(context.CurrentUser))
                    {
                        errors.Add($"mv: cannot move '{source}': Permission denied");
                        continue;
                    }

                    string targetPath;
                    if (destIsDirectory)
                    {
                        targetPath = System.IO.Path.Combine(destinationPath, System.IO.Path.GetFileName(sourcePath));
                    }
                    else
                    {
                        targetPath = destinationPath;
                    }

                    // Check if target exists and handle overwrite
                    bool targetExists = await _fileSystem.FileExistsAsync(targetPath, context.CurrentUser) ||
                                      await _fileSystem.DirectoryExistsAsync(targetPath, context.CurrentUser);

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
                            errors.Add($"mv: '{destination}' already exists (use -f to force)");
                            continue;
                        }
                    }

                    // If target exists and we're forcing, remove it first
                    if (targetExists && force)
                    {
                        var targetNode = await _fileSystem.GetNodeAsync(targetPath, context.CurrentUser);
                        if (targetNode != null)
                        {
                            if (targetNode.Type == VirtualFileSystemNodeType.Directory)
                            {
                                await _fileSystem.DeleteDirectoryAsync(targetPath, context.CurrentUser);
                            }
                            else
                            {
                                await _fileSystem.DeleteFileAsync(targetPath, context.CurrentUser);
                            }
                        }
                    }

                    // Perform the move operation
                    if (sourceNode.Type == VirtualFileSystemNodeType.Directory)
                    {
                        await MoveDirectoryAsync(sourcePath, targetPath, context.CurrentUser);
                    }
                    else
                    {
                        await MoveFileAsync(sourcePath, targetPath, context.CurrentUser);
                    }

                    if (verbose)
                    {
                        results.Add($"'{source}' -> '{targetPath}'");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"mv: cannot move '{source}': {ex.Message}");
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

        private async Task MoveFileAsync(string sourcePath, string targetPath, User user)
        {
            // For a simple move, we can copy then delete
            var content = await _fileSystem.ReadFileAsync(sourcePath, user);
            await _fileSystem.WriteFileAsync(targetPath, content, user);
            await _fileSystem.DeleteFileAsync(sourcePath, user);
        }

        private async Task MoveDirectoryAsync(string sourcePath, string targetPath, User user)
        {
            // Create target directory
            await _fileSystem.CreateDirectoryAsync(targetPath, user);

            // Move all children
            var children = await _fileSystem.ListDirectoryAsync(sourcePath, user);
            
            foreach (var child in children)
            {
                var childSourcePath = System.IO.Path.Combine(sourcePath, child.Name);
                var childTargetPath = System.IO.Path.Combine(targetPath, child.Name);
                
                if (child.Type == VirtualFileSystemNodeType.Directory)
                {
                    await MoveDirectoryAsync(childSourcePath, childTargetPath, user);
                }
                else
                {
                    await MoveFileAsync(childSourcePath, childTargetPath, user);
                }
            }

            // Remove the source directory
            await _fileSystem.DeleteDirectoryAsync(sourcePath, user);
        }

        public override Task<IEnumerable<string>> GetCompletionsAsync(string[] args, int cursorPosition, IShellContext context)
        {
            return GetFileCompletionsAsync(args, cursorPosition, context);
        }
    }
}
