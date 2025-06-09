
using System.Text;
using HackerOs.OS.Shell;

namespace HackerOs.OS.Shell.Commands;

/// <summary>
/// Echo command for displaying text
/// </summary>
public class EchoCommand : CommandBase
{
    public override string Name => "echo";
    public override string Description => "Display a line of text";
    public override string Usage => "echo [OPTIONS] [STRING...]";

    public override IReadOnlyList<CommandOption> Options => new List<CommandOption>
    {
        new("n", null, "Do not output trailing newline"),
        new("e", null, "Enable interpretation of backslash escapes"),
        new("E", null, "Disable interpretation of backslash escapes (default)")
    };

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        string[] args,
        Stream stdin,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parsedArgs = ParseArguments(args, Options);
            var text = string.Join(" ", parsedArgs.Parameters);

            // Handle escape sequences if -e option is provided
            if (parsedArgs.HasOption("e"))
            {
                text = ProcessEscapeSequences(text);
            }

            // Expand environment variables
            text = ExpandEnvironmentVariables(text, context.EnvironmentVariables);

            // Write output
            if (parsedArgs.HasOption("n"))
            {
                await WriteAsync(stdout, text);
            }
            else
            {
                await WriteLineAsync(stdout, text);
            }

            return 0;
        }
        catch (Exception ex)
        {
            await WriteLineAsync(stderr, $"echo: {ex.Message}");
            return 1;
        }
    }

    public override Task<IEnumerable<string>> GetCompletionsAsync(
        CommandContext context,
        string[] args,
        string currentArg)
    {
        // Echo doesn't typically need file completions, but we can provide variable completions
        var completions = new List<string>();

        if (currentArg.StartsWith("$"))
        {
            var variablePrefix = currentArg[1..];
            foreach (var envVar in context.EnvironmentVariables.Keys)
            {
                if (envVar.StartsWith(variablePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    completions.Add($"${envVar}");
                }
            }
        }

        return Task.FromResult<IEnumerable<string>>(completions);
    }

    private static string ProcessEscapeSequences(string text)
    {
        var result = new StringBuilder();
        
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                var nextChar = text[i + 1];
                switch (nextChar)
                {
                    case 'n':
                        result.Append('\n');
                        i++; // Skip the next character
                        break;
                    case 't':
                        result.Append('\t');
                        i++;
                        break;
                    case 'r':
                        result.Append('\r');
                        i++;
                        break;
                    case 'b':
                        result.Append('\b');
                        i++;
                        break;
                    case 'a':
                        result.Append('\a');
                        i++;
                        break;
                    case 'f':
                        result.Append('\f');
                        i++;
                        break;
                    case 'v':
                        result.Append('\v');
                        i++;
                        break;
                    case '\\':
                        result.Append('\\');
                        i++;
                        break;
                    case '"':
                        result.Append('"');
                        i++;
                        break;
                    case '\'':
                        result.Append('\'');
                        i++;
                        break;
                    default:
                        // Unknown escape sequence, keep the backslash
                        result.Append('\\');
                        break;
                }
            }
            else
            {
                result.Append(text[i]);
            }
        }
        
        return result.ToString();
    }

    private static string ExpandEnvironmentVariables(string text, IDictionary<string, string> environmentVariables)
    {
        var result = text;
        
        // Simple variable expansion: $VAR or ${VAR}
        foreach (var kvp in environmentVariables)
        {
            var simpleVar = $"${kvp.Key}";
            var bracedVar = $"${{{kvp.Key}}}";
            
            result = result.Replace(simpleVar, kvp.Value);
            result = result.Replace(bracedVar, kvp.Value);
        }
        
        return result;
    }
}
