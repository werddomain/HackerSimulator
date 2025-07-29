using System;

namespace HackerOs.OS.Shell.History;

/// <summary>
/// Represents a single command history entry with metadata
/// </summary>
public record HistoryEntry
{
    /// <summary>
    /// The command text
    /// </summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when the command was executed
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Exit code of the command execution
    /// </summary>
    public int ExitCode { get; init; } = 0;

    /// <summary>
    /// Working directory when the command was executed
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Duration of command execution, if available
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Whether the command was executed successfully
    /// </summary>
    public bool IsSuccess => ExitCode == 0;

    public HistoryEntry() { }

    public HistoryEntry(string command, int exitCode = 0, string workingDirectory = "", TimeSpan? duration = null)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        ExitCode = exitCode;
        WorkingDirectory = workingDirectory ?? string.Empty;
        Duration = duration;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns a string representation of the history entry
    /// </summary>
    public override string ToString()
    {
        return $"{Timestamp:yyyy-MM-dd HH:mm:ss} [{ExitCode}] {Command}";
    }
}
