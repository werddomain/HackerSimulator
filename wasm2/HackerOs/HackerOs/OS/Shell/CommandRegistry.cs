
using System.Collections.Concurrent;

namespace HackerOs.OS.Shell;

/// <summary>
/// Registry for managing available shell commands
/// </summary>
public class CommandRegistry : ICommandRegistry
{
    private readonly ConcurrentDictionary<string, ICommand> _commands = new();
    private readonly ConcurrentDictionary<string, string> _aliases = new();

    public IReadOnlyCollection<ICommand> Commands => _commands.Values.ToList().AsReadOnly();
    public IReadOnlyDictionary<string, string> Aliases => _aliases;

    /// <summary>
    /// Register a command in the registry
    /// </summary>
    /// <param name="command">Command to register</param>
    public void RegisterCommand(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.TryAdd(command.Name.ToLowerInvariant(), command);
    }

    /// <summary>
    /// Register multiple commands at once
    /// </summary>
    /// <param name="commands">Commands to register</param>
    public void RegisterCommands(IEnumerable<ICommand> commands)
    {
        foreach (var command in commands)
        {
            RegisterCommand(command);
        }
    }

    /// <summary>
    /// Unregister a command from the registry
    /// </summary>
    /// <param name="commandName">Name of command to unregister</param>
    /// <returns>True if command was found and removed</returns>
    public bool UnregisterCommand(string commandName)
    {
        return _commands.TryRemove(commandName.ToLowerInvariant(), out _);
    }

    /// <summary>
    /// Get a command by name
    /// </summary>
    /// <param name="commandName">Command name or alias</param>
    /// <returns>Command instance or null if not found</returns>
    public ICommand? GetCommand(string commandName)
    {
        var lowerName = commandName.ToLowerInvariant();
        
        // Check direct command name
        if (_commands.TryGetValue(lowerName, out var command))
        {
            return command;
        }

        // Check aliases
        if (_aliases.TryGetValue(lowerName, out var aliasTarget))
        {
            return _commands.TryGetValue(aliasTarget.ToLowerInvariant(), out command) ? command : null;
        }

        return null;
    }

    /// <summary>
    /// Check if a command exists
    /// </summary>
    /// <param name="commandName">Command name or alias</param>
    /// <returns>True if command exists</returns>
    public bool HasCommand(string commandName)
    {
        return GetCommand(commandName) != null;
    }

    /// <summary>
    /// Register a command alias
    /// </summary>
    /// <param name="alias">Alias name</param>
    /// <param name="commandName">Target command name</param>
    public void RegisterAlias(string alias, string commandName)
    {
        var lowerAlias = alias.ToLowerInvariant();
        var lowerCommand = commandName.ToLowerInvariant();
        
        if (_commands.ContainsKey(lowerCommand))
        {
            _aliases.TryAdd(lowerAlias, lowerCommand);
        }
    }

    /// <summary>
    /// Unregister a command alias
    /// </summary>
    /// <param name="alias">Alias to remove</param>
    /// <returns>True if alias was found and removed</returns>
    public bool UnregisterAlias(string alias)
    {
        return _aliases.TryRemove(alias.ToLowerInvariant(), out _);
    }

    /// <summary>
    /// Get all command names (including aliases) that start with the given prefix
    /// </summary>
    /// <param name="prefix">Command prefix to search for</param>
    /// <returns>Matching command names</returns>
    public IEnumerable<string> GetCommandNamesStartingWith(string prefix)
    {
        var lowerPrefix = prefix.ToLowerInvariant();
        
        var commandNames = _commands.Keys
            .Where(name => name.StartsWith(lowerPrefix))
            .ToList();

        var aliasNames = _aliases.Keys
            .Where(alias => alias.StartsWith(lowerPrefix))
            .ToList();

        return commandNames.Concat(aliasNames).Distinct().OrderBy(name => name);
    }

    /// <summary>
    /// Get help information for all commands
    /// </summary>
    /// <returns>Dictionary of command names to descriptions</returns>
    public IReadOnlyDictionary<string, string> GetCommandHelp()
    {
        return _commands.Values
            .ToDictionary(cmd => cmd.Name, cmd => cmd.Description);
    }

    /// <summary>
    /// Clear all registered commands and aliases
    /// </summary>
    public void Clear()
    {
        _commands.Clear();
        _aliases.Clear();
    }
}

/// <summary>
/// Interface for command registry
/// </summary>
public interface ICommandRegistry
{
    /// <summary>
    /// All registered commands
    /// </summary>
    IReadOnlyCollection<ICommand> Commands { get; }

    /// <summary>
    /// All registered aliases
    /// </summary>
    IReadOnlyDictionary<string, string> Aliases { get; }

    /// <summary>
    /// Register a command
    /// </summary>
    void RegisterCommand(ICommand command);

    /// <summary>
    /// Register multiple commands
    /// </summary>
    void RegisterCommands(IEnumerable<ICommand> commands);

    /// <summary>
    /// Unregister a command
    /// </summary>
    bool UnregisterCommand(string commandName);

    /// <summary>
    /// Get a command by name or alias
    /// </summary>
    ICommand? GetCommand(string commandName);

    /// <summary>
    /// Check if a command exists
    /// </summary>
    bool HasCommand(string commandName);

    /// <summary>
    /// Register a command alias
    /// </summary>
    void RegisterAlias(string alias, string commandName);

    /// <summary>
    /// Unregister a command alias
    /// </summary>
    bool UnregisterAlias(string alias);

    /// <summary>
    /// Get command names starting with prefix
    /// </summary>
    IEnumerable<string> GetCommandNamesStartingWith(string prefix);

    /// <summary>
    /// Get help information for all commands
    /// </summary>
    IReadOnlyDictionary<string, string> GetCommandHelp();

    /// <summary>
    /// Clear all commands and aliases
    /// </summary>
    void Clear();
}
