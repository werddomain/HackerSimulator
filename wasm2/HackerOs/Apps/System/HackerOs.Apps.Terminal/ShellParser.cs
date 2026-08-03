using System.Text;

namespace HackerOs.Apps.Terminal;

/// <summary>Represents a parsed shell command line or diagnostic error.</summary>
public sealed record ShellParseResult(
    string CommandName,
    IReadOnlyList<string> Arguments,
    string? ErrorMessage = null)
{
    /// <summary>Gets whether parsing succeeded without syntax errors.</summary>
    public bool IsSuccess => ErrorMessage is null;
}

/// <summary>
/// Tokenizes and parses shell command lines according to ADR 0014 syntax rules (`P2-TERM-003`).
/// </summary>
public static class ShellParser
{
    /// <summary>Parses a command line into command name and arguments with quote/escape handling.</summary>
    public static ShellParseResult Parse(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return new ShellParseResult(string.Empty, [], ErrorMessage: null);
        }

        List<string> tokens = [];
        StringBuilder current = new();
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool isEscaped = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            char ch = commandLine[i];

            if (isEscaped)
            {
                current.Append(ch);
                isEscaped = false;
                continue;
            }

            if (ch == '\\' && !inSingleQuote)
            {
                isEscaped = true;
                continue;
            }

            if (ch == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (ch == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inSingleQuote && !inDoubleQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(ch);
        }

        if (isEscaped)
        {
            return new ShellParseResult(string.Empty, [], "Syntax error: trailing escape character '\\'");
        }

        if (inSingleQuote)
        {
            return new ShellParseResult(string.Empty, [], "Syntax error: unterminated single quote");
        }

        if (inDoubleQuote)
        {
            return new ShellParseResult(string.Empty, [], "Syntax error: unterminated double quote");
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        if (tokens.Count == 0)
        {
            return new ShellParseResult(string.Empty, [], ErrorMessage: null);
        }

        return new ShellParseResult(tokens[0], tokens[1..], ErrorMessage: null);
    }
}
