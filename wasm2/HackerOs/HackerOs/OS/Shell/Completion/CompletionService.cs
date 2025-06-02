using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Shell.Completion;

/// <summary>
/// Main completion service that aggregates results from multiple providers
/// </summary>
public class CompletionService : ICompletionService
{
    private readonly List<ICompletionProvider> _providers = new();
    private readonly ILogger<CompletionService> _logger;
    private readonly object _lock = new();

    public CompletionService(ILogger<CompletionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RegisterProvider(ICompletionProvider provider)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        lock (_lock)
        {
            if (!_providers.Contains(provider))
            {
                _providers.Add(provider);
                // Sort providers by priority (higher priority first)
                _providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                _logger.LogDebug("Registered completion provider: {ProviderType}", provider.GetType().Name);
            }
        }
    }

    public void UnregisterProvider(ICompletionProvider provider)
    {
        if (provider == null)
            return;

        lock (_lock)
        {
            if (_providers.Remove(provider))
            {
                _logger.LogDebug("Unregistered completion provider: {ProviderType}", provider.GetType().Name);
            }
        }
    }

    public async Task<IEnumerable<CompletionItem>> GetCompletionsAsync(
        string commandLine,
        int cursorPosition,
        string workingDirectory,
        IDictionary<string, string> environmentVariables,
        User.User? currentUser = null)
    {
        if (string.IsNullOrEmpty(commandLine))
        {
            return Enumerable.Empty<CompletionItem>();
        }

        try
        {
            var context = CreateCompletionContext(commandLine, cursorPosition, workingDirectory, environmentVariables, currentUser);
            var allCompletions = new List<CompletionItem>();

            ICompletionProvider[] providers;
            lock (_lock)
            {
                providers = _providers.ToArray();
            }

            // Get completions from all applicable providers
            foreach (var provider in providers)
            {
                try
                {
                    if (provider.CanProvideCompletions(context))
                    {
                        var completions = await provider.GetCompletionsAsync(context);
                        allCompletions.AddRange(completions);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Completion provider {ProviderType} failed", provider.GetType().Name);
                }
            }

            // Filter, deduplicate, and sort results
            return ProcessCompletions(allCompletions, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completions for command line: {CommandLine}", commandLine);
            return Enumerable.Empty<CompletionItem>();
        }
    }

    private CompletionContext CreateCompletionContext(
        string commandLine,
        int cursorPosition,
        string workingDirectory,
        IDictionary<string, string> environmentVariables,
        User.User? currentUser)
    {
        // Parse the command line to extract tokens
        var tokens = ParseTokens(commandLine, cursorPosition);
        var currentTokenIndex = FindCurrentTokenIndex(commandLine, cursorPosition, tokens);
        var currentToken = currentTokenIndex < tokens.Length ? tokens[currentTokenIndex] : "";

        return new CompletionContext
        {
            CommandLine = commandLine,
            CursorPosition = cursorPosition,
            Tokens = tokens,
            CurrentTokenIndex = currentTokenIndex,
            CurrentToken = currentToken,
            CurrentWorkingDirectory = workingDirectory,
            EnvironmentVariables = environmentVariables,
            CurrentUser = currentUser
        };
    }

    private string[] ParseTokens(string commandLine, int cursorPosition)
    {
        // Simple tokenization - can be enhanced for better shell parsing
        var beforeCursor = commandLine.Substring(0, Math.Min(cursorPosition, commandLine.Length));
        var tokens = beforeCursor.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        
        // If cursor is after a space, add an empty token for completion
        if (beforeCursor.EndsWith(" ") || beforeCursor.EndsWith("\t"))
        {
            return tokens.Concat(new[] { "" }).ToArray();
        }

        return tokens;
    }

    private int FindCurrentTokenIndex(string commandLine, int cursorPosition, string[] tokens)
    {
        if (tokens.Length == 0)
            return 0;

        // Find which token the cursor is currently in
        var beforeCursor = commandLine.Substring(0, Math.Min(cursorPosition, commandLine.Length));
        
        if (beforeCursor.EndsWith(" ") || beforeCursor.EndsWith("\t"))
        {
            return tokens.Length - 1; // Cursor is after the last token
        }

        return Math.Max(0, tokens.Length - 1);
    }

    private IEnumerable<CompletionItem> ProcessCompletions(List<CompletionItem> completions, CompletionContext context)
    {
        if (!completions.Any())
            return Enumerable.Empty<CompletionItem>();

        var currentToken = context.CurrentToken;

        return completions
            // Filter by current token prefix
            .Where(c => string.IsNullOrEmpty(currentToken) || 
                       c.Text.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
            // Remove duplicates (keep highest priority)
            .GroupBy(c => c.Text, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.Priority).First())
            // Sort by priority then alphabetically
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.Text, StringComparer.OrdinalIgnoreCase);
    }
}
