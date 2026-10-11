using HackerOs.AppSdk;
using HackerOs.AppSdk.Shell;

namespace HackerOs.Platform.Core.Shell;

/// <summary>
/// Tab completion done by the shell, as <c>bash</c> does (ADR 0058, section 4), not by the
/// terminal emulator. This version completes command names; path completion is added by the
/// <c>4-terminal</c> chantier.
/// </summary>
internal static class ShellCompletion
{
    /// <summary>Characters that end a command and start a new one: <c>|</c>, <c>&amp;&amp;</c>, <c>;</c>.</summary>
    private static readonly char[] CommandSeparators = ['|', '&', ';'];

    /// <summary>Computes the candidates for the word that ends at <paramref name="cursor"/>.</summary>
    /// <param name="commandLine">The line being typed.</param>
    /// <param name="cursor">Cursor position.</param>
    /// <param name="commands">Installed commands (names and static aliases).</param>
    /// <returns>The completion, empty when the word is not a command position or nothing matches.</returns>
    public static HackShellCompletion Complete(string commandLine, int cursor, IReadOnlyList<ShellCommandInfo> commands)
    {
        int wordStart = cursor;
        while (wordStart > 0 && !char.IsWhiteSpace(commandLine[wordStart - 1]) && Array.IndexOf(CommandSeparators, commandLine[wordStart - 1]) < 0)
        {
            wordStart--;
        }

        if (!IsCommandPosition(commandLine, wordStart))
        {
            // Arguments are paths: completed once 4-terminal adds them.
            return HackShellCompletion.None(cursor);
        }

        string word = commandLine[wordStart..cursor];
        // Case-sensitive, as a Linux shell: `LS` is not `ls`.
        string[] candidates = [.. commands
            .SelectMany(static command => command.Aliases.Prepend(command.Name))
            .Where(name => name.StartsWith(word, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        return new HackShellCompletion(wordStart, cursor - wordStart, candidates);
    }

    /// <summary>Whether only blanks separate <paramref name="wordStart"/> from the line start or a command separator.</summary>
    private static bool IsCommandPosition(string commandLine, int wordStart)
    {
        for (int index = wordStart - 1; index >= 0; index--)
        {
            char character = commandLine[index];
            if (Array.IndexOf(CommandSeparators, character) >= 0)
            {
                return true;
            }

            if (!char.IsWhiteSpace(character))
            {
                return false;
            }
        }

        return true;
    }
}
