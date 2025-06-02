using HackerOs.OS.Applications;
using HackerOs.OS.Shell;
using HackerOs.OS.User;
using System.Text;
using System.Text.Json;

namespace HackerOs.OS.Applications.BuiltIn
{
    /// <summary>
    /// Terminal emulator application that provides command-line interface
    /// </summary>
    public class TerminalEmulator : ApplicationBase
    {
        private readonly IShell _shell;
        private readonly StringBuilder _outputBuffer;
        private readonly List<string> _commandHistory;
        private int _historyIndex;
        private string _currentInput;
        private readonly object _lockObject = new object();
        
        public TerminalEmulator(IShell shell) : base()
        {
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _outputBuffer = new StringBuilder();
            _commandHistory = new List<string>();
            _historyIndex = -1;
            _currentInput = string.Empty;
        }

        protected override async Task<bool> OnStartAsync(ApplicationLaunchContext context)
        {
            try
            {
                // Subscribe to shell output events
                _shell.OutputReceived += OnShellOutput;
                _shell.ErrorReceived += OnShellError;
                
                // Clear terminal and show welcome message
                _outputBuffer.Clear();
                await WriteWelcomeMessage(context.UserSession);
                
                // Show initial prompt
                await ShowPrompt();
                
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Failed to start terminal: {ex.Message}");
                return false;
            }
        }        protected override async Task<bool> OnStopAsync()
        {
            try
            {
                // Unsubscribe from shell events
                if (_shell != null)
                {
                    _shell.OutputReceived -= OnShellOutput;
                    _shell.ErrorReceived -= OnShellError;
                }
                
                await WriteLineAsync("Terminal session ended.");
                return true;
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Error stopping terminal: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Process user input (keyboard events)
        /// </summary>
        public async Task ProcessInputAsync(string input, ConsoleKey key = ConsoleKey.Enter)
        {
            try
            {
                switch (key)
                {
                    case ConsoleKey.Enter:
                        await ProcessCommandAsync(input);
                        break;
                        
                    case ConsoleKey.UpArrow:
                        await NavigateHistory(-1);
                        break;
                        
                    case ConsoleKey.DownArrow:
                        await NavigateHistory(1);
                        break;
                        
                    case ConsoleKey.Tab:
                        await ProcessTabCompletionAsync(input);
                        break;
                        
                    default:
                        _currentInput = input;
                        break;
                }
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Error processing input: {ex.Message}");
            }
        }

        /// <summary>
        /// Get current terminal output as string
        /// </summary>
        public string GetOutput()
        {
            lock (_lockObject)
            {
                return _outputBuffer.ToString();
            }
        }

        /// <summary>
        /// Clear terminal output
        /// </summary>
        public async Task ClearAsync()
        {
            lock (_lockObject)
            {
                _outputBuffer.Clear();
            }
            
            await ShowPrompt();
            await OnOutputAsync("Terminal cleared.");
        }

        private async Task ProcessCommandAsync(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                await ShowPrompt();
                return;
            }

            // Add to history
            if (!string.IsNullOrWhiteSpace(command) && 
                (_commandHistory.Count == 0 || _commandHistory.Last() != command))
            {
                _commandHistory.Add(command);
            }
            _historyIndex = -1;
            _currentInput = string.Empty;

            // Echo command
            await WriteLineAsync($"{GetPromptPrefix()}{command}");

            // Handle built-in terminal commands
            if (await ProcessBuiltInCommandAsync(command))
            {
                await ShowPrompt();
                return;
            }

            try
            {
                // Execute command through shell
                int exitCode = await _shell.ExecuteCommandAsync(command, CancellationToken.None);
                
                if (exitCode != 0)
                {
                    await WriteLineAsync($"Command exited with code: {exitCode}");
                }
            }
            catch (Exception ex)
            {
                await WriteLineAsync($"Command execution failed: {ex.Message}");
            }

            await ShowPrompt();
        }

        private async Task<bool> ProcessBuiltInCommandAsync(string command)
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            switch (parts[0].ToLower())
            {
                case "clear":
                case "cls":
                    await ClearAsync();
                    return true;
                    
                case "exit":
                case "quit":
                    await StopAsync();
                    return true;
                    
                case "history":
                    await ShowHistoryAsync();
                    return true;
                    
                case "help":
                    await ShowHelpAsync();
                    return true;
                    
                default:
                    return false;
            }
        }

