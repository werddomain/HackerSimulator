using System.Collections.Concurrent;
using System.Text;
using System.IO;
using HackerOs.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Shell;

/// <summary>
/// Main shell implementation with command execution and session management
/// </summary>
public class Shell : IShell
{
    private readonly ICommandRegistry _commandRegistry;
    private readonly IVirtualFileSystem _fileSystem;
    private readonly ILogger<Shell> _logger;
    private readonly List<string> _commandHistory = new();
    private readonly Dictionary<string, string> _environmentVariables = new();
    
    private UserSession? _currentSession;
    private string _currentWorkingDirectory = "/";
    private const int MaxHistorySize = 1000;

    public UserSession? CurrentSession => _currentSession;
    public string CurrentWorkingDirectory 
    { 
        get => _currentWorkingDirectory;
        set 
        {
            var previousDirectory = _currentWorkingDirectory;
            _currentWorkingDirectory = value;
            DirectoryChanged?.Invoke(this, new DirectoryChangedEventArgs(previousDirectory, value));
        }
    }    public IDictionary<string, string> EnvironmentVariables => _environmentVariables;
    public IReadOnlyList<string> CommandHistory => _commandHistory.AsReadOnly();
    public IVirtualFileSystem FileSystem => _fileSystem;

    public event EventHandler<ShellOutputEventArgs>? OutputReceived;
    public event EventHandler<ShellErrorEventArgs>? ErrorReceived;
    public event EventHandler<DirectoryChangedEventArgs>? DirectoryChanged;

    public Shell(
        ICommandRegistry commandRegistry,
        IVirtualFileSystem fileSystem,
        ILogger<Shell> logger)
    {
        _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeDefaultEnvironment();
    }

