using HackerOs.OS.User;

namespace HackerOs.OS.Shell;

/// <summary>
/// Interface for the shell system that provides command execution and session management
/// </summary>
public interface IShell
{
    /// <summary>
    /// Current user session for this shell instance
    /// </summary>
    UserSession? CurrentSession { get; }

    /// <summary>
    /// Current working directory for this shell session
    /// </summary>
    string CurrentWorkingDirectory { get; set; }

    /// <summary>
    /// Environment variables for this shell session
    /// </summary>
    IDictionary<string, string> EnvironmentVariables { get; }

    /// <summary>
    /// Command history for this shell session
    /// </summary>
    IReadOnlyList<string> CommandHistory { get; }

    /// <summary>
    /// Event raised when shell output is produced
    /// </summary>
    event EventHandler<ShellOutputEventArgs>? OutputReceived;

    /// <summary>
    /// Event raised when shell encounters an error
    /// </summary>
    event EventHandler<ShellErrorEventArgs>? ErrorReceived;

    /// <summary>
    /// Event raised when working directory changes
    /// </summary>
    event EventHandler<DirectoryChangedEventArgs>? DirectoryChanged;

    /// <summary>
    /// Initialize the shell with a user session
    /// </summary>
    /// <param name="session">User session to associate with this shell</param>
    /// <returns>True if initialization successful</returns>
    Task<bool> InitializeAsync(UserSession session);

    /// <summary>
    /// Execute a command line with full parsing and pipeline support
    /// </summary>
    /// <param name="commandLine">Command line to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exit code of the command execution</returns>
    Task<int> ExecuteCommandAsync(string commandLine, CancellationToken cancellationToken = default);    /// <summary>
    /// Get command suggestions for tab completion
    /// </summary>
    /// <param name="partialCommand">Partial command to complete</param>
    /// <returns>List of possible completions</returns>
    Task<IEnumerable<string>> GetCompletionsAsync(string partialCommand);

    /// <summary>
    /// Get command suggestions for tab completion with cursor position
    /// </summary>
    /// <param name="commandLine">Full command line</param>
    /// <param name="cursorPosition">Current cursor position in the command line</param>
    /// <returns>List of possible completions</returns>
    Task<IEnumerable<string>> GetCompletionsAsync(string commandLine, int cursorPosition);

    /// <summary>
    /// Set an environment variable for this shell session
    /// </summary>
    /// <param name="name">Variable name</param>
    /// <param name="value">Variable value</param>
    void SetEnvironmentVariable(string name, string value);

    /// <summary>
    /// Get an environment variable value
    /// </summary>
    /// <param name="name">Variable name</param>
    /// <returns>Variable value or null if not found</returns>
    string? GetEnvironmentVariable(string name);

    /// <summary>
    /// Change the current working directory
    /// </summary>
    /// <param name="path">New working directory path</param>
    /// <returns>True if directory change successful</returns>
    Task<bool> ChangeDirectoryAsync(string path);

    /// <summary>
    /// Load command history from persistent storage
    /// </summary>
    Task LoadHistoryAsync();    /// <summary>
    /// Save command history to persistent storage
    /// </summary>
    Task SaveHistoryAsync();

    /// <summary>
    /// Navigate up in command history (to older commands)
    /// </summary>
    /// <returns>The previous command, or null if at the beginning</returns>
    string? NavigateHistoryUp();

    /// <summary>
    /// Navigate down in command history (to newer commands)
    /// </summary>
    /// <returns>The next command, or null if at the end</returns>
    string? NavigateHistoryDown();

    /// <summary>
    /// Reset history navigation position
    /// </summary>
    void ResetHistoryNavigation();

    /// <summary>
    /// Search command history for commands containing the specified text
    /// </summary>
    /// <param name="searchText">Text to search for</param>
    /// <returns>List of matching history entries</returns>
    IEnumerable<string> SearchHistory(string searchText);

    /// <summary>
    /// Clear the shell session and reset state
    /// </summary>
    void ClearSession();

    /// <summary>
    /// Get the current working directory
    /// </summary>
    /// <returns>The current working directory path</returns>
    string GetWorkingDirectory();
}

/// <summary>
/// Event arguments for shell output
/// </summary>
public class ShellOutputEventArgs : EventArgs
{
    public string Output { get; }
    public bool IsError { get; }
    public DateTime Timestamp { get; }

    public ShellOutputEventArgs(string output, bool isError = false)
    {
        Output = output;
        IsError = isError;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for shell errors
/// </summary>
public class ShellErrorEventArgs : EventArgs
{
    public string Error { get; }
    public Exception? Exception { get; }
    public DateTime Timestamp { get; }

    public ShellErrorEventArgs(string error, Exception? exception = null)
    {
        Error = error;
        Exception = exception;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for directory changes
/// </summary>
public class DirectoryChangedEventArgs : EventArgs
{
    public string PreviousDirectory { get; }
    public string NewDirectory { get; }
    public DateTime Timestamp { get; }

    public DirectoryChangedEventArgs(string previousDirectory, string newDirectory)
    {
        PreviousDirectory = previousDirectory;
        NewDirectory = newDirectory;
        Timestamp = DateTime.UtcNow;
    }
}