        private async Task NavigateHistory(int direction)
        {
            if (_commandHistory.Count == 0) return;

            if (direction < 0) // Up arrow
            {
                if (_historyIndex == -1)
                {
                    _historyIndex = _commandHistory.Count - 1;
                }
                else if (_historyIndex > 0)
                {
                    _historyIndex--;
                }
            }
            else // Down arrow
            {
                if (_historyIndex == -1) return;
                
                if (_historyIndex < _commandHistory.Count - 1)
                {
                    _historyIndex++;
                }
                else
                {
                    _historyIndex = -1;
                    _currentInput = string.Empty;
                    await OnOutputAsync("Input cleared.");
                    return;
                }
            }

            if (_historyIndex >= 0 && _historyIndex < _commandHistory.Count)
            {
                _currentInput = _commandHistory[_historyIndex];
                await OnOutputAsync($"History: {_currentInput}");
            }
        }

        private async Task ProcessTabCompletionAsync(string input)
        {
            try
            {
                // Simple tab completion for commands and files
                var completions = await _shell.GetCompletionsAsync(input);
                var completionList = completions.ToList();
                
                if (completionList.Count == 1)
                {
                    _currentInput = completionList[0];
                    await OnOutputAsync($"Completed: {_currentInput}");
                }
                else if (completionList.Count > 1)
                {
                    await WriteLineAsync("Completions:");
                    foreach (var completion in completionList)
                    {
                        await WriteLineAsync($"  {completion}");
                    }
                    await ShowPrompt();
                }
            }
            catch (Exception ex)
            {
                await OnErrorAsync($"Tab completion failed: {ex.Message}");
            }
        }

        private async Task ShowHistoryAsync()
        {
            if (_commandHistory.Count == 0)
            {
                await WriteLineAsync("No command history.");
                return;
            }

            await WriteLineAsync("Command History:");
            for (int i = 0; i < _commandHistory.Count; i++)
            {
                await WriteLineAsync($"  {i + 1}: {_commandHistory[i]}");
            }
        }

        private async Task ShowHelpAsync()
        {
            await WriteLineAsync("Terminal Emulator Help:");
            await WriteLineAsync("  clear/cls  - Clear terminal screen");
            await WriteLineAsync("  exit/quit  - Exit terminal");
            await WriteLineAsync("  history    - Show command history");
            await WriteLineAsync("  help       - Show this help");
            await WriteLineAsync("");
            await WriteLineAsync("Navigation:");
            await WriteLineAsync("  Up/Down arrows - Navigate command history");
            await WriteLineAsync("  Tab            - Auto-complete commands/paths");
            await WriteLineAsync("");
            await WriteLineAsync("All other commands are passed to the shell.");
        }

        private async Task WriteWelcomeMessage(UserSession userSession)
        {
            var user = userSession?.User;
            var hostname = "hackeros";
            
            await WriteLineAsync("====================================");
            await WriteLineAsync("     HackerOS Terminal Emulator");
            await WriteLineAsync("====================================");
            await WriteLineAsync($"Welcome, {user?.Username ?? "user"}!");
            await WriteLineAsync($"Session: {userSession?.SessionId ?? "unknown"}");
            await WriteLineAsync($"Host: {hostname}");
            await WriteLineAsync($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await WriteLineAsync("");
            await WriteLineAsync("Type 'help' for terminal commands.");
            await WriteLineAsync("");
        }

        private async Task ShowPrompt()
        {
            var prompt = GetPromptPrefix();
            await WriteAsync(prompt);
        }

        private string GetPromptPrefix()
        {
            var user = Context?.UserSession?.User;
            var username = user?.Username ?? "user";
            var hostname = "hackeros";
            var workingDir = _shell?.GetWorkingDirectory() ?? "~";
            
            // Simplify long paths
            if (workingDir.Length > 20)
            {
                workingDir = "..." + workingDir.Substring(workingDir.Length - 17);
            }
            
            var promptChar = user?.UserId == 0 ? "#" : "$"; // Root vs regular user
            
            return $"[{username}@{hostname} {workingDir}]{promptChar} ";
        }

        private async Task WriteLineAsync(string text)
        {
            await WriteAsync(text + Environment.NewLine);
        }

        private async Task WriteAsync(string text)
        {
            lock (_lockObject)
            {
                _outputBuffer.Append(text);
            }
            
            await OnOutputAsync(text);
        }        private void OnShellOutput(object? sender, ShellOutputEventArgs e)
        {
            Task.Run(async () => await WriteAsync(e.Output));
        }

        private void OnShellError(object? sender, ShellErrorEventArgs e)
        {
            Task.Run(async () => await WriteLineAsync($"Shell Error: {e.Error}"));
        }
    }
}
