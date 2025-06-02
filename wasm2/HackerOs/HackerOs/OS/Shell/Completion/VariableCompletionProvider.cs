using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HackerOs.OS.Shell.Completion;

/// <summary>
/// Provides completion for environment variables
/// </summary>
public class VariableCompletionProvider : ICompletionProvider
{
    public int Priority => 60; // Medium priority for variables

    public bool CanProvideCompletions(CompletionContext context)
    {
        // Provide variable completions when the current token starts with $
        return context.CurrentToken.StartsWith("$");
    }

    public async Task<IEnumerable<CompletionItem>> GetCompletionsAsync(CompletionContext context)
    {
        if (!CanProvideCompletions(context))
        {
            return Enumerable.Empty<CompletionItem>();
        }

        await Task.CompletedTask; // Environment variables are synchronous

        var prefix = context.CurrentToken.Substring(1); // Remove the $
        var completions = new List<CompletionItem>();

        foreach (var variable in context.EnvironmentVariables)
        {
            if (string.IsNullOrEmpty(prefix) || variable.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                completions.Add(new CompletionItem(
                    text: "$" + variable.Key,
                    type: CompletionType.Variable,
                    description: $"Value: {variable.Value}",
                    priority: 60
                ));
            }
        }

        return completions;
    }
}
