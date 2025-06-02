using System.Text;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Shell.Commands;

/// <summary>
/// Command to execute shell scripts
/// </summary>
public class ShCommand : CommandBase
{
    private readonly IVirtualFileSystem _fileSystem;
    private readonly ILogger<ShCommand> _logger;

    public ShCommand(IVirtualFileSystem fileSystem, ILogger<ShCommand> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override string Name => "sh";
    public override string Description => "Execute shell scripts";
    public override string Usage => "sh <script> [args...]";

    public override async Task<IEnumerable<string>> GetCompletionsAsync(
        CommandContext context,
        string[] args,
        string currentArg)
    {
        if (args.Length == 0)
        {
            // Complete script files
            try
            {
                var files = await _fileSystem.ListDirectoryAsync(context.CurrentWorkingDirectory, context.User);
                return files.Where(f => f.Name.EndsWith(".sh") || !f.Name.Contains('.')).Select(f => f.Name).ToList();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        return Enumerable.Empty<string>();
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        string[] args,
        Stream stdin,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken = default)
    {
        if (args.Length == 0)
        {
            await WriteToStreamAsync(stderr, "Usage: sh <script> [args...]\n");
            return 1;
        }

        var scriptPath = args[0];
        var scriptArgs = args.Skip(1).ToArray();

        // Resolve script path
        if (!Path.IsPathRooted(scriptPath))
        {
            scriptPath = Path.Combine(context.CurrentWorkingDirectory, scriptPath);
        }

        try
        {            // Create shell script interpreter
            var interpreter = new ShellScriptInterpreter(
                context.Shell,
                _fileSystem,
                _logger);

            // Execute the script
            return await interpreter.ExecuteScriptAsync(
                scriptPath,
                scriptArgs,
                stdout,
                stderr,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing shell script {ScriptPath}", scriptPath);
            await WriteToStreamAsync(stderr, $"Error executing script: {ex.Message}\n");
            return 1;
        }
    }

    private static async Task WriteToStreamAsync(Stream stream, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }
}
