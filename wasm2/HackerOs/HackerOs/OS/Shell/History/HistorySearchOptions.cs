using System;
using System.Text.RegularExpressions;

namespace HackerOs.OS.Shell.History;

/// <summary>
/// Options for searching command history
/// </summary>
public class HistorySearchOptions
{
    /// <summary>
    /// Text to search for in commands
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Whether the search should be case sensitive
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// Whether to use regular expression matching
    /// </summary>
    public bool UseRegex { get; set; } = false;

    /// <summary>
    /// Start date for filtering entries (inclusive)
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for filtering entries (inclusive)
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Filter by exit code (null means no filter)
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// Filter by working directory
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Maximum number of results to return (0 means no limit)
    /// </summary>
    public int MaxResults { get; set; } = 0;

    /// <summary>
    /// Whether to include only successful commands (exit code 0)
    /// </summary>
    public bool OnlySuccessful { get; set; } = false;

    /// <summary>
    /// Whether to include only failed commands (exit code != 0)
    /// </summary>
    public bool OnlyFailed { get; set; } = false;

    /// <summary>
    /// Whether to search in reverse order (newest first)
    /// </summary>
    public bool ReverseOrder { get; set; } = false;

    /// <summary>
    /// Regular expression options if UseRegex is true
    /// </summary>
    public RegexOptions RegexOptions { get; set; } = RegexOptions.None;

    /// <summary>
    /// Creates a new instance with default options
    /// </summary>
    public HistorySearchOptions() { }

    /// <summary>
    /// Creates a new instance with a search term
    /// </summary>
    /// <param name="searchTerm">The term to search for</param>
    /// <param name="caseSensitive">Whether the search should be case sensitive</param>
    public HistorySearchOptions(string searchTerm, bool caseSensitive = false)
    {
        SearchTerm = searchTerm;
        CaseSensitive = caseSensitive;
    }

    /// <summary>
    /// Creates options for reverse search (Ctrl+R style)
    /// </summary>
    /// <param name="searchTerm">The term to search for</param>
    /// <param name="caseSensitive">Whether the search should be case sensitive</param>
    /// <returns>Search options configured for reverse search</returns>
    public static HistorySearchOptions ForReverseSearch(string searchTerm, bool caseSensitive = false)
    {
        return new HistorySearchOptions
        {
            SearchTerm = searchTerm,
            CaseSensitive = caseSensitive,
            ReverseOrder = true,
            MaxResults = 1
        };
    }

    /// <summary>
    /// Creates options for finding successful commands
    /// </summary>
    /// <param name="searchTerm">Optional search term</param>
    /// <returns>Search options configured for successful commands</returns>
    public static HistorySearchOptions ForSuccessfulCommands(string? searchTerm = null)
    {
        return new HistorySearchOptions
        {
            SearchTerm = searchTerm,
            OnlySuccessful = true
        };
    }

    /// <summary>
    /// Creates options for finding failed commands
    /// </summary>
    /// <param name="searchTerm">Optional search term</param>
    /// <returns>Search options configured for failed commands</returns>
    public static HistorySearchOptions ForFailedCommands(string? searchTerm = null)
    {
        return new HistorySearchOptions
        {
            SearchTerm = searchTerm,
            OnlyFailed = true
        };
    }
}
