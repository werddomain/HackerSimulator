
using System.Text;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.Shell;

namespace HackerOs.OS.Shell.Commands;

/// <summary>
/// List directory contents command (ls)
/// </summary>
public class LsCommand : CommandBase
{
    public override string Name => "ls";
    public override string Description => "List directory contents";
    public override string Usage => "ls [OPTIONS] [DIRECTORY...]";

    public override IReadOnlyList<CommandOption> Options => new List<CommandOption>
    {
        new("l", "long", "Use long listing format"),
        new("a", "all", "Show hidden files (starting with .)"),
        new("h", "human-readable", "Print sizes in human readable format"),
        new("t", "time", "Sort by modification time"),
        new("r", "reverse", "Reverse sort order"),
        new("R", "recursive", "List subdirectories recursively"),
        new("d", "directory", "List directories themselves, not their contents"),
        new("1", "one", "List one file per line")
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
            var directories = parsedArgs.Parameters.Any() ? parsedArgs.Parameters.ToArray() : new[] { "." };

            var vfs = GetVirtualFileSystem(context);
            var output = new StringBuilder();

            foreach (var directory in directories)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var absolutePath = vfs.GetAbsolutePath(directory, context.CurrentWorkingDirectory);
                
                if (!await vfs.DirectoryExistsAsync(absolutePath, context.UserSession.User))
                {
                    await WriteLineAsync(stderr, $"ls: cannot access '{directory}': No such file or directory");
                    continue;
                }

                var node = await vfs.GetNodeAsync(absolutePath, context.UserSession.User);
                if (node == null || !node.CanRead(context.UserSession.User))
                {
                    await WriteLineAsync(stderr, $"ls: cannot open directory '{directory}': Permission denied");
                    continue;
                }

                if (directories.Length > 1)
                {
                    output.AppendLine($"{directory}:");
                }                var entries = await vfs.ListDirectoryAsync(absolutePath);

                // Apply sorting
                entries = ApplySorting(entries, parsedArgs);                // Generate output
                if (parsedArgs.HasOption("l") || parsedArgs.HasOption("long"))
                {
                    WriteLongFormat(entries, output, parsedArgs);
                }
                else
                {
                    WriteShortFormat(entries, output, parsedArgs);
                }

                if (directories.Length > 1)
                {
                    output.AppendLine();
                }
            }

            await WriteAsync(stdout, output.ToString());
            return 0;
        }
        catch (Exception ex)
        {
            await WriteLineAsync(stderr, $"ls: {ex.Message}");
            return 1;
        }
    }

    private static IEnumerable<VirtualFileSystemNode> ApplySorting(
        IEnumerable<VirtualFileSystemNode> entries,
        ParsedArguments args)
    {
        var sorted = entries.AsEnumerable();        if (args.HasOption("t") || args.HasOption("time"))
        {
            sorted = sorted.OrderBy(e => e.ModifiedAt);
        }
        else
        {
            sorted = sorted.OrderBy(e => e.Name);
        }

        if (args.HasOption("r") || args.HasOption("reverse"))
        {
            sorted = sorted.Reverse();
        }

        return sorted;
    }    private static void WriteLongFormat(
        IEnumerable<VirtualFileSystemNode> entries,
        StringBuilder output,
        ParsedArguments args)
    {
        foreach (var entry in entries)
        {
            // File type and permissions
            var permissions = GetPermissionString(entry);
            
            // Link count (always 1 for simplicity)
            var linkCount = 1;
              // Owner and group
            var owner = entry.Owner ?? "root";
            var group = entry.Group ?? "root";
            
            // Size
            var size = entry.Size;
            var sizeString = args.HasOption("h") || args.HasOption("human-readable") 
                ? FormatHumanReadableSize(size)
                : size.ToString();
            
            // Date
            var dateString = entry.ModifiedAt.ToString("MMM dd HH:mm");
            
            // Name
            var name = entry.Name;
            if (entry.IsDirectory)
            {
                name += "/";
            }
            
            output.AppendLine($"{permissions} {linkCount,3} {owner,-8} {group,-8} {sizeString,8} {dateString} {name}");
        }
    }    private static void WriteShortFormat(
        IEnumerable<VirtualFileSystemNode> entries,
        StringBuilder output,
        ParsedArguments args)
    {
        if (args.HasOption("1") || args.HasOption("one"))
        {
            foreach (var entry in entries)
            {
                var name = entry.Name;
                if (entry.IsDirectory)
                {
                    name += "/";
                }
                output.AppendLine(name);
            }
        }
        else
        {
            // Multi-column format
            var names = entries.Select(e => e.IsDirectory ? $"{e.Name}/" : e.Name).ToList();
            var terminalWidth = 80; // Assume 80 character terminal
            var columnWidth = names.Any() ? names.Max(n => n.Length) + 2 : 1;
            var columnsPerRow = Math.Max(1, terminalWidth / columnWidth);
            
            for (int i = 0; i < names.Count; i += columnsPerRow)
            {
                var rowNames = names.Skip(i).Take(columnsPerRow);
                output.AppendLine(string.Join("", rowNames.Select(name => name.PadRight(columnWidth))));
            }
        }
    }

    private static string GetPermissionString(VirtualFileSystemNode node)
    {
        var sb = new StringBuilder();
        
        // File type
        sb.Append(node.IsDirectory ? 'd' : '-');
        
        // Owner permissions
        sb.Append(node.Permissions.OwnerRead ? 'r' : '-');
        sb.Append(node.Permissions.OwnerWrite ? 'w' : '-');
        sb.Append(node.Permissions.OwnerExecute ? 'x' : '-');
        
        // Group permissions
        sb.Append(node.Permissions.GroupRead ? 'r' : '-');
        sb.Append(node.Permissions.GroupWrite ? 'w' : '-');
        sb.Append(node.Permissions.GroupExecute ? 'x' : '-');
        
        // Other permissions
        sb.Append(node.Permissions.OtherRead ? 'r' : '-');
        sb.Append(node.Permissions.OtherWrite ? 'w' : '-');
        sb.Append(node.Permissions.OtherExecute ? 'x' : '-');
        
        return sb.ToString();
    }

    private static string FormatHumanReadableSize(long size)
    {
        if (size < 1024)
            return $"{size}B";
        
        double doubleSize = size;
        string[] suffixes = { "B", "K", "M", "G", "T" };
        int suffixIndex = 0;
        
        while (doubleSize >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            doubleSize /= 1024;
            suffixIndex++;
        }
        
        return $"{doubleSize:F1}{suffixes[suffixIndex]}";
    }

    protected override async Task<IEnumerable<string>> GetFileCompletionsAsync(CommandContext context, string partialPath)
    {
        // For ls command, complete with directories
        var completions = await base.GetFileCompletionsAsync(context, partialPath);
        return completions;
    }

    private static IVirtualFileSystem GetVirtualFileSystem(CommandContext context)
    {
        // This would be injected through DI in a real implementation
        // For now, we'll access it through the shell or context
        throw new NotImplementedException("VFS access needs proper DI integration");
    }
}
