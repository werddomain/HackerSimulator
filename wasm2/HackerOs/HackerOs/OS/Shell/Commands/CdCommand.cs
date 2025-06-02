
using HackerOs.OS.IO.FileSystem;

namespace HackerOs.OS.Shell.Commands;

/// <summary>
/// Change directory command (cd)
/// </summary>
public class CdCommand : CommandBase
{
    public override string Name => "cd";
    public override string Description => "Change current directory";
    public override string Usage => "cd [DIRECTORY]";

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
            string targetDirectory;

            if (args.Length == 0)
            {
                // No arguments - go to home directory
                targetDirectory = context.UserSession.User.HomeDirectory;
            }
            else if (args.Length == 1)
            {
                targetDirectory = args[0];
                
                // Handle special cases
                if (targetDirectory == "-")
                {
                    // Go to previous directory (if we tracked it)
                    var previousDir = context.EnvironmentVariables.TryGetValue("OLDPWD", out var oldPwd) ? oldPwd : context.UserSession.User.HomeDirectory;
                    targetDirectory = previousDir;
                }
                else if (targetDirectory == "~")
                {
                    targetDirectory = context.UserSession.User.HomeDirectory;
                }
            }
            else
            {
                await WriteLineAsync(stderr, "cd: too many arguments");
                return 1;
            }

            // Attempt to change directory through the shell
            var success = await context.Shell.ChangeDirectoryAsync(targetDirectory);
            
            if (success)
            {
                // Update OLDPWD environment variable
                if (context.EnvironmentVariables.TryGetValue("PWD", out var currentPwd))
                {
                    context.Shell.SetEnvironmentVariable("OLDPWD", currentPwd);
                }
                
                return 0;
            }
            else
            {
                return 1; // Error message already displayed by shell
            }
        }
        catch (Exception ex)
        {
            await WriteLineAsync(stderr, $"cd: {ex.Message}");
            return 1;
        }
    }

    public override async Task<IEnumerable<string>> GetCompletionsAsync(
        CommandContext context,
        string[] args,
        string currentArg)
    {
        // Only complete directories for cd command
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

            // Get directory contents - only directories
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
        if (args.Length > 1)
        {
            return CommandValidationResult.Error("cd: too many arguments");
        }

        return CommandValidationResult.Success();
    }

    private static IVirtualFileSystem GetVirtualFileSystem(CommandContext context)
    {
        // This would be injected through DI in a real implementation
        throw new NotImplementedException("VFS access needs proper DI integration");
    }
}
