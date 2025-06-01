
using System.Text;
using System.Text.RegularExpressions;

namespace HackerOs.OS.Shell;

/// <summary>
/// Parser for shell command lines with support for pipes, redirections, and quoting
/// </summary>
public class CommandParser
{
    private static readonly Regex QuoteRegex = new(@"""([^""\\]|\\.)*""|'([^'\\]|\\.)*'", RegexOptions.Compiled);
    private static readonly Regex VariableRegex = new(@"\$\{?(\w+)\}?", RegexOptions.Compiled);

    /// <summary>
    /// Parse a command line into a pipeline of commands
    /// </summary>
    /// <param name="commandLine">Command line to parse</param>
    /// <param name="environmentVariables">Environment variables for substitution</param>
    /// <returns>Parsed command pipeline</returns>
    public static CommandPipeline ParseCommandLine(string commandLine, IDictionary<string, string>? environmentVariables = null)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return new CommandPipeline(new List<ParsedCommand>());
        }

        // Expand environment variables
        var expandedLine = ExpandVariables(commandLine, environmentVariables ?? new Dictionary<string, string>());

        // Split by pipes, but respect quoted strings
        var pipeSegments = SplitRespectingQuotes(expandedLine, '|');
        var commands = new List<ParsedCommand>();

        foreach (var segment in pipeSegments)
        {
            var parsedCommand = ParseSingleCommand(segment.Trim());
            if (parsedCommand != null)
            {
                commands.Add(parsedCommand);
            }
        }

        return new CommandPipeline(commands);
    }

    /// <summary>
    /// Parse a single command with arguments and redirections
    /// </summary>
    private static ParsedCommand? ParseSingleCommand(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return null;
        }

        var redirections = new List<Redirection>();
        var cleanedCommand = commandText;

        // Extract redirections (>, >>, <, 2>, 2>>)
        var redirectionMatches = Regex.Matches(commandText, @"(\d*)([<>]{1,2})\s*([^\s|]+)");
        foreach (Match match in redirectionMatches)
        {
            var fdString = match.Groups[1].Value;
            var operator_ = match.Groups[2].Value;
            var target = match.Groups[3].Value.Trim('"', '\'');

            var fileDescriptor = string.IsNullOrEmpty(fdString) ? 
                (operator_.StartsWith('<') ? 0 : 1) : 
                int.Parse(fdString);

            var type = operator_ switch
            {
                "<" => RedirectionType.Input,
                ">" => RedirectionType.Output,
                ">>" => RedirectionType.Append,
                "2>" => RedirectionType.ErrorOutput,
                "2>>" => RedirectionType.ErrorAppend,
                _ => RedirectionType.Output
            };

            redirections.Add(new Redirection(type, target, fileDescriptor));
            
            // Remove the redirection from the command text
            cleanedCommand = cleanedCommand.Replace(match.Value, "").Trim();
        }

        // Parse command and arguments
        var tokens = SplitRespectingQuotes(cleanedCommand, ' ', '\t');
        if (tokens.Count == 0)
        {
            return null;
        }

        var command = tokens[0];
        var args = tokens.Skip(1).ToArray();

        // Remove quotes from arguments
        for (int i = 0; i < args.Length; i++)
        {
            args[i] = UnquoteString(args[i]);
        }

        return new ParsedCommand(command, args, redirections);
    }

    /// <summary>
    /// Split a string by delimiters while respecting quoted sections
    /// </summary>
    private static List<string> SplitRespectingQuotes(string input, params char[] delimiters)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var quoteChar = '\0';

        for (int i = 0; i < input.Length; i++)
        {
            var ch = input[i];

            if (!inQuotes && (ch == '"' || ch == '\''))
            {
                inQuotes = true;
                quoteChar = ch;
                current.Append(ch);
            }
            else if (inQuotes && ch == quoteChar)
            {
                inQuotes = false;
                current.Append(ch);
            }
            else if (!inQuotes && delimiters.Contains(ch))
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    /// <summary>
    /// Remove quotes from a string
    /// </summary>
    private static string UnquoteString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        if ((input.StartsWith('"') && input.EndsWith('"')) ||
            (input.StartsWith('\'') && input.EndsWith('\'')))
        {
            return input[1..^1];
        }

        return input;
    }

    /// <summary>
    /// Expand environment variables in a string
    /// </summary>
    private static string ExpandVariables(string input, IDictionary<string, string> environmentVariables)
    {
        return VariableRegex.Replace(input, match =>
        {
            var variableName = match.Groups[1].Value;
            return environmentVariables.TryGetValue(variableName, out var value) ? value : match.Value;
        });
    }
}

/// <summary>
/// Represents a pipeline of commands connected by pipes
/// </summary>
public class CommandPipeline
{
    public IReadOnlyList<ParsedCommand> Commands { get; }

    public CommandPipeline(IList<ParsedCommand> commands)
    {
        Commands = commands.ToList();
    }

    public bool IsEmpty => Commands.Count == 0;
    public bool HasMultipleCommands => Commands.Count > 1;
}

/// <summary>
/// Represents a single parsed command with arguments and redirections
/// </summary>
public class ParsedCommand
{
    public string Command { get; }
    public string[] Arguments { get; }
    public IReadOnlyList<Redirection> Redirections { get; }

    public ParsedCommand(string command, string[] arguments, IList<Redirection> redirections)
    {
        Command = command;
        Arguments = arguments;
        Redirections = redirections.ToList();
    }

    public Redirection? GetInputRedirection() =>
        Redirections.FirstOrDefault(r => r.Type == RedirectionType.Input);

    public Redirection? GetOutputRedirection() =>
        Redirections.FirstOrDefault(r => r.Type == RedirectionType.Output || r.Type == RedirectionType.Append);

    public Redirection? GetErrorRedirection() =>
        Redirections.FirstOrDefault(r => r.Type == RedirectionType.ErrorOutput || r.Type == RedirectionType.ErrorAppend);
}

/// <summary>
/// Represents a command redirection (>, >>, <, etc.)
/// </summary>
public class Redirection
{
    public RedirectionType Type { get; }
    public string Target { get; }
    public int FileDescriptor { get; }

    public Redirection(RedirectionType type, string target, int fileDescriptor = 1)
    {
        Type = type;
        Target = target;
        FileDescriptor = fileDescriptor;
    }
}

/// <summary>
/// Types of command redirections
/// </summary>
public enum RedirectionType
{
    Input,          // <
    Output,         // >
    Append,         // >>
    ErrorOutput,    // 2>
    ErrorAppend     // 2>>
}
