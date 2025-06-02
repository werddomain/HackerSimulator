using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;

namespace HackerOs.OS.Shell.Commands;

/// <summary>
/// Create directories command (mkdir)
/// </summary>
public class MkdirCommand : CommandBase
{
    public override string Name => "mkdir";
    public override string Description => "Create directories";
    public override string Usage => "mkdir [OPTIONS] DIRECTORY...";

    public override IReadOnlyList<CommandOption> Options => new List<CommandOption>
    {
        new("p", "parents", "Create parent directories as needed"),
        new("m", "mode", "Set file mode (permissions) for created directories", requiresValue: true),
        new("v", "verbose", "Print a message for each created directory")
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        string[] args,
        Stream stdin,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parsedArgs = ParseArguments(args, Options);
            
            if (!parsedArgs.Parameters.Any())
            {
                await WriteLineAsync(stderr, "mkdir: missing operand");
                await WriteLineAsync(stderr, $"Try 'mkdir --help' for more information.");
                return 1;
            }

            var vfs = GetVirtualFileSystem(context);
            var exitCode = 0;
            var createParents = parsedArgs.HasOption("p") || parsedArgs.HasOption("parents");
            var verbose = parsedArgs.HasOption("v") || parsedArgs.HasOption("verbose");

            foreach (var directory in parsedArgs.Parameters)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var absolutePath = vfs.GetAbsolutePath(directory, context.CurrentWorkingDirectory);
                    
                    // Check if directory already exists
                    if (await vfs.ExistsAsync(absolutePath))
                    {
                        await WriteLineAsync(stderr, $"mkdir: cannot create directory '{directory}': File exists");
                        exitCode = 1;
                        continue;
                    }

                    // Create directory
                    bool success;
                    if (createParents)
                    {
                        success = await CreateDirectoryRecursive(vfs, absolutePath, context, verbose, stdout);
                    }
                    else
                    {
                        success = await vfs.CreateDirectoryAsync(absolutePath);
                        if (success && verbose)
                        {
                            await WriteLineAsync(stdout, $"mkdir: created directory '{directory}'");
                        }
                    }

                    if (!success)
                    {
                        await WriteLineAsync(stderr, $"mkdir: cannot create directory '{directory}': Permission denied or parent directory doesn't exist");
                        exitCode = 1;
                    }
                }
                catch (Exception ex)
                {
                    await WriteLineAsync(stderr, $"mkdir: cannot create directory '{directory}': {ex.Message}");
                    exitCode = 1;
                }
            }

            return exitCode;
        }
        catch (Exception ex)
        {
            await WriteLineAsync(stderr, $"mkdir: {ex.Message}");
            return 1;
        }
    }

    private static async Task<bool> CreateDirectoryRecursive(
        IVirtualFileSystem vfs, 
        string path, 
        CommandContext context, 
        bool verbose, 
        Stream stdout)
    {
        // Split path into components
        var components = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var currentPath = "/";

        foreach (var component in components)
        {
            currentPath = currentPath == "/" ? $"/{component}" : $"{currentPath}/{component}";

            if (!await vfs.ExistsAsync(currentPath))
            {
                if (!await vfs.CreateDirectoryAsync(currentPath))
                {
                    return false;
                }

                if (verbose)
                {
                    await WriteLineAsync(stdout, $"mkdir: created directory '{currentPath}'");
                }
            }
        }

        return true;
    }    public override async Task<IEnumerable<string>> GetCompletionsAsync(
        CommandContext context,
        string[] args,
        string currentArg)
    {
        // Complete with directories (for parent path context)
        try
        {
            var vfs = GetVirtualFileSystem(context);
            var completions = new List<string>();

            // Resolve the base path for completion
            string basePath;
            string searchPattern;

            if (currentArg.Contains('/'))
            {
                var lastSlash = currentArg.LastIndexOf('/');
                basePath = currentArg[..lastSlash];
                searchPattern = currentArg[(lastSlash + 1)..];
            }
            else
            {
                basePath = context.CurrentWorkingDirectory;
                searchPattern = currentArg;
            }

            // Convert relative path to absolute
            basePath = vfs.GetAbsolutePath(basePath, context.CurrentWorkingDirectory);

            // Get directory contents - only directories for mkdir context
            if (await vfs.DirectoryExistsAsync(basePath, context.UserSession.User))
            {
                var entries = await vfs.ListDirectoryAsync(basePath);
                
                foreach (var entry in entries.Where(e => e.IsDirectory))
                {
                    if (entry.Name.StartsWith(searchPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        completions.Add($"{entry.Name}/");
                    }
                }
            }

            return completions;
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    public override CommandValidationResult ValidateArguments(string[] args)
    {
        if (args.Length == 0)
        {
            return CommandValidationResult.Error("mkdir: missing operand");
        }

        return CommandValidationResult.Success();
    }

    private static IVirtualFileSystem GetVirtualFileSystem(CommandContext context)
    {
        // This would be injected through DI in a real implementation
        throw new NotImplementedException("VFS access needs proper DI integration");
    }
}
