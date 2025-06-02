using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Shell.History;

/// <summary>
/// Manages shell command history with navigation, search, and persistent storage
/// </summary>
public class HistoryManager : IHistoryManager
{
    private readonly IHistoryStorage _historyStorage;
    private readonly ILogger<HistoryManager> _logger;
    private readonly List<HistoryEntry> _entries = new();
    private readonly object _lock = new();
    
    private int _currentPosition = -1;
    private int _maxHistorySize = 1000;
    private User.User? _currentUser;

    public HistoryManager(
        IHistoryStorage historyStorage,
        ILogger<HistoryManager> logger)
    {
        _historyStorage = historyStorage ?? throw new ArgumentNullException(nameof(historyStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public int MaxHistorySize
    {
        get => _maxHistorySize;
        set => _maxHistorySize = Math.Max(1, value);
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    public int CurrentPosition
    {
        get => _currentPosition;
        set
        {
            lock (_lock)
            {
                _currentPosition = Math.Max(-1, Math.Min(value, _entries.Count - 1));
            }
        }
    }

    public IReadOnlyList<HistoryEntry> GetEntries()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    public void SetUser(User.User user)
    {
        _currentUser = user ?? throw new ArgumentNullException(nameof(user));
    }

    public async Task LoadAsync()
    {
        if (_currentUser == null)
            throw new InvalidOperationException("User must be set before loading history");
        
        try
        {
            var entries = await _historyStorage.LoadAsync(_currentUser);
            
            lock (_lock)
            {
                _entries.Clear();
                _entries.AddRange(entries);
                _currentPosition = -1;
            }
            
            _logger.LogInformation("Loaded {Count} history entries for user {Username}", entries.Count, _currentUser.Username);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load history for user {Username}", _currentUser?.Username);
        }
    }

    public void AddCommand(string command, int exitCode = 0)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        string workingDirectory = _currentUser?.HomeDirectory ?? "/";
        var entry = new HistoryEntry(
            command: command.Trim(),
            exitCode: exitCode,
            workingDirectory: workingDirectory
        );

        lock (_lock)
        {
            // Don't add duplicate consecutive commands
            if (_entries.Count > 0 && _entries[^1].Command == entry.Command)
            {
                // Update the last entry with new timestamp and exit code
                var lastEntry = _entries[^1];
                _entries[^1] = lastEntry with 
                { 
                    Timestamp = entry.Timestamp,
                    ExitCode = entry.ExitCode
                };
                return;
            }

            _entries.Add(entry);

            // Maintain size limit
            if (_entries.Count > _maxHistorySize)
            {
                _entries.RemoveAt(0);
            }

            // Reset position to indicate we're at the end
            _currentPosition = -1;
        }

        _logger.LogTrace("Added command to history: {Command}", command);
    }

    public string? NavigateUp()
    {
        lock (_lock)
        {
            if (_entries.Count == 0)
            {
                return null;
            }

            if (_currentPosition == -1)
            {
                _currentPosition = _entries.Count - 1;
            }
            else if (_currentPosition > 0)
            {
                _currentPosition--;
            }

            return _currentPosition >= 0 && _currentPosition < _entries.Count
                ? _entries[_currentPosition].Command
                : null;
        }
    }

    public string? NavigateDown()
    {
        lock (_lock)
        {
            if (_entries.Count == 0 || _currentPosition == -1)
            {
                return null;
            }

            _currentPosition++;
            
            if (_currentPosition >= _entries.Count)
            {
                _currentPosition = -1;
                return string.Empty; // Return empty string to clear the line
            }

            return _entries[_currentPosition].Command;
        }
    }

    public void ResetPosition()
    {
        lock (_lock)
        {
            _currentPosition = -1;
        }
    }

    public IEnumerable<HistoryEntry> Search(HistorySearchOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        lock (_lock)
        {
            var query = _entries.AsEnumerable();

            // Apply text search filter
            if (!string.IsNullOrEmpty(options.SearchTerm))
            {
                if (options.UseRegex)
                {
                    try
                    {
                        var regexOptions = options.RegexOptions;
                        if (!options.CaseSensitive)
                            regexOptions |= System.Text.RegularExpressions.RegexOptions.IgnoreCase;

                        var regex = new System.Text.RegularExpressions.Regex(options.SearchTerm, regexOptions);
                        query = query.Where(e => regex.IsMatch(e.Command));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid regex pattern: {Pattern}. Falling back to text search", options.SearchTerm);
                        // Fall back to simple text search
                        var comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        query = query.Where(e => e.Command.Contains(options.SearchTerm, comparison));
                    }
                }
                else
                {
                    var comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    query = query.Where(e => e.Command.Contains(options.SearchTerm, comparison));
                }
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
    }

    public HistoryEntry? ReverseSearch(string searchTerm, bool caseSensitive = false)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        lock (_lock)
        {
            if (_entries.Count == 0)
            {
                return null;
            }

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            
            // Start from the specified position or the end of the list
            int searchStart = _currentPosition >= 0 ? Math.Min(_currentPosition, _entries.Count - 1) : _entries.Count - 1;

            // Search backwards through the history
            for (int i = searchStart; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.Command.Contains(searchTerm, comparison))
                {
                    _currentPosition = i;
                    return entry;
                }
            }

            return null;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _currentPosition = -1;
        }
        
        _logger.LogInformation("History cleared");
    }

    public async Task SaveAsync()
    {
        if (_currentUser == null)
        {
            _logger.LogWarning("Cannot save history: no current user set");
            return;
        }

        try
        {
            List<HistoryEntry> entriesToSave;
            
            lock (_lock)
            {
                entriesToSave = _entries.TakeLast(_maxHistorySize).ToList();
            }

            await _historyStorage.SaveAsync(_currentUser, entriesToSave);
            _logger.LogInformation("Saved {Count} history entries for user {Username}", 
                entriesToSave.Count, _currentUser.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save history for user {Username}", _currentUser?.Username);
        }
    }

    public void Dispose()
    {
        // Save history on disposal if we have a user
        if (_currentUser != null)
        {
            try
            {
                SaveAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save history during disposal");
            }
        }
    }
}
