using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Shell.History;

/// <summary>
/// Provides advanced search capabilities for command history
/// </summary>
public class HistorySearchProvider
{
    private readonly ILogger<HistorySearchProvider> _logger;

    public HistorySearchProvider(ILogger<HistorySearchProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Searches through history entries based on the provided options
    /// </summary>
    /// <param name="entries">The history entries to search through</param>
    /// <param name="options">Search options</param>
    /// <returns>Filtered and sorted history entries</returns>
    public IEnumerable<HistoryEntry> Search(IEnumerable<HistoryEntry> entries, HistorySearchOptions options)
    {
        if (entries == null) throw new ArgumentNullException(nameof(entries));
        if (options == null) throw new ArgumentNullException(nameof(options));

        try
        {
            var query = entries.AsEnumerable();

            // Apply text search filter
            if (!string.IsNullOrEmpty(options.SearchTerm))
            {
                query = ApplyTextFilter(query, options.SearchTerm, options.UseRegex, options.CaseSensitive, options.RegexOptions);
            }

            // Apply date range filter
            if (options.StartDate.HasValue)
            {
                query = query.Where(e => e.Timestamp >= options.StartDate.Value);
            }

            if (options.EndDate.HasValue)
            {
                query = query.Where(e => e.Timestamp <= options.EndDate.Value);
            }

            // Apply exit code filter
            if (options.ExitCode.HasValue)
            {
                query = query.Where(e => e.ExitCode == options.ExitCode.Value);
            }

            // Apply success/failure filters
            if (options.OnlySuccessful)
            {
                query = query.Where(e => e.IsSuccess);
            }
            else if (options.OnlyFailed)
            {
                query = query.Where(e => !e.IsSuccess);
            }

            // Apply working directory filter
            if (!string.IsNullOrEmpty(options.WorkingDirectory))
            {
                var comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                query = query.Where(e => e.WorkingDirectory.Contains(options.WorkingDirectory, comparison));
            }

            // Apply ordering
            if (options.ReverseOrder)
            {
                query = query.OrderByDescending(e => e.Timestamp);
            }
            else
            {
                query = query.OrderBy(e => e.Timestamp);
            }

            // Apply result limit
            if (options.MaxResults > 0)
            {
                query = query.Take(options.MaxResults);
            }

            return query.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during history search with term: {SearchTerm}", options.SearchTerm);
            return Enumerable.Empty<HistoryEntry>();
        }
    }

    /// <summary>
    /// Performs a reverse search starting from a specific position
    /// </summary>
    /// <param name="entries">The history entries to search through</param>
    /// <param name="searchTerm">The term to search for</param>
    /// <param name="startPosition">Position to start searching from</param>
    /// <param name="caseSensitive">Whether search should be case sensitive</param>
    /// <returns>The matching entry and its index, or null if not found</returns>
    public (HistoryEntry? Entry, int Index)? ReverseSearch(
        IReadOnlyList<HistoryEntry> entries, 
        string searchTerm, 
        int startPosition = -1, 
        bool caseSensitive = false)
    {
        if (entries == null) throw new ArgumentNullException(nameof(entries));
        if (string.IsNullOrEmpty(searchTerm)) return null;

        try
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            
            // Start from the specified position or the end of the list
            int searchStart = startPosition >= 0 ? Math.Min(startPosition, entries.Count - 1) : entries.Count - 1;

            // Search backwards through the history
            for (int i = searchStart; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry.Command.Contains(searchTerm, comparison))
                {
                    return (entry, i);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during reverse search with term: {SearchTerm}", searchTerm);
            return null;
        }
    }

    /// <summary>
    /// Finds the most frequently used commands
    /// </summary>
    /// <param name="entries">The history entries to analyze</param>
    /// <param name="count">Number of top commands to return</param>
    /// <returns>Commands ordered by frequency of use</returns>
    public IEnumerable<(string Command, int Frequency)> GetMostFrequentCommands(
        IEnumerable<HistoryEntry> entries, 
        int count = 10)
    {
        if (entries == null) throw new ArgumentNullException(nameof(entries));

        try
        {
            return entries
                .GroupBy(e => e.Command.Split(' ')[0]) // Group by base command (first word)
                .Select(g => (Command: g.Key, Frequency: g.Count()))
                .OrderByDescending(x => x.Frequency)
                .Take(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while analyzing command frequency");
            return Enumerable.Empty<(string, int)>();
        }
    }

    /// <summary>
    /// Gets commands that failed execution
    /// </summary>
    /// <param name="entries">The history entries to analyze</param>
    /// <param name="count">Maximum number of failed commands to return</param>
    /// <returns>Failed commands with their exit codes</returns>
    public IEnumerable<HistoryEntry> GetFailedCommands(IEnumerable<HistoryEntry> entries, int count = 20)
    {
        if (entries == null) throw new ArgumentNullException(nameof(entries));

        try
        {
            return entries
                .Where(e => !e.IsSuccess)
                .OrderByDescending(e => e.Timestamp)
                .Take(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while retrieving failed commands");
            return Enumerable.Empty<HistoryEntry>();
        }
    }

    /// <summary>
    /// Applies text-based filtering to the query
    /// </summary>
    private IEnumerable<HistoryEntry> ApplyTextFilter(
        IEnumerable<HistoryEntry> query,
        string searchTerm,
        bool useRegex,
        bool caseSensitive,
        RegexOptions regexOptions)
    {
        if (useRegex)
        {
            try
            {
                var options = regexOptions;
                if (!caseSensitive)
                    options |= RegexOptions.IgnoreCase;

                var regex = new Regex(searchTerm, options);
                return query.Where(e => regex.IsMatch(e.Command));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid regex pattern: {Pattern}. Falling back to text search", searchTerm);
                // Fall back to simple text search
                useRegex = false;
            }
        }

        if (!useRegex)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return query.Where(e => e.Command.Contains(searchTerm, comparison));
        }

        return query;
    }
}
