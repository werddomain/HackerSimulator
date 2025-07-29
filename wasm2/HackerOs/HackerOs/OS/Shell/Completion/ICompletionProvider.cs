using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Shell.Completion;

/// <summary>
/// Context information for tab completion
/// </summary>
public class CompletionContext
{
    public string CommandLine { get; init; } = string.Empty;
    public int CursorPosition { get; init; }
    public string[] Tokens { get; init; } = Array.Empty<string>();
    public int CurrentTokenIndex { get; init; }
    public string CurrentToken { get; init; } = string.Empty;
    public string CurrentWorkingDirectory { get; init; } = string.Empty;
    public IDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>();
    public User.User? CurrentUser { get; init; }

    public bool IsCommandPosition => CurrentTokenIndex == 0;
    public bool IsArgumentPosition => CurrentTokenIndex > 0;
}

/// <summary>
/// Represents a completion suggestion
/// </summary>
public class CompletionItem
{
    public string Text { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public CompletionType Type { get; init; }
    public int Priority { get; init; } = 0;

    public CompletionItem(string text, CompletionType type, string description = "", int priority = 0)
    {
        Text = text;
        Type = type;
        Description = description;
        Priority = priority;
    }
}

/// <summary>
/// Types of completion items
/// </summary>
public enum CompletionType
{
    Command,
    File,
    Directory,
    Variable,
    Option,
    Alias,
    Keyword
}

/// <summary>
/// Base interface for completion providers
/// </summary>
public interface ICompletionProvider
{
    /// <summary>
    /// Gets the priority of this completion provider (higher = more important)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Determines if this provider can handle the given completion context
    /// </summary>
    /// <param name="context">The completion context</param>
    /// <returns>True if this provider can provide completions for the context</returns>
    bool CanProvideCompletions(CompletionContext context);

    /// <summary>
    /// Provides completion suggestions for the given context
    /// </summary>
    /// <param name="context">The completion context</param>
    /// <returns>A collection of completion suggestions</returns>
    Task<IEnumerable<CompletionItem>> GetCompletionsAsync(CompletionContext context);
}

/// <summary>
/// Main completion service that aggregates results from multiple providers
/// </summary>
public interface ICompletionService
{
    /// <summary>
    /// Registers a completion provider
    /// </summary>
    /// <param name="provider">The completion provider to register</param>
    void RegisterProvider(ICompletionProvider provider);

    /// <summary>
    /// Unregisters a completion provider
    /// </summary>
    /// <param name="provider">The completion provider to unregister</param>
    void UnregisterProvider(ICompletionProvider provider);

    /// <summary>
    /// Gets completion suggestions for the given input
    /// </summary>
    /// <param name="commandLine">The current command line</param>
    /// <param name="cursorPosition">The current cursor position</param>
    /// <param name="workingDirectory">The current working directory</param>
    /// <param name="environmentVariables">Environment variables</param>
    /// <param name="currentUser">The current user</param>
    /// <returns>A collection of completion suggestions</returns>
    Task<IEnumerable<CompletionItem>> GetCompletionsAsync(
        string commandLine,
        int cursorPosition,
        string workingDirectory,
        IDictionary<string, string> environmentVariables,
        User.User? currentUser = null);
}
