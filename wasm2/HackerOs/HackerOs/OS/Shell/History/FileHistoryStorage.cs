using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Shell.History;

/// <summary>
/// File-based implementation of history storage using ~/.bash_history format
/// </summary>
public class FileHistoryStorage : IHistoryStorage
{
    private readonly IVirtualFileSystem _fileSystem;
    private readonly ILogger<FileHistoryStorage> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileHistoryStorage(IVirtualFileSystem fileSystem, ILogger<FileHistoryStorage> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Gets the history file path for a user
    /// </summary>
    /// <param name="user">The user</param>
    /// <returns>The path to the user's history file</returns>
    private string GetHistoryFilePath(User.User user)
    {
        return Path.Combine(user.HomeDirectory, ".bash_history");
    }

    /// <summary>
    /// Gets the metadata file path for a user's history
    /// </summary>
    /// <param name="user">The user</param>
    /// <returns>The path to the user's history metadata file</returns>
    private string GetMetadataFilePath(User.User user)
    {
        return Path.Combine(user.HomeDirectory, ".bash_history_meta");
    }

    public async Task<List<HistoryEntry>> LoadAsync(User.User user)
    {
        try
        {
            var historyPath = GetHistoryFilePath(user);
            var metadataPath = GetMetadataFilePath(user);
            
            var entries = new List<HistoryEntry>();            // Load basic command history
            if (await _fileSystem.FileExistsAsync(historyPath, user))
            {
                var content = await _fileSystem.ReadAllTextAsync(historyPath, user);
                if (content != null)
                {
                    var commands = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (var command in commands)
                    {
                        if (!string.IsNullOrWhiteSpace(command))
                        {
                            entries.Add(new HistoryEntry(command.Trim()));
                        }
                    }
                }
            }

            // Load metadata if available
            Dictionary<string, HistoryEntryMetadata>? metadata = null;
            if (await _fileSystem.FileExistsAsync(metadataPath, user))
            {
                try
                {
                    var metadataContent = await _fileSystem.ReadAllTextAsync(metadataPath, user);
                    if (metadataContent != null)
                    {
                        metadata = JsonSerializer.Deserialize<Dictionary<string, HistoryEntryMetadata>>(metadataContent, _jsonOptions);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load history metadata for user {Username}", user.Username);
                }
            }

            // Merge metadata with entries
            if (metadata != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var key = $"{i}:{entry.Command}";
                    
                    if (metadata.TryGetValue(key, out var meta))
                    {
                        entries[i] = entry with
                        {
                            Timestamp = meta.Timestamp,
                            ExitCode = meta.ExitCode,
                            WorkingDirectory = meta.WorkingDirectory ?? string.Empty,
                            Duration = meta.Duration
                        };
                    }
                }
            }

            _logger.LogDebug("Loaded {Count} history entries for user {Username}", entries.Count, user.Username);
            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history for user {Username}", user.Username);
            return new List<HistoryEntry>();
        }
    }

    public async Task SaveAsync(User.User user, IEnumerable<HistoryEntry> entries)
    {
        try
        {
            var historyPath = GetHistoryFilePath(user);
            var metadataPath = GetMetadataFilePath(user);
            
            var entryList = entries.ToList();            // Save basic command history (for compatibility with bash)
            var commands = entryList.Select(e => e.Command).ToList();
            var historyContent = string.Join('\n', commands);
            
            await _fileSystem.WriteAllTextAsync(historyPath, historyContent, user);

            // Save metadata
            var metadata = new Dictionary<string, HistoryEntryMetadata>();
            for (int i = 0; i < entryList.Count; i++)
            {
                var entry = entryList[i];
                var key = $"{i}:{entry.Command}";
                
                metadata[key] = new HistoryEntryMetadata
                {
                    Timestamp = entry.Timestamp,
                    ExitCode = entry.ExitCode,
                    WorkingDirectory = entry.WorkingDirectory,
                    Duration = entry.Duration
                };
            }

            var metadataContent = JsonSerializer.Serialize(metadata, _jsonOptions);
            await _fileSystem.WriteAllTextAsync(metadataPath, metadataContent, user);

            _logger.LogDebug("Saved {Count} history entries for user {Username}", entryList.Count, user.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save history for user {Username}", user.Username);
            throw;
        }
    }

    public async Task ClearAsync(User.User user)
    {
        try
        {
            var historyPath = GetHistoryFilePath(user);
            var metadataPath = GetMetadataFilePath(user);            if (await _fileSystem.FileExistsAsync(historyPath, user))
            {
                await _fileSystem.DeleteFileAsync(historyPath, user);
            }

            if (await _fileSystem.FileExistsAsync(metadataPath, user))
            {
                await _fileSystem.DeleteFileAsync(metadataPath, user);
            }

            _logger.LogDebug("Cleared history for user {Username}", user.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear history for user {Username}", user.Username);
            throw;
        }
    }

    /// <summary>
    /// Metadata structure for history entries
    /// </summary>
    private class HistoryEntryMetadata
    {
        public DateTime Timestamp { get; set; }
        public int ExitCode { get; set; }
        public string? WorkingDirectory { get; set; }
        public TimeSpan? Duration { get; set; }
    }
}
