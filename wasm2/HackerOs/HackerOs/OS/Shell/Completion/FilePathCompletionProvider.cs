using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Shell.Completion;

/// <summary>
/// Provides completion for file and directory paths
/// </summary>
public class FilePathCompletionProvider : ICompletionProvider
{
    private readonly IVirtualFileSystem _fileSystem;
    private readonly ILogger<FilePathCompletionProvider> _logger;

    public int Priority => 80; // Medium-high priority for file paths

    public FilePathCompletionProvider(IVirtualFileSystem fileSystem, ILogger<FilePathCompletionProvider> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanProvideCompletions(CompletionContext context)
    {
        // Provide file path completions for arguments (not the command itself)
        return context.IsArgumentPosition && context.CurrentUser != null;
    }

    public async Task<IEnumerable<CompletionItem>> GetCompletionsAsync(CompletionContext context)
    {
        if (!CanProvideCompletions(context) || context.CurrentUser == null)
        {
            return Enumerable.Empty<CompletionItem>();
        }

        try
        {
            var currentToken = context.CurrentToken;
            string searchPath;
            string prefix;

            // Determine the directory to search and the prefix to match
            if (string.IsNullOrEmpty(currentToken))
            {
                searchPath = context.CurrentWorkingDirectory;
                prefix = "";
            }
            else if (currentToken.EndsWith("/") || currentToken.EndsWith("\\"))
            {
                searchPath = _fileSystem.GetAbsolutePath(currentToken, context.CurrentWorkingDirectory);
                prefix = "";
            }
            else
            {
                var lastSeparator = Math.Max(currentToken.LastIndexOf('/'), currentToken.LastIndexOf('\\'));
                if (lastSeparator >= 0)
                {
                    var dirPart = currentToken.Substring(0, lastSeparator + 1);
                    searchPath = _fileSystem.GetAbsolutePath(dirPart, context.CurrentWorkingDirectory);
                    prefix = currentToken.Substring(lastSeparator + 1);
                }
                else
                {
                    searchPath = context.CurrentWorkingDirectory;
                    prefix = currentToken;
                }
            }

            // Get directory contents
            if (!await _fileSystem.DirectoryExistsAsync(searchPath, context.CurrentUser))
            {
                return Enumerable.Empty<CompletionItem>();
            }

            var items = await _fileSystem.GetDirectoryContentsAsync(searchPath);
            var completions = new List<CompletionItem>();

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(prefix) || item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var completionText = item.IsDirectory ? item.Name + "/" : item.Name;
                    var completionType = item.IsDirectory ? CompletionType.Directory : CompletionType.File;
                    var priority = item.IsDirectory ? 90 : 85; // Directories slightly higher priority

                    completions.Add(new CompletionItem(
                        text: completionText,
                        type: completionType,
                        description: GetItemDescription(item),
                        priority: priority
                    ));
                }
            }

            return completions;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting file path completions for token: {Token}", context.CurrentToken);
            return Enumerable.Empty<CompletionItem>();
        }
    }

    private string GetItemDescription(VirtualFileSystemNode item)
    {
        if (item.IsDirectory)
        {
            return "directory";
        }
        else
        {
            var size = item.Size;
            if (size < 1024)
                return $"{size} bytes";
            else if (size < 1024 * 1024)
                return $"{size / 1024:F1} KB";
            else
                return $"{size / (1024 * 1024):F1} MB";
        }
    }
}
