
using System.Text;

namespace HackerOs.OS.Shell;

/// <summary>
/// Interface for shell commands that support stream-based input/output
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Command name (e.g., "ls", "cat", "grep")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Command description for help text
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Command usage syntax
    /// </summary>
    string Usage { get; }

    /// <summary>
    /// Available command options/flags
    /// </summary>
    IReadOnlyList<CommandOption> Options { get; }

    /// <summary>
    /// Execute the command with stream-based input/output
    /// </summary>
    /// <param name="context">Command execution context</param>
    /// <param name="args">Command arguments</param>
    /// <param name="stdin">Standard input stream</param>
    /// <param name="stdout">Standard output stream</param>
    /// <param name="stderr">Standard error stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exit code (0 = success, non-zero = error)</returns>
    Task<int> ExecuteAsync(
        CommandContext context,
        string[] args,
        Stream stdin,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get completion suggestions for this command
    /// </summary>
    /// <param name="context">Command context</param>
    /// <param name="args">Current arguments</param>
    /// <param name="currentArg">Current argument being completed</param>
    /// <returns>Completion suggestions</returns>
    Task<IEnumerable<string>> GetCompletionsAsync(
        CommandContext context,
        string[] args,
        string currentArg);

    /// <summary>
    /// Validate command arguments before execution
    /// </summary>
    /// <param name="args">Command arguments</param>
    /// <returns>Validation result</returns>    CommandValidationResult ValidateArguments(string[] args);
}

/// <summary>
/// Command option definition
/// </summary>
public class CommandOption
{
    public string ShortName { get; }
    public string? LongName { get; }
    public string Description { get; }
    public bool RequiresValue { get; }
    public string? DefaultValue { get; }

    public CommandOption(
        string shortName,
        string? longName,
        string description,
        bool requiresValue = false,
        string? defaultValue = null)
    {
        ShortName = shortName;
        LongName = longName;
        Description = description;
        RequiresValue = requiresValue;
        DefaultValue = defaultValue;
    }
}

/// <summary>
/// Command argument validation result
/// </summary>
public class CommandValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }
    public IReadOnlyList<string> Warnings { get; }

    public CommandValidationResult(bool isValid, string? errorMessage = null, IList<string>? warnings = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
        Warnings = warnings?.ToList() ?? new List<string>();
    }

    public static CommandValidationResult Success(IList<string>? warnings = null) =>
        new(true, null, warnings);

    public static CommandValidationResult Error(string errorMessage) =>
        new(false, errorMessage);
}
