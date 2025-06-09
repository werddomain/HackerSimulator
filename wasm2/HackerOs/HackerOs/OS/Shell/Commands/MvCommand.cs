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
    /// Move (rename) files and directories
    /// </summary>
    public class MvCommand : CommandBase
    {
        private readonly IVirtualFileSystem _fileSystem;

        public MvCommand(IVirtualFileSystem fileSystem)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public override string Name => "mv";
        public override string Description => "Move (rename) files and directories";
        public override string Usage => "mv SOURCE DESTINATION";

        public override IReadOnlyList<CommandOption> Options => new List<CommandOption>
        {
            new("f", null, "Force move, overwrite existing files"),
            new("v", null, "Verbose mode, show files being moved")
        };

        public override async Task<int> ExecuteAsync(
            CommandContext context,
            string[] args,
            Stream stdin,
            Stream stdout,
            Stream stderr,
            CancellationToken cancellationToken = default)
        {
            if (args.Length < 2)
            {
                await WriteLineAsync(stderr, "mv: missing destination file operand", cancellationToken);
                return 1;
            }

            var source = args[0];
            var destination = args[1];

            try
            {
                var sourcePath = _fileSystem.GetAbsolutePath(source, context.WorkingDirectory);
                var destinationPath = _fileSystem.GetAbsolutePath(destination, context.WorkingDirectory);

                // Check if source exists
                if (!await _fileSystem.FileExistsAsync(sourcePath, context.CurrentUser) && 
                    !await _fileSystem.DirectoryExistsAsync(sourcePath, context.CurrentUser))
                {
                    await WriteLineAsync(stderr, $"mv: cannot stat '{source}': No such file or directory", cancellationToken);
                    return 1;
                }

                var sourceNode = await _fileSystem.GetNodeAsync(sourcePath, context.CurrentUser);
                if (sourceNode == null || !sourceNode.CanRead(context.CurrentUser))
                {
                    await WriteLineAsync(stderr, $"mv: cannot access '{source}': Permission denied", cancellationToken);
                    return 1;
                }

                // Check if destination exists
                bool destExists = await _fileSystem.FileExistsAsync(destinationPath, context.CurrentUser) ||
                                  await _fileSystem.DirectoryExistsAsync(destinationPath, context.CurrentUser);

                if (destExists)
                {
                    var destNode = await _fileSystem.GetNodeAsync(destinationPath, context.CurrentUser);
                    if (destNode != null && destNode.IsDirectory)
                    {
                        // Moving into a directory - create new path with source filename
                        var fileName = System.IO.Path.GetFileName(sourcePath);
                        destinationPath = System.IO.Path.Combine(destinationPath, fileName);
                    }
                    else
                    {
                        // Destination file exists - would overwrite
                        await WriteLineAsync(stderr, $"mv: '{destination}' already exists", cancellationToken);
                        return 1;
                    }
                }

                // Perform the move operation
                if (sourceNode.IsDirectory)
                {
                    await MoveDirectoryAsync(sourcePath, destinationPath, context.CurrentUser);
                }
                else
                {
                    await MoveFileAsync(sourcePath, destinationPath, context.CurrentUser);
                }

                return 0;
            }
            catch (Exception ex)
            {
                await WriteLineAsync(stderr, $"mv: {ex.Message}", cancellationToken);
                return 1;
            }
        }

        private async Task MoveFileAsync(string sourcePath, string targetPath, OS.User.User user)
        {
            var content = await _fileSystem.ReadAllTextAsync(sourcePath, user);
            await _fileSystem.CreateFileAsync(targetPath, user);
            await _fileSystem.WriteFileAsync(targetPath, content ?? string.Empty, user);
            await _fileSystem.DeleteFileAsync(sourcePath, user);
        }

        private async Task MoveDirectoryAsync(string sourcePath, string targetPath, OS.User.User user)
        {
            await _fileSystem.CreateDirectoryAsync(targetPath, user);
            
            var children = await _fileSystem.ListDirectoryAsync(sourcePath, user);
            foreach (var child in children.Where(c => c.CanRead(user)))
            {
                var childSourcePath = System.IO.Path.Combine(sourcePath, child.Name);
                var childTargetPath = System.IO.Path.Combine(targetPath, child.Name);

                if (child.IsDirectory)
                {
                    await MoveDirectoryAsync(childSourcePath, childTargetPath, user);
                }
                else
                {
                    await MoveFileAsync(childSourcePath, childTargetPath, user);
                }
            }

            await _fileSystem.DeleteDirectoryAsync(sourcePath, user);
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
