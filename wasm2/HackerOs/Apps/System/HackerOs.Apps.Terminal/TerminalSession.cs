namespace HackerOs.Apps.Terminal;

/// <summary>
/// Maintains interactive terminal session state including working directory, environment variables,
/// command history, and execution status per `P2-TERM-002`.
/// </summary>
public sealed class TerminalSession
{
    private readonly List<string> _history = [];
    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);
    private int _historyIndex = -1;

    /// <summary>Initializes a terminal session for the specified user.</summary>
    public TerminalSession(string username = "user", string initialCwd = "/home/user")
    {
        Username = username;
        Cwd = initialCwd;

        _environment["USER"] = username;
        _environment["HOME"] = initialCwd;
        _environment["SHELL"] = "/bin/shell";
        _environment["TERM"] = "xterm-256color";
        _environment["PATH"] = "/bin:/usr/bin";
    }

    /// <summary>Gets the acting session user name.</summary>
    public string Username { get; private set; }

    /// <summary>Re-initializes the session username and working directory for an authenticated user.</summary>
    public void InitializeUser(string username, string homePath)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            Username = username;
            _environment["USER"] = username;
        }

        if (!string.IsNullOrWhiteSpace(homePath))
        {
            _environment["HOME"] = homePath;
            SetCwd(homePath);
        }
    }

    /// <summary>Gets the current working directory.</summary>
    public string Cwd { get; private set; }

    /// <summary>Gets session environment variables.</summary>
    public IReadOnlyDictionary<string, string> Environment => _environment;

    /// <summary>Gets command execution history.</summary>
    public IReadOnlyList<string> History => _history;

    /// <summary>Gets or sets the exit code of the last executed command.</summary>
    public int LastExitCode { get; set; }

    /// <summary>Appends a command line to history and resets history navigation.</summary>
    public void AddHistory(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return;
        }

        if (_history.Count == 0 || _history[^1] != commandLine)
        {
            _history.Add(commandLine);
        }

        _historyIndex = _history.Count;
    }

    /// <summary>Navigates command history up (previous, delta &lt; 0) or down (next, delta &gt; 0).</summary>
    public string? NavigateHistory(int delta, string currentDraft)
    {
        if (_history.Count == 0)
        {
            return null;
        }

        int targetIndex = Math.Clamp(_historyIndex + delta, 0, _history.Count);
        if (targetIndex == _history.Count)
        {
            _historyIndex = targetIndex;
            return string.Empty;
        }

        _historyIndex = targetIndex;
        return _history[_historyIndex];
    }

    /// <summary>Updates the working directory.</summary>
    public void SetCwd(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Cwd = ResolvePath(path);
    }

    /// <summary>
    /// Resolves a <c>cd</c>-style target (absolute, <c>..</c>, <c>~</c>, or relative) against the
    /// current working directory, without mutating <see cref="Cwd"/>.
    /// </summary>
    public string ResolvePath(string path)
    {
        if (path.StartsWith('/'))
        {
            return NormalizePath(path);
        }
        if (path == "..")
        {
            int lastSlash = Cwd.LastIndexOf('/');
            return lastSlash > 0 ? Cwd[..lastSlash] : "/";
        }
        if (path == "~")
        {
            return _environment.TryGetValue("HOME", out string? home) ? home : "/home/user";
        }
        return NormalizePath($"{Cwd.TrimEnd('/')}/{path}");
    }

    /// <summary>Sets or updates an environment variable.</summary>
    public void SetEnvironment(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        _environment[name] = value;
    }

    /// <summary>Gets the formatted prompt string (e.g. <c>user@hackeros:/home/user$</c>).</summary>
    public string GetPrompt()
    {
        string displayPath = Cwd;
        if (_environment.TryGetValue("HOME", out string? home) && Cwd.StartsWith(home, StringComparison.Ordinal))
        {
            displayPath = $"~{Cwd[home.Length..]}";
        }

        char promptChar = Username == "root" ? '#' : '$';
        return $"{Username}@hackeros:{displayPath}{promptChar}";
    }

    private static string NormalizePath(string rawPath)
    {
        string[] segments = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Stack<string> stack = new();

        foreach (string segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }
            if (segment == "..")
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
            }
            else
            {
                stack.Push(segment);
            }
        }

        string result = "/" + string.Join('/', stack.ToArray().Reverse());
        return result;
    }
}
