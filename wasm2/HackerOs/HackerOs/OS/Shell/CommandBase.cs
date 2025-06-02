
using System.Text;
using HackerOs.OS.IO.FileSystem;

namespace HackerOs.OS.Shell;

/// <summary>
/// Abstract base class for shell commands providing common functionality
/// </summary>
public abstract class CommandBase : ICommand
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Usage { get; }
    public virtual IReadOnlyList<CommandOption> Options => new List<CommandOption>();

    public abstract Task<int> ExecuteAsync(
        CommandContext context,
        string[] args,
        Stream stdin,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken = default);

    public virtual Task<IEnumerable<string>> GetCompletionsAsync(
        CommandContext context,
        string[] args,
        string currentArg)
    {
        // Default implementation provides file/directory completion
        return GetFileCompletionsAsync(context, currentArg);
    }

    public virtual CommandValidationResult ValidateArguments(string[] args)
    {
        // Default validation - can be overridden by specific commands
        return CommandValidationResult.Success();
    }

    /// <summary>
    /// Helper method to write text to an output stream
    /// </summary>
    protected static async Task WriteLineAsync(Stream stream, string text, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(text + Environment.NewLine);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    /// <summary>
    /// Helper method to write text to an output stream without newline
    /// </summary>
    protected static async Task WriteAsync(Stream stream, string text, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    /// <summary>
    /// Helper method to read all text from an input stream
    /// </summary>
    protected static async Task<string> ReadAllTextAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Helper method to read lines from an input stream
    /// </summary>
    protected static async IAsyncEnumerable<string> ReadLinesAsync(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return line;
        }
    }

    /// <summary>
    /// Helper method for file/directory completion
    /// </summary>
    protected virtual async Task<IEnumerable<string>> GetFileCompletionsAsync(CommandContext context, string partialPath)
    {
        try
        {
            var vfs = GetFileSystem(context);
            var completions = new List<string>();

            // Resolve the base path for completion
            string basePath;
            string searchPattern;

            if (partialPath.Contains('/'))
            {
                var lastSlash = partialPath.LastIndexOf('/');
                basePath = partialPath[..lastSlash];
                searchPattern = partialPath[(lastSlash + 1)..];
            }            else
            {
                basePath = context.WorkingDirectory;
                searchPattern = partialPath;
            }

            // Convert relative path to absolute
            basePath = vfs.GetAbsolutePath(basePath, context.WorkingDirectory);

            // Get directory contents
            if (await vfs.DirectoryExistsAsync(basePath, context.CurrentUser))
            {
                var entries = await vfs.ListDirectoryAsync(basePath, context.CurrentUser);
                
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
            // Return empty completions on error
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Helper method to get the virtual file system from context
    /// </summary>
    protected static IVirtualFileSystem GetFileSystem(CommandContext context)
    {
        // In a real implementation, this would be injected or obtained from context
        // For now, we'll assume it's available through the shell
        throw new NotImplementedException("File system access needs to be implemented through dependency injection");
    }

    /// <summary>
    /// Parse command line arguments into options and parameters
    /// </summary>
    protected static ParsedArguments ParseArguments(string[] args, IReadOnlyList<CommandOption> availableOptions)
    {
        var options = new Dictionary<string, string?>();
        var parameters = new List<string>();
        var warnings = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith("--"))
            {
                // Long option
                var optionName = arg[2..];
                var equalIndex = optionName.IndexOf('=');
                
                if (equalIndex > 0)
                {
                    var name = optionName[..equalIndex];
                    var value = optionName[(equalIndex + 1)..];
                    options[name] = value;
                }
                else
                {
                    var option = availableOptions.FirstOrDefault(o => o.LongName == optionName);
                    if (option?.RequiresValue == true && i + 1 < args.Length)
                    {
                        options[optionName] = args[++i];
                    }
                    else
                    {
                        options[optionName] = option?.DefaultValue;
                    }
                }
            }
            else if (arg.StartsWith("-") && arg.Length > 1)
            {
                // Short option(s)
                for (int j = 1; j < arg.Length; j++)
                {
                    var shortName = arg[j].ToString();
                    var option = availableOptions.FirstOrDefault(o => o.ShortName == shortName);
                    
                    if (option?.RequiresValue == true)
                    {
                        if (j == arg.Length - 1 && i + 1 < args.Length)
                        {
                            options[shortName] = args[++i];
                        }
                        else if (j < arg.Length - 1)
                        {
                            options[shortName] = arg[(j + 1)..];
                            break;
                        }
                        else
                        {
                            warnings.Add($"Option -{shortName} requires a value");
                            options[shortName] = option.DefaultValue;
                        }
                    }
                    else
                    {
                        options[shortName] = option?.DefaultValue;
                    }
                }
            }
            else
            {
                // Regular parameter
                parameters.Add(arg);
            }
        }

        return new ParsedArguments(options, parameters, warnings);
    }
}

/// <summary>
/// Parsed command line arguments
/// </summary>
public class ParsedArguments
{
    public IReadOnlyDictionary<string, string?> Options { get; }
    public IReadOnlyList<string> Parameters { get; }
    public IReadOnlyList<string> Warnings { get; }

    public ParsedArguments(
        IDictionary<string, string?> options,
        IList<string> parameters,
        IList<string> warnings)
    {
        Options = new Dictionary<string, string?>(options);
        Parameters = parameters.ToList();
        Warnings = warnings.ToList();
    }

    public bool HasOption(string name) => Options.ContainsKey(name);
    
    public string? GetOption(string name) => Options.TryGetValue(name, out var value) ? value : null;
    
    public bool GetBoolOption(string name) => HasOption(name);
}
