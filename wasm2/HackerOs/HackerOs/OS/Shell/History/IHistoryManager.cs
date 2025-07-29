using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Shell.History;

/// <summary>
/// Interface for managing shell command history with navigation and search capabilities
/// </summary>
public interface IHistoryManager : IDisposable
{
    /// <summary>
    /// Maximum number of entries to keep in history
    /// </summary>
    int MaxHistorySize { get; set; }

    /// <summary>
    /// Current number of entries in history
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Current position in history navigation (-1 means no navigation)
    /// </summary>
    int CurrentPosition { get; }

    /// <summary>
    /// Adds a command to the history
    /// </summary>
    /// <param name="command">The command to add</param>
    /// <param name="exitCode">The exit code of the command</param>
    void AddCommand(string command, int exitCode = 0);

    /// <summary>
    /// Gets all history entries
    /// </summary>
    /// <returns>Read-only list of history entries</returns>
    IReadOnlyList<HistoryEntry> GetEntries();

    /// <summary>
    /// Navigates up in history (to older commands)
    /// </summary>
    /// <returns>The previous command, or null if at the beginning</returns>
    string? NavigateUp();

    /// <summary>
    /// Navigates down in history (to newer commands)
    /// </summary>
    /// <returns>The next command, or null if at the end</returns>
    string? NavigateDown();

    /// <summary>
    /// Resets navigation position to the end of history
    /// </summary>
    void ResetPosition();

    /// <summary>
    /// Searches history entries
    /// </summary>
    /// <param name="options">Search options</param>
    /// <returns>Matching history entries</returns>
    IEnumerable<HistoryEntry> Search(HistorySearchOptions options);

    /// <summary>
    /// Performs reverse search (Ctrl+R style) starting from current position
    /// </summary>
    /// <param name="searchTerm">Term to search for</param>
    /// <param name="caseSensitive">Whether search should be case sensitive</param>
    /// <returns>The matching entry, or null if not found</returns>
    HistoryEntry? ReverseSearch(string searchTerm, bool caseSensitive = false);

    /// <summary>
    /// Clears all history entries
    /// </summary>
    void Clear();

    /// <summary>
    /// Loads history from persistent storage
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    Task LoadAsync();

    /// <summary>
    /// Saves history to persistent storage
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    Task SaveAsync();

    /// <summary>
    /// Sets the current user context for history management
    /// </summary>
    /// <param name="user">The current user</param>
    void SetUser(User.User user);
}
