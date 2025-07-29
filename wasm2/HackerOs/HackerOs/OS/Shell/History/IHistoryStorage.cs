using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Shell.History;

/// <summary>
/// Interface for persistent storage of command history
/// </summary>
public interface IHistoryStorage
{
    /// <summary>
    /// Loads history entries from storage
    /// </summary>
    /// <param name="user">The user whose history to load</param>
    /// <returns>List of history entries</returns>
    Task<List<HistoryEntry>> LoadAsync(User.User user);

    /// <summary>
    /// Saves history entries to storage
    /// </summary>
    /// <param name="user">The user whose history to save</param>
    /// <param name="entries">The history entries to save</param>
    /// <returns>Task representing the async operation</returns>
    Task SaveAsync(User.User user, IEnumerable<HistoryEntry> entries);

    /// <summary>
    /// Clears all history for a user
    /// </summary>
    /// <param name="user">The user whose history to clear</param>
    /// <returns>Task representing the async operation</returns>
    Task ClearAsync(User.User user);
}
