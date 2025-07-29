using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Interfaces;
using HackerOs.OS.Shell;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.Core
{
    /// <summary>
    /// Base class for all command-line applications in HackerOS.
    /// Commands are executed in a terminal and provide command-line functionality.
    /// </summary>
    public abstract class CommandBase : ApplicationCoreBase, ICommand
    {
        private readonly ILogger _logger;
        
        /// <summary>
        /// Gets the command name (how it's invoked in the terminal)
        /// </summary>
        public virtual string CommandName => Name.ToLowerInvariant();
        
        /// <summary>
        /// Gets the command syntax help
        /// </summary>
        public virtual string Syntax => $"{CommandName} [options]";
        
        /// <summary>
        /// Gets the command usage examples
        /// </summary>
        public virtual string[] Examples => new[] { $"{CommandName}" };
        
        /// <summary>
        /// Gets the command aliases
        /// </summary>
        public virtual string[] Aliases => Array.Empty<string>();
        
        /// <summary>
        /// Gets or sets the parsed command arguments
        /// </summary>
        protected CommandArguments Args { get; private set; }
        
        /// <summary>
        /// Gets the terminal that the command is running in
        /// </summary>
        protected ITerminal Terminal { get; private set; }
        
        /// <summary>
        /// Creates a new CommandBase
        /// </summary>
        /// <param name="logger">Logger for this command</param>
        protected CommandBase(ILogger logger)
        {
            _logger = logger;
            Type = ApplicationType.CommandLineApplication;
            Args = new CommandArguments();
        }
        
        /// <summary>
        /// Parses the command arguments
        /// </summary>
        /// <param name="args">The arguments to parse</param>
        /// <returns>True if parsing was successful</returns>
        protected virtual bool ParseArguments(string[] args)
        {
            // Default implementation with simple argument parsing
            Args = new CommandArguments(args);
            return true;
        }
        
        /// <summary>
        /// Shows the command help
        /// </summary>
        protected virtual Task ShowHelpAsync()
        {
            WriteLine($"Usage: {Syntax}");
            WriteLine();
            WriteLine($"Description: {Description}");
            WriteLine();
            
            if (Examples.Length > 0)
            {
                WriteLine("Examples:");
                foreach (var example in Examples)
                {
                    WriteLine($"  {example}");
                }
                WriteLine();
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Executes the command
        /// </summary>
        /// <param name="args">Command arguments</param>
        /// <returns>Exit code</returns>
        protected abstract Task<int> ExecuteCommandAsync(string[] args);
        
        #region Terminal I/O Helper Methods
        
        /// <summary>
        /// Writes a line to the terminal
        /// </summary>
        /// <param name="text">Text to write</param>
        protected Task WriteLine(string text = "")
        {
            return WriteOutputAsync(text + Environment.NewLine);
        }
        
        /// <summary>
        /// Writes text to the terminal
        /// </summary>
        /// <param name="text">Text to write</param>
        protected Task Write(string text)
        {
            return WriteOutputAsync(text);
        }
        
        /// <summary>
        /// Writes an error line to the terminal
        /// </summary>
        /// <param name="text">Error text to write</param>
        protected Task WriteErrorLine(string text)
        {
            return WriteErrorAsync(text + Environment.NewLine);
        }
        
        /// <summary>
        /// Writes an error to the terminal
        /// </summary>
        /// <param name="text">Error text to write</param>
        protected Task WriteError(string text)
        {
            return WriteErrorAsync(text);
        }
        
        /// <summary>
        /// Reads a line from the terminal
        /// </summary>
        /// <param name="prompt">Optional prompt to display</param>
        /// <returns>The line read from the terminal</returns>
        protected async Task<string> ReadLine(string prompt = null)
        {
            if (!string.IsNullOrEmpty(prompt))
            {
                await Write(prompt);
            }
            
            return await Terminal.ReadLineAsync();
        }
        
        /// <summary>
        /// Reads a key from the terminal
        /// </summary>
        /// <param name="prompt">Optional prompt to display</param>
        /// <returns>The key read from the terminal</returns>
        protected async Task<ConsoleKeyInfo> ReadKey(string prompt = null)
        {
            if (!string.IsNullOrEmpty(prompt))
            {
                await Write(prompt);
            }
            
            return await Terminal.ReadKeyAsync();
        }
        
        /// <summary>
        /// Writes output to the terminal and raises the OutputReceived event
        /// </summary>
        /// <param name="output">Output text</param>
        protected async Task WriteOutputAsync(string output)
        {
            await Terminal.WriteAsync(output);
            await RaiseOutputReceivedAsync(output);
        }
        
        /// <summary>
        /// Writes error to the terminal and raises the ErrorReceived event
        /// </summary>
        /// <param name="error">Error text</param>
        protected async Task WriteErrorAsync(string error)
        {
            await Terminal.WriteErrorAsync(error);
            await RaiseErrorReceivedAsync(error);
        }
        
        #endregion
        
        #region ICommand Implementation
        
        /// <inheritdoc />
        public async Task<int> ExecuteAsync(ITerminal terminal, string[] args)
        {
            try
            {
                // Store terminal reference
                Terminal = terminal;
                
                // Parse arguments
                if (!ParseArguments(args))
                {
                    await ShowHelpAsync();
                    return 1;
                }
                
                // Check for help flag
                if (Args.HasFlag("h") || Args.HasFlag("help"))
                {
                    await ShowHelpAsync();
                    return 0;
                }
                
                // Execute the command
                return await ExecuteCommandAsync(args);
            }
            catch (Exception ex)
            {
                await WriteErrorLine($"Error executing command {CommandName}: {ex.Message}");
                _logger.LogError(ex, "Error executing command {CommandName}", CommandName);
                await RaiseErrorReceivedAsync($"Command execution error: {ex.Message}", ex);
                return 1;
            }
        }
        
        #endregion
        
        #region ApplicationCoreBase Implementation
        
        /// <inheritdoc />
        protected override async Task<bool> OnStartAsync(ApplicationLaunchContext context)
        {
            try
            {
                // Commands don't have a persistent execution context
                // They start, execute, and terminate
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting command {CommandName}", CommandName);
                await RaiseErrorReceivedAsync($"Error starting command: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <inheritdoc />
        protected override Task<bool> OnStopAsync()
        {
            // Commands don't need special stop handling
            return Task.FromResult(true);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Helper class for parsing command arguments
    /// </summary>
    public class CommandArguments
    {
        private readonly List<string> _arguments = new();
        private readonly Dictionary<string, string> _options = new();
        private readonly HashSet<string> _flags = new();
        
        /// <summary>
        /// Creates a new empty CommandArguments
        /// </summary>
        public CommandArguments()
        {
        }
        
        /// <summary>
        /// Creates a new CommandArguments from an array of arguments
        /// </summary>
        /// <param name="args">The arguments to parse</param>
        public CommandArguments(string[] args)
        {
            Parse(args);
        }
        
        /// <summary>
        /// Parses the command arguments
        /// </summary>
        /// <param name="args">The arguments to parse</param>
        public void Parse(string[] args)
        {
            _arguments.Clear();
            _options.Clear();
            _flags.Clear();
            
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                
                if (arg.StartsWith("--"))
                {
                    // Long option
                    string option = arg.Substring(2);
                    if (option.Contains('='))
                    {
                        // Option with value
                        string[] parts = option.Split('=', 2);
                        _options[parts[0]] = parts[1];
                    }
                    else
                    {
                        // Flag
                        _flags.Add(option);
                    }
                }
                else if (arg.StartsWith("-"))
                {
                    // Short option
                    string option = arg.Substring(1);
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        // Option with value
                        _options[option] = args[++i];
                    }
                    else
                    {
                        // Flag
                        _flags.Add(option);
                    }
                }
                else
                {
                    // Regular argument
                    _arguments.Add(arg);
                }
            }
        }
        
        /// <summary>
        /// Gets a regular argument at the specified index
        /// </summary>
        /// <param name="index">The argument index</param>
        /// <returns>The argument value or null if not found</returns>
        public string GetArgument(int index)
        {
            return index < _arguments.Count ? _arguments[index] : null;
        }
        
        /// <summary>
        /// Gets all regular arguments
        /// </summary>
        /// <returns>Array of arguments</returns>
        public string[] GetArguments()
        {
            return _arguments.ToArray();
        }
        
        /// <summary>
        /// Gets an option value
        /// </summary>
        /// <param name="option">The option name</param>
        /// <returns>The option value or null if not found</returns>
        public string GetOption(string option)
        {
            return _options.TryGetValue(option, out string value) ? value : null;
        }
        
        /// <summary>
        /// Gets an option value with a default value
        /// </summary>
        /// <param name="option">The option name</param>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The option value or the default value if not found</returns>
        public string GetOption(string option, string defaultValue)
        {
            return _options.TryGetValue(option, out string value) ? value : defaultValue;
        }
        
        /// <summary>
        /// Gets an option value as an integer
        /// </summary>
        /// <param name="option">The option name</param>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The option value as an integer or the default value if not found or not a valid integer</returns>
        public int GetOptionInt(string option, int defaultValue = 0)
        {
            return _options.TryGetValue(option, out string value) && int.TryParse(value, out int intValue) 
                ? intValue 
                : defaultValue;
        }
        
        /// <summary>
        /// Checks if a flag is present
        /// </summary>
        /// <param name="flag">The flag name</param>
        /// <returns>True if the flag is present</returns>
        public bool HasFlag(string flag)
        {
            return _flags.Contains(flag);
        }
        
        /// <summary>
        /// Gets the number of regular arguments
        /// </summary>
        public int ArgumentCount => _arguments.Count;
        
        /// <summary>
        /// Gets all options
        /// </summary>
        public IReadOnlyDictionary<string, string> Options => _options;
        
        /// <summary>
        /// Gets all flags
        /// </summary>
        public IReadOnlyCollection<string> Flags => _flags;
    }
}
