using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HackerOs.OS.Shell.Completion;

/// <summary>
/// Provides completion for command names
/// </summary>
public class CommandCompletionProvider : ICompletionProvider
{
    private readonly ICommandRegistry _commandRegistry;

    public int Priority => 100; // High priority for command completion

    public CommandCompletionProvider(ICommandRegistry commandRegistry)
    {
        _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
    }

    public bool CanProvideCompletions(CompletionContext context)
    {
        // Provide command completions when at the command position (first token)
        return context.IsCommandPosition;
    }

    public async Task<IEnumerable<CompletionItem>> GetCompletionsAsync(CompletionContext context)
    {
        if (!CanProvideCompletions(context))
        {
            return Enumerable.Empty<CompletionItem>();
        }

        await Task.CompletedTask; // CommandRegistry is synchronous

        var commandNames = _commandRegistry.GetCommandNamesStartingWith(context.CurrentToken);
        
        return commandNames.Select(name => new CompletionItem(
            text: name,
            type: CompletionType.Command,
            description: GetCommandDescription(name),
            priority: 100
        ));
    }

    private string GetCommandDescription(string commandName)
    {
        try
        {
            var command = _commandRegistry.GetCommand(commandName);
            return command?.Description ?? "";
        }
        catch
        {
            return "";
        }
    }
}
