
using System.Text;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.Shell;

namespace HackerOs.OS.Shell.Commands;

/// <summary>
/// Concatenate and display file contents command (cat)
/// </summary>
public class CatCommand : CommandBase
{
    public override string Name => "cat";
    public override string Description => "Concatenate and display file contents";
    public override string Usage => "cat [OPTIONS] [FILE...]";

    public override IReadOnlyList<CommandOption> Options => new List<CommandOption>
    {
        new("n", "number", "Number all output lines"),
        new("b", "number-nonblank", "Number non-empty output lines"),
        new("s", "squeeze-blank", "Suppress repeated empty output lines"),
        new("e", "show-ends", "Display $ at end of each line"),
        new("t", "show-tabs", "Display TAB characters as ^I"),
        new("A", "show-all", "Equivalent to -vET")
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
            var vfs = GetVirtualFileSystem(context);
            
            if (!parsedArgs.Parameters.Any())
            {
                // No files specified, read from stdin
                await ProcessStream(stdin, stdout, stderr, parsedArgs, "<stdin>", cancellationToken);
                return 0;
            }

            var exitCode = 0;
            foreach (var filePath in parsedArgs.Parameters)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var absolutePath = vfs.GetAbsolutePath(filePath, context.CurrentWorkingDirectory);
                
                if (!await vfs.FileExistsAsync(absolutePath, context.UserSession.User))
                {
                    await WriteLineAsync(stderr, $"cat: {filePath}: No such file or directory");
                    exitCode = 1;
                    continue;
                }

                var node = await vfs.GetNodeAsync(absolutePath, context.UserSession.User);
                if (node == null || !node.CanRead(context.UserSession.User))
                {
                    await WriteLineAsync(stderr, $"cat: {filePath}: Permission denied");
                    exitCode = 1;
                    continue;
                }

                if (node.IsDirectory)
                {
                    await WriteLineAsync(stderr, $"cat: {filePath}: Is a directory");
                    exitCode = 1;
                    continue;
                }

                try
                {                    
                    var content = await vfs.ReadAllTextAsync(absolutePath, context.UserSession.User);
                    using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(content ?? ""));
                    await ProcessStream(contentStream, stdout, stderr, parsedArgs, filePath, cancellationToken);
                }
                catch (Exception ex)
                {
                    await WriteLineAsync(stderr, $"cat: {filePath}: {ex.Message}");
                    exitCode = 1;
                }
            }

            return exitCode;
        }
        catch (Exception ex)
        {
            await WriteLineAsync(stderr, $"cat: {ex.Message}");
            return 1;
        }
    }

    private static async Task ProcessStream(
        Stream input,
        Stream output,
        Stream error,
        ParsedArguments args,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(input, Encoding.UTF8);
        
        var lineNumber = 1;
        var nonBlankLineNumber = 1;
        var previousLineWasEmpty = false;
        
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var isEmpty = string.IsNullOrEmpty(line);
            
            // Handle squeeze-blank option
            if (args.HasOption("s") || args.HasOption("squeeze-blank"))
            {
                if (isEmpty && previousLineWasEmpty)
                {
                    continue; // Skip repeated empty lines
                }
            }
            
            var processedLine = line;
            
            // Handle show options
            if (args.HasOption("t") || args.HasOption("show-tabs") || args.HasOption("A") || args.HasOption("show-all"))
            {
                processedLine = processedLine.Replace("\t", "^I");
            }
            
            if (args.HasOption("e") || args.HasOption("show-ends") || args.HasOption("A") || args.HasOption("show-all"))
            {
                processedLine += "$";
            }
            
            // Handle line numbering
            if (args.HasOption("n") || args.HasOption("number"))
            {
                processedLine = $"{lineNumber,6}\t{processedLine}";
            }
            else if ((args.HasOption("b") || args.HasOption("number-nonblank")) && !isEmpty)
            {
                processedLine = $"{nonBlankLineNumber,6}\t{processedLine}";
                nonBlankLineNumber++;
            }
            
            await WriteLineAsync(output, processedLine);
            
            lineNumber++;
            previousLineWasEmpty = isEmpty;
        }
    }

    public override async Task<IEnumerable<string>> GetCompletionsAsync(
        CommandContext context,
        string[] args,
        string currentArg)
    {
        // For cat command, complete with files (not directories)
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

            // Get directory contents - files and directories
            if (await vfs.DirectoryExistsAsync(basePath, context.UserSession.User))
            {
                var entries = await vfs.ListDirectoryAsync(basePath);
                
                foreach (var entry in entries)
                {
                    if (entry.Name.StartsWith(searchPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        var completion = entry.IsDirectory ? $"{entry.Name}/" : entry.Name;
                        completions.Add(completion);
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

    private static IVirtualFileSystem GetVirtualFileSystem(CommandContext context)
    {
        // Access the VFS from the shell through the context
        if (context.FileSystem == null)
            throw new InvalidOperationException("File system not available in command context");

        return context.FileSystem;
    }
}