    public async Task<bool> InitializeAsync(UserSession session)
    {
        try
        {
            _currentSession = session;
            _currentWorkingDirectory = session.User.HomeDirectory;

            // Initialize environment variables for this session
            InitializeSessionEnvironment(session);

            // Load command history for this user
            await LoadHistoryAsync();

            _logger.LogInformation("Shell initialized for user {Username}", session.User.Username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize shell for user {Username}", session?.User?.Username);
            ErrorReceived?.Invoke(this, new ShellErrorEventArgs("Failed to initialize shell", ex));
            return false;
        }
    }

    public async Task<int> ExecuteCommandAsync(string commandLine, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return 0;
        }

        if (_currentSession == null)
        {
            ErrorReceived?.Invoke(this, new ShellErrorEventArgs("No active user session"));
            return 1;
        }

        try
        {
            // Add to history
            AddToHistory(commandLine);

            // Parse the command line
            var pipeline = CommandParser.ParseCommandLine(commandLine, _environmentVariables);
            
            if (pipeline.IsEmpty)
            {
                return 0;
            }

            // Execute the pipeline
            return await ExecutePipelineAsync(pipeline, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            OutputReceived?.Invoke(this, new ShellOutputEventArgs("^C", true));
            return 130; // Standard exit code for SIGINT
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {CommandLine}", commandLine);
            ErrorReceived?.Invoke(this, new ShellErrorEventArgs($"Command execution failed: {ex.Message}", ex));
            return 1;
        }
    }

    public async Task<IEnumerable<string>> GetCompletionsAsync(string partialCommand)
    {
        var completions = new List<string>();

        if (string.IsNullOrWhiteSpace(partialCommand))
        {
            return completions;
        }

        try
        {
            var tokens = partialCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (tokens.Length == 1)
            {
                // Complete command names
                completions.AddRange(_commandRegistry.GetCommandNamesStartingWith(tokens[0]));
            }
            else if (tokens.Length > 1)
            {
                // Complete arguments for specific command
                var commandName = tokens[0];
                var command = _commandRegistry.GetCommand(commandName);
                
                if (command != null && _currentSession != null)
                {
                    var context = new CommandContext(this, _currentWorkingDirectory, _environmentVariables, _currentSession);
                    var args = tokens.Skip(1).ToArray();
                    var currentArg = args.LastOrDefault() ?? "";
                    
                    var commandCompletions = await command.GetCompletionsAsync(context, args, currentArg);
                    completions.AddRange(commandCompletions);
                }
            }

            return completions.Distinct().OrderBy(c => c);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completions for: {PartialCommand}", partialCommand);
            return completions;
        }
    }

    public void SetEnvironmentVariable(string name, string value)
    {
        _environmentVariables[name] = value;
    }

    public string? GetEnvironmentVariable(string name)
    {
        return _environmentVariables.TryGetValue(name, out var value) ? value : null;
    }

    public async Task<bool> ChangeDirectoryAsync(string path)
    {
        if (_currentSession == null)
        {
            return false;
        }

        try
        {
            var absolutePath = _fileSystem.GetAbsolutePath(path, _currentWorkingDirectory);
            
            if (await _fileSystem.DirectoryExistsAsync(absolutePath, _currentSession.User))
            {
                // Check permissions
                var node = await _fileSystem.GetNodeAsync(absolutePath, _currentSession.User);
                if (node != null && node.CanExecute(_currentSession.User))
                {
                    CurrentWorkingDirectory = absolutePath;
                    SetEnvironmentVariable("PWD", absolutePath);
                    return true;
                }
                else
                {
                    ErrorReceived?.Invoke(this, new ShellErrorEventArgs($"Permission denied: {absolutePath}"));
                    return false;
                }
            }
            else
            {
                ErrorReceived?.Invoke(this, new ShellErrorEventArgs($"Directory not found: {absolutePath}"));
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing directory to: {Path}", path);
            ErrorReceived?.Invoke(this, new ShellErrorEventArgs($"Failed to change directory: {ex.Message}", ex));
            return false;
        }
    }

    public async Task LoadHistoryAsync()
    {
        if (_currentSession == null)
        {
            return;
        }

        try
        {
            var historyPath = $"{_currentSession.User.HomeDirectory}/.bash_history";
              if (await _fileSystem.FileExistsAsync(historyPath, _currentSession.User))
            {
                var content = await _fileSystem.ReadFileAsync(historyPath, _currentSession.User);
                if (content != null)
                {
                    var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    
                    _commandHistory.Clear();
                    _commandHistory.AddRange(lines.Take(MaxHistorySize));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load command history for user {Username}", _currentSession.User.Username);
        }
    }

    public async Task SaveHistoryAsync()
    {
        if (_currentSession == null)
        {
            return;
        }

        try
        {
            var historyPath = $"{_currentSession.User.HomeDirectory}/.bash_history";
            var content = string.Join("\n", _commandHistory.TakeLast(MaxHistorySize));
            
            await _fileSystem.WriteFileAsync(historyPath, content, _currentSession.User);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save command history for user {Username}", _currentSession.User.Username);
        }
    }

    public void ClearSession()
    {
        _currentSession = null;
        _currentWorkingDirectory = "/";
        _commandHistory.Clear();
        _environmentVariables.Clear();
        InitializeDefaultEnvironment();
    }

    private void InitializeDefaultEnvironment()
    {
        _environmentVariables["PATH"] = "/bin:/usr/bin";
        _environmentVariables["HOME"] = "/";
        _environmentVariables["PWD"] = "/";
        _environmentVariables["SHELL"] = "/bin/bash";
        _environmentVariables["TERM"] = "xterm";
    }

    private void InitializeSessionEnvironment(UserSession session)
    {
        _environmentVariables["USER"] = session.User.Username;
        _environmentVariables["HOME"] = session.User.HomeDirectory;
        _environmentVariables["PWD"] = session.User.HomeDirectory;
        _environmentVariables["SHELL"] = session.User.Shell;
        _environmentVariables["LOGNAME"] = session.User.Username;
        _environmentVariables["UID"] = session.User.UserId.ToString();
        _environmentVariables["GID"] = session.User.PrimaryGroupId.ToString();
    }

    private void AddToHistory(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return;
        }

        // Don't add duplicate consecutive commands
        if (_commandHistory.Count > 0 && _commandHistory[^1] == commandLine)
        {
            return;
        }

        _commandHistory.Add(commandLine);

        // Limit history size
        if (_commandHistory.Count > MaxHistorySize)
        {
            _commandHistory.RemoveAt(0);
        }
    }

    private async Task<int> ExecutePipelineAsync(CommandPipeline pipeline, CancellationToken cancellationToken)
    {
        if (pipeline.Commands.Count == 1)
        {
            // Single command execution
            return await ExecuteSingleCommandAsync(pipeline.Commands[0], cancellationToken);
        }
        else
        {
            // Pipeline execution
            return await ExecuteCommandPipelineAsync(pipeline.Commands, cancellationToken);
        }
    }

    private async Task<int> ExecuteSingleCommandAsync(ParsedCommand parsedCommand, CancellationToken cancellationToken)
    {
        var command = _commandRegistry.GetCommand(parsedCommand.Command);
        if (command == null)
        {
            ErrorReceived?.Invoke(this, new ShellErrorEventArgs($"Command not found: {parsedCommand.Command}"));
            return 127; // Standard exit code for command not found
        }

        var context = new CommandContext(this, _currentWorkingDirectory, _environmentVariables, _currentSession!);

        // Set up streams based on redirections
        using var stdin = await CreateInputStreamAsync(parsedCommand, cancellationToken);
        using var stdout = await CreateOutputStreamAsync(parsedCommand, false, cancellationToken);
        using var stderr = await CreateOutputStreamAsync(parsedCommand, true, cancellationToken);

        return await command.ExecuteAsync(context, parsedCommand.Arguments, stdin, stdout, stderr, cancellationToken);
    }

    private async Task<int> ExecuteCommandPipelineAsync(IReadOnlyList<ParsedCommand> commands, CancellationToken cancellationToken)
    {
        var streams = new List<Stream>();
        var tasks = new List<Task<int>>();

        try
        {
            // Create pipe streams for connecting commands
            for (int i = 0; i < commands.Count - 1; i++)
            {
                var pipeStream = new MemoryStream();
                streams.Add(pipeStream);
            }

            // Execute all commands in the pipeline
            for (int i = 0; i < commands.Count; i++)
            {
                var parsedCommand = commands[i];
                var command = _commandRegistry.GetCommand(parsedCommand.Command);
                
                if (command == null)
                {
                    ErrorReceived?.Invoke(this, new ShellErrorEventArgs($"Command not found: {parsedCommand.Command}"));
                    return 127;
                }

                var context = new CommandContext(this, _currentWorkingDirectory, _environmentVariables, _currentSession!);

                // Set up streams for this command in the pipeline
                Stream stdin, stdout;
                
                if (i == 0)
                {
                    // First command: use input redirection or stdin
                    stdin = await CreateInputStreamAsync(parsedCommand, cancellationToken);
                }
                else
                {
                    // Middle/last command: read from previous pipe
                    var pipeStream = streams[i - 1];
                    pipeStream.Position = 0;
                    stdin = pipeStream;
                }

                if (i == commands.Count - 1)
                {
                    // Last command: use output redirection or stdout
                    stdout = await CreateOutputStreamAsync(parsedCommand, false, cancellationToken);
                }
                else
                {
                    // First/middle command: write to next pipe
                    stdout = streams[i];
                }

                using var stderr = await CreateOutputStreamAsync(parsedCommand, true, cancellationToken);

                var task = command.ExecuteAsync(context, parsedCommand.Arguments, stdin, stdout, stderr, cancellationToken);
                tasks.Add(task);
            }

            // Wait for all commands to complete
            var results = await Task.WhenAll(tasks);
            
            // Return the exit code of the last command
            return results.LastOrDefault();
        }
        finally
        {
            foreach (var stream in streams)
            {
                stream.Dispose();
            }
        }
    }

    private async Task<Stream> CreateInputStreamAsync(ParsedCommand parsedCommand, CancellationToken cancellationToken)
    {
        var inputRedirection = parsedCommand.GetInputRedirection();
        
        if (inputRedirection != null)
        {            try
            {
                var absolutePath = _fileSystem.GetAbsolutePath(inputRedirection.Target, _currentWorkingDirectory);
                var content = await _fileSystem.ReadFileAsync(absolutePath, _currentSession!.User);
                return new MemoryStream(Encoding.UTF8.GetBytes(content ?? ""));
            }
            catch (Exception ex)
            {
                ErrorReceived?.Invoke(this, new ShellErrorEventArgs($"Failed to read input file: {ex.Message}", ex));
                return new MemoryStream();
            }
        }

        return new MemoryStream(); // Empty input stream
    }

    private async Task<Stream> CreateOutputStreamAsync(ParsedCommand parsedCommand, bool isError, CancellationToken cancellationToken)
    {
        var redirection = isError ? parsedCommand.GetErrorRedirection() : parsedCommand.GetOutputRedirection();
        
        if (redirection != null)
        {
            try
            {
                var absolutePath = _fileSystem.GetAbsolutePath(redirection.Target, _currentWorkingDirectory);
                
                if (redirection.Type == RedirectionType.Append || redirection.Type == RedirectionType.ErrorAppend)
                {
                    // For append redirections, we need to read existing content first
                    var existingContent = "";                    if (await _fileSystem.FileExistsAsync(absolutePath, _currentSession!.User))
                    {
                        existingContent = await _fileSystem.ReadFileAsync(absolutePath, _currentSession.User) ?? "";
                    }
                    
                    return new FileRedirectionStream(absolutePath, existingContent, _fileSystem, _currentSession!.User);
                }
                else
                {
                    // For overwrite redirections
                    return new FileRedirectionStream(absolutePath, "", _fileSystem, _currentSession!.User);
                }
            }
            catch (Exception ex)
            {
                ErrorReceived?.Invoke(this, new ShellErrorEventArgs($"Failed to create output file: {ex.Message}", ex));
            }
        }

        // Default to a stream that forwards output to the shell's OutputReceived event
        return new ShellOutputRedirectionStream(this, isError);
    }

    /// <summary>
    /// Internal helper method to trigger output events from nested classes
    /// </summary>
    internal void OnOutputReceived(string content, bool isError = false)
    {
        OutputReceived?.Invoke(this, new ShellOutputEventArgs(content, isError));
    }

    /// <summary>
    /// Internal helper method to trigger error events from nested classes
    /// </summary>
    internal void OnErrorReceived(string message, Exception? exception = null)
    {
        ErrorReceived?.Invoke(this, new ShellErrorEventArgs(message, exception));
    }
}

/// <summary>
/// Stream that redirects output to a file in the virtual file system
/// </summary>
internal class FileRedirectionStream : Stream
{
    private readonly string _filePath;
    private readonly IVirtualFileSystem _fileSystem;
    private readonly User.User _user;
    private readonly MemoryStream _buffer;

    public FileRedirectionStream(string filePath, string initialContent, IVirtualFileSystem fileSystem, User.User user)
    {
        _filePath = filePath;
        _fileSystem = fileSystem;
        _user = user;
        _buffer = new MemoryStream(Encoding.UTF8.GetBytes(initialContent));
        _buffer.Position = _buffer.Length; // Position at end for append
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _buffer.Length;
    public override long Position 
    { 
        get => _buffer.Position; 
        set => _buffer.Position = value; 
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _buffer.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _buffer.Write(buffer, offset, count);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Save buffer content to file
            try
            {
                var content = Encoding.UTF8.GetString(_buffer.ToArray());
                Task.Run(async () => await _fileSystem.WriteFileAsync(_filePath, content, _user));
            }
            catch
            {
                // Ignore errors during disposal
            }
            
            _buffer.Dispose();
        }
        base.Dispose(disposing);
    }    public override void Flush() => _buffer.Flush();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}

/// <summary>
/// Stream that redirects output to the shell's output events
/// </summary>
internal class ShellOutputRedirectionStream : Stream
{
    private readonly Shell _shell;
    private readonly bool _isError;
    private readonly MemoryStream _buffer = new();

    public ShellOutputRedirectionStream(Shell shell, bool isError)
    {
        _shell = shell;
        _isError = isError;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _buffer.Length;
    public override long Position 
    { 
        get => _buffer.Position; 
        set => _buffer.Position = value; 
    }    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _buffer.WriteAsync(buffer, offset, count, cancellationToken);
        FlushToShell();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _buffer.Write(buffer, offset, count);
        FlushToShell();
    }

    private void FlushToShell()
    {
        if (_buffer.Length > 0)
        {
            var content = Encoding.UTF8.GetString(_buffer.ToArray());
            if (_isError)
            {
                _shell.OnErrorReceived(content);
            }
            else
            {
                _shell.OnOutputReceived(content);
            }
            _buffer.SetLength(0);
            _buffer.Position = 0;        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            FlushToShell();
            _buffer.Dispose();
        }
        base.Dispose(disposing);
    }

    public override void Flush() => FlushToShell();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
