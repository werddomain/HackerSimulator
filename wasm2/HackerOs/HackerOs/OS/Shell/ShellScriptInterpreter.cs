using System.Text;
using System.Text.RegularExpressions;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Shell;

/// <summary>
/// Interpreter for shell scripts with basic scripting constructs
/// </summary>
public class ShellScriptInterpreter
{
    private readonly IShell _shell;
    private readonly IVirtualFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly Dictionary<string, string> _scriptVariables = new();

    public ShellScriptInterpreter(
        IShell shell,
        IVirtualFileSystem fileSystem,
        ILogger logger)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }/// <summary>
    /// Execute a shell script from file
    /// </summary>
    public async Task<int> ExecuteScriptAsync(
        string scriptPath,
        string[] args,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // We need a user context - get it from the shell's current session
            var currentUser = _shell.CurrentSession?.User;
            if (currentUser == null)
            {
                await WriteToStreamAsync(stderr, "No active user session\n");
                return 1;
            }

            // Read script content
            if (!await _fileSystem.FileExistsAsync(scriptPath, currentUser))
            {
                await WriteToStreamAsync(stderr, $"Script not found: {scriptPath}\n");
                return 1;
            }            var content = await _fileSystem.ReadAllTextAsync(scriptPath, currentUser);
            if (string.IsNullOrEmpty(content))
            {
                await WriteToStreamAsync(stderr, $"Script is empty: {scriptPath}\n");
                return 1;
            }
            
            return await ExecuteScriptContentAsync(content, args, stdout, stderr, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing script {ScriptPath}", scriptPath);
            await WriteToStreamAsync(stderr, $"Error executing script: {ex.Message}\n");
            return 1;
        }
    }

    /// <summary>
    /// Execute shell script content
    /// </summary>
    public async Task<int> ExecuteScriptContentAsync(
        string scriptContent,
        string[] args,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken = default)
    {        // Set up script arguments
        _scriptVariables.Clear();
        _scriptVariables["0"] = "script"; // Script name
        for (int i = 0; i < args.Length; i++)
        {
            _scriptVariables[(i + 1).ToString()] = args[i];
        }
        _scriptVariables["#"] = args.Length.ToString();
        _scriptVariables["@"] = string.Join(" ", args);

        var lines = scriptContent.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .ToArray();

        int exitCode = 0;
        int currentLine = 0;

        while (currentLine < lines.Length)
        {
            var line = lines[currentLine].Trim();
            currentLine++;

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            try
            {
                // Handle variable assignments
                if (IsVariableAssignment(line))
                {
                    ProcessVariableAssignment(line);
                    continue;
                }

                // Handle if statements
                if (line.StartsWith("if "))
                {
                    var (skipTo, conditionMet) = await ProcessIfStatement(lines, currentLine - 1, stdout, stderr, cancellationToken);
                    currentLine = skipTo;
                    continue;
                }

                // Handle for loops
                if (line.StartsWith("for "))
                {
                    var skipTo = await ProcessForLoop(lines, currentLine - 1, stdout, stderr, cancellationToken);
                    currentLine = skipTo;
                    continue;
                }

                // Handle while loops
                if (line.StartsWith("while "))
                {
                    var skipTo = await ProcessWhileLoop(lines, currentLine - 1, stdout, stderr, cancellationToken);
                    currentLine = skipTo;
                    continue;
                }

                // Skip control structure keywords when encountered outside context
                if (IsControlKeyword(line))
                    continue;                // Execute regular command
                var expandedLine = ExpandScriptVariables(line);
                exitCode = await _shell.ExecuteCommandAsync(expandedLine, cancellationToken);

                // Handle exit command
                if (expandedLine.Trim().StartsWith("exit"))
                {
                    var exitArgs = expandedLine.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (exitArgs.Length > 1 && int.TryParse(exitArgs[1], out var code))
                    {
                        exitCode = code;
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing script line: {Line}", line);
                await WriteToStreamAsync(stderr, $"Error on line {currentLine}: {ex.Message}\n");
                exitCode = 1;
            }
        }

        return exitCode;
    }

    /// <summary>
    /// Check if line is a variable assignment
    /// </summary>
    private static bool IsVariableAssignment(string line)
    {
        return Regex.IsMatch(line, @"^\s*\w+\s*=");
    }

    /// <summary>
    /// Process variable assignment
    /// </summary>
    private void ProcessVariableAssignment(string line)
    {
        var match = Regex.Match(line, @"^\s*(\w+)\s*=\s*(.*)");
        if (match.Success)
        {
            var varName = match.Groups[1].Value;
            var varValue = match.Groups[2].Value.Trim();
            
            // Remove quotes if present
            if ((varValue.StartsWith('"') && varValue.EndsWith('"')) ||
                (varValue.StartsWith('\'') && varValue.EndsWith('\'')))
            {
                varValue = varValue.Substring(1, varValue.Length - 2);
            }

            varValue = ExpandScriptVariables(varValue);
            _scriptVariables[varName] = varValue;
        }
    }

    /// <summary>
    /// Process if statement
    /// </summary>
    private async Task<(int nextLine, bool conditionMet)> ProcessIfStatement(
        string[] lines,
        int startLine,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken)
    {
        var ifLine = lines[startLine].Trim();
        var condition = ifLine.Substring(3).Trim(); // Remove "if "

        bool conditionMet = await EvaluateCondition(condition, stdout, stderr, cancellationToken);
        
        int currentLine = startLine + 1;
        int elseLineIndex = -1;
        int fiLineIndex = -1;
        int nestLevel = 0;

        // Find else and fi
        while (currentLine < lines.Length)
        {
            var line = lines[currentLine].Trim();
            
            if (line.StartsWith("if "))
                nestLevel++;
            else if (line == "fi")
            {
                if (nestLevel == 0)
                {
                    fiLineIndex = currentLine;
                    break;
                }
                nestLevel--;
            }
            else if (line == "else" && nestLevel == 0)
                elseLineIndex = currentLine;
            
            currentLine++;
        }

        if (fiLineIndex == -1)
        {
            await WriteToStreamAsync(stderr, "Error: if statement without matching fi\n");
            return (lines.Length, false);
        }

        // Execute appropriate block
        if (conditionMet)
        {
            // Execute if block
            var endLine = elseLineIndex != -1 ? elseLineIndex : fiLineIndex;
            await ExecuteScriptBlock(lines, startLine + 1, endLine, stdout, stderr, cancellationToken);
        }
        else if (elseLineIndex != -1)
        {
            // Execute else block
            await ExecuteScriptBlock(lines, elseLineIndex + 1, fiLineIndex, stdout, stderr, cancellationToken);
        }

        return (fiLineIndex + 1, conditionMet);
    }

    /// <summary>
    /// Process for loop
    /// </summary>
    private async Task<int> ProcessForLoop(
        string[] lines,
        int startLine,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken)
    {
        var forLine = lines[startLine].Trim();
        var match = Regex.Match(forLine, @"for\s+(\w+)\s+in\s+(.+)");
        
        if (!match.Success)
        {
            await WriteToStreamAsync(stderr, "Error: Invalid for loop syntax\n");
            return startLine + 1;
        }

        var varName = match.Groups[1].Value;
        var itemsStr = ExpandScriptVariables(match.Groups[2].Value);
        var items = itemsStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Find done
        int doneLineIndex = FindLoopEnd(lines, startLine, "for", "done");
        if (doneLineIndex == -1)
        {
            await WriteToStreamAsync(stderr, "Error: for loop without matching done\n");
            return lines.Length;
        }

        // Execute loop body for each item
        foreach (var item in items)
        {
            _scriptVariables[varName] = item;
            await ExecuteScriptBlock(lines, startLine + 1, doneLineIndex, stdout, stderr, cancellationToken);
        }

        return doneLineIndex + 1;
    }

    /// <summary>
    /// Process while loop
    /// </summary>
    private async Task<int> ProcessWhileLoop(
        string[] lines,
        int startLine,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken)
    {
        var whileLine = lines[startLine].Trim();
        var condition = whileLine.Substring(6).Trim(); // Remove "while "

        // Find done
        int doneLineIndex = FindLoopEnd(lines, startLine, "while", "done");
        if (doneLineIndex == -1)
        {
            await WriteToStreamAsync(stderr, "Error: while loop without matching done\n");
            return lines.Length;
        }

        // Execute loop while condition is true
        while (await EvaluateCondition(condition, stdout, stderr, cancellationToken))
        {
            await ExecuteScriptBlock(lines, startLine + 1, doneLineIndex, stdout, stderr, cancellationToken);
        }

        return doneLineIndex + 1;
    }

    /// <summary>
    /// Execute a block of script lines
    /// </summary>
    private async Task ExecuteScriptBlock(
        string[] lines,
        int startLine,
        int endLine,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken)
    {
        var blockContent = string.Join("\n", lines.Skip(startLine).Take(endLine - startLine));
        await ExecuteScriptContentAsync(blockContent, Array.Empty<string>(), stdout, stderr, cancellationToken);
    }

    /// <summary>
    /// Evaluate a condition for if/while statements
    /// </summary>
    private async Task<bool> EvaluateCondition(
        string condition,
        Stream stdout,
        Stream stderr,
        CancellationToken cancellationToken)
    {
        // Expand variables in condition
        condition = ExpandScriptVariables(condition);

        // Handle test command syntax [ ... ]
        if (condition.StartsWith('[') && condition.EndsWith(']'))
        {
            condition = condition.Substring(1, condition.Length - 2).Trim();
            return EvaluateTestCondition(condition);
        }        // Execute as command and check exit code
        try
        {
            var exitCode = await _shell.ExecuteCommandAsync(condition, cancellationToken);
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Evaluate test condition (simplified)
    /// </summary>
    private bool EvaluateTestCondition(string condition)
    {
        var parts = condition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 3)
        {
            var left = parts[0];
            var op = parts[1];
            var right = parts[2];

            return op switch
            {
                "=" or "==" => left == right,
                "!=" => left != right,
                "-eq" => int.TryParse(left, out var l) && int.TryParse(right, out var r) && l == r,
                "-ne" => int.TryParse(left, out var l2) && int.TryParse(right, out var r2) && l2 != r2,
                "-lt" => int.TryParse(left, out var l3) && int.TryParse(right, out var r3) && l3 < r3,
                "-le" => int.TryParse(left, out var l4) && int.TryParse(right, out var r4) && l4 <= r4,
                "-gt" => int.TryParse(left, out var l5) && int.TryParse(right, out var r5) && l5 > r5,
                "-ge" => int.TryParse(left, out var l6) && int.TryParse(right, out var r6) && l6 >= r6,
                _ => false
            };
        }

        return false;
    }

    /// <summary>
    /// Find the end of a loop structure
    /// </summary>
    private static int FindLoopEnd(string[] lines, int startLine, string startKeyword, string endKeyword)
    {
        int nestLevel = 0;
        for (int i = startLine + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith(startKeyword + " "))
                nestLevel++;
            else if (line == endKeyword)
            {
                if (nestLevel == 0)
                    return i;
                nestLevel--;
            }
        }
        return -1;
    }

    /// <summary>
    /// Expand script variables in text
    /// </summary>
    private string ExpandScriptVariables(string text)
    {
        // First expand shell environment variables
        foreach (var envVar in _shell.EnvironmentVariables)
        {
            text = text.Replace($"${envVar.Key}", envVar.Value);
            text = text.Replace($"${{{envVar.Key}}}", envVar.Value);
        }

        // Then expand script variables
        foreach (var scriptVar in _scriptVariables)
        {
            text = text.Replace($"${scriptVar.Key}", scriptVar.Value);
            text = text.Replace($"${{{scriptVar.Key}}}", scriptVar.Value);
        }

        return text;
    }

    /// <summary>
    /// Check if line is a control structure keyword
    /// </summary>
    private static bool IsControlKeyword(string line)
    {
        return line == "fi" || line == "else" || line == "done";
    }

    /// <summary>
    /// Write text to stream
    /// </summary>
    private static async Task WriteToStreamAsync(Stream stream, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }
}
