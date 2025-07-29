# Command Application Migration Template

**🚨 IMPORTANT: ONLY WORK IN THIS DIRECTORY: `wasm2\HackerOs`** 
**🚨 REFER TO `worksheet.md` FOR PROJECT GUIDELINES**

## Overview

This document provides a step-by-step template for migrating existing command applications to the new unified architecture. The template includes migration steps, code examples, and testing guidelines to ensure consistent implementation.

## Migration Checklist

### Pre-Migration Assessment
- [ ] Review the command's current implementation
- [ ] Identify dependencies and integration points
- [ ] Document current functionality and behavior
- [ ] Verify command complexity against migration inventory

### Code Migration Steps
- [ ] Update file structure (if needed)
- [ ] Convert to CommandBase inheritance
- [ ] Update dependency injection and service usage
- [ ] Implement argument parsing
- [ ] Add lifecycle method implementations
- [ ] Implement terminal integration
- [ ] Add error handling and help text
- [ ] Update output formatting

### Testing Steps
- [ ] Verify command registration
- [ ] Test command execution with various arguments
- [ ] Verify command functionality matches original
- [ ] Test integration with other components
- [ ] Verify process lifecycle (start, stop)
- [ ] Test error scenarios
- [ ] Verify help text and documentation

## Directory Structure

Migrated command applications should follow this directory structure:

```
HackerOs/
└── OS/
    └── Applications/
        └── Commands/
            └── {Category}/
                └── {CommandName}Command.cs
```

Where `{Category}` is a functional category like `FileSystem`, `Network`, `System`, etc.

## Code Template

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Core;
using HackerOs.OS.Core;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.Commands.{Category}
{
    /// <summary>
    /// {CommandName}Command provides functionality to [brief description of command].
    /// </summary>
    public class {CommandName}Command : CommandBase
    {
        private readonly ILogger<{CommandName}Command> _logger;
        // Add other required service dependencies
        
        /// <summary>
        /// Initializes a new instance of the <see cref="{CommandName}Command"/> class.
        /// </summary>
        /// <param name="applicationBridge">The application bridge for process and event management.</param>
        /// <param name="logger">The logger for command logging.</param>
        public {CommandName}Command(
            IApplicationBridge applicationBridge,
            ILogger<{CommandName}Command> logger
            /* Add other required services */)
            : base(applicationBridge)
        {
            _logger = logger;
            // Initialize other dependencies
            
            // Set application information
            Name = "{command-name}";  // Command name in lowercase
            Description = "Provides functionality to [brief description of command]";
            ApplicationType = ApplicationType.Command;
            
            // Set command-specific properties
            Usage = "{command-name} [options] [arguments]";
            HelpText = @"
Description:
  {CommandName} - [Brief description of the command]

Usage:
  {command-name} [options] [arguments]

Options:
  -h, --help       Show help information
  -v, --verbose    Enable verbose output
  [Command-specific options]

Arguments:
  [Command-specific arguments]

Examples:
  {command-name} example1      # Example 1 description
  {command-name} -v example2   # Example 2 description with verbose output
";
        }
        
        /// <summary>
        /// Executes the command with the provided arguments.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        public override async Task ExecuteAsync(string[] args)
        {
            try
            {
                _logger.LogInformation($"Executing {Name} command with {args.Length} arguments");
                
                // Show help if requested or no arguments provided
                if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
                {
                    await WriteLineAsync(HelpText);
                    await CompleteAsync(0);
                    return;
                }
                
                // Parse arguments
                bool verbose = args.Contains("-v") || args.Contains("--verbose");
                
                // Remove options to get remaining arguments
                var remainingArgs = args.Where(arg => !arg.StartsWith("-")).ToArray();
                
                // Parse command-specific arguments
                if (remainingArgs.Length < 1)
                {
                    await WriteLineAsync($"Error: Missing required arguments");
                    await WriteLineAsync($"Use '{Name} --help' for usage information");
                    await CompleteAsync(1);
                    return;
                }
                
                // Command implementation
                int exitCode = await ProcessCommandAsync(remainingArgs, verbose);
                
                // Complete the command with appropriate exit code
                await CompleteAsync(exitCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing {Name} command");
                await WriteLineAsync($"Error: {ex.Message}");
                
                if (ex is ArgumentException || ex is FormatException)
                {
                    await WriteLineAsync($"Use '{Name} --help' for usage information");
                }
                
                await CompleteAsync(1);
            }
        }
        
        /// <summary>
        /// Processes the command with the parsed arguments.
        /// </summary>
        /// <param name="args">The parsed arguments.</param>
        /// <param name="verbose">Whether verbose output is enabled.</param>
        /// <returns>Exit code for the command (0 for success).</returns>
        private async Task<int> ProcessCommandAsync(string[] args, bool verbose)
        {
            // Command-specific implementation
            string arg1 = args[0];
            
            if (verbose)
            {
                await WriteLineAsync($"Processing '{arg1}'...");
            }
            
            // Implement command functionality
            // ...
            
            await WriteLineAsync($"Command executed successfully.");
            return 0; // Success
        }
        
        /// <summary>
        /// Starts the command process.
        /// </summary>
        public override async Task StartAsync()
        {
            try
            {
                _logger.LogInformation($"Starting {Name} command...");
                
                // Command-specific initialization
                
                await base.StartAsync();
                
                _logger.LogInformation($"{Name} command started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting {Name} command");
                await RaiseErrorAsync($"Error starting {Name}: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Stops the command process.
        /// </summary>
        public override async Task StopAsync()
        {
            try
            {
                _logger.LogInformation($"Stopping {Name} command...");
                
                // Command-specific cleanup
                
                await base.StopAsync();
                
                _logger.LogInformation($"{Name} command stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error stopping {Name} command");
                await RaiseErrorAsync($"Error stopping {Name}: {ex.Message}");
                throw;
            }
        }
        
        // Command-specific methods
    }
}
```

## Advanced Argument Parsing

For commands with more complex argument parsing needs:

```csharp
private class CommandOptions
{
    public bool Verbose { get; set; }
    public bool Recursive { get; set; }
    public bool Force { get; set; }
    public string OutputFormat { get; set; } = "text";
    public string[] Targets { get; set; } = Array.Empty<string>();
}

private CommandOptions ParseOptions(string[] args)
{
    var options = new CommandOptions();
    var targets = new List<string>();
    
    for (int i = 0; i < args.Length; i++)
    {
        string arg = args[i];
        
        if (arg.StartsWith("--"))
        {
            // Handle long options
            switch (arg)
            {
                case "--help":
                    throw new ArgumentException("Help requested");
                case "--verbose":
                    options.Verbose = true;
                    break;
                case "--recursive":
                    options.Recursive = true;
                    break;
                case "--force":
                    options.Force = true;
                    break;
                case "--format":
                    if (i + 1 < args.Length)
                    {
                        options.OutputFormat = args[++i];
                    }
                    else
                    {
                        throw new ArgumentException("Missing value for --format option");
                    }
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {arg}");
            }
        }
        else if (arg.StartsWith("-") && arg.Length > 1)
        {
            // Handle short options (can be combined like -rf)
            for (int j = 1; j < arg.Length; j++)
            {
                switch (arg[j])
                {
                    case 'h':
                        throw new ArgumentException("Help requested");
                    case 'v':
                        options.Verbose = true;
                        break;
                    case 'r':
                        options.Recursive = true;
                        break;
                    case 'f':
                        options.Force = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option: -{arg[j]}");
                }
            }
        }
        else
        {
            // Non-option argument (target)
            targets.Add(arg);
        }
    }
    
    options.Targets = targets.ToArray();
    return options;
}
```

## Handling Output Formats

For commands that support different output formats (text, json, etc.):

```csharp
private async Task OutputResultsAsync(CommandResult result, string format)
{
    switch (format.ToLowerInvariant())
    {
        case "json":
            await WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));
            break;
            
        case "csv":
            // Output CSV format
            await WriteLineAsync($"Name,Size,Modified");
            foreach (var item in result.Items)
            {
                await WriteLineAsync($"{item.Name},{item.Size},{item.Modified}");
            }
            break;
            
        case "text":
        default:
            // Output plain text format
            foreach (var item in result.Items)
            {
                await WriteLineAsync($"{item.Name,-20} {item.Size,10} {item.Modified}");
            }
            break;
    }
}
```

## Progress Reporting

For long-running commands that need to show progress:

```csharp
private async Task ProcessLargeOperationAsync(string[] targets, bool verbose)
{
    int total = targets.Length;
    int completed = 0;
    
    foreach (var target in targets)
    {
        if (verbose)
        {
            await WriteLineAsync($"Processing {completed + 1}/{total}: {target}");
        }
        else
        {
            // Simple progress indicator
            await WriteAsync($"\rProgress: {completed * 100 / total}% complete");
        }
        
        // Process the item
        await ProcessItemAsync(target);
        
        completed++;
    }
    
    // Clear the progress line
    if (!verbose)
    {
        await WriteLineAsync($"\rProgress: 100% complete");
    }
}
```

## Common Migration Patterns

### File System Operations

For commands that work with the file system:

```csharp
private async Task<bool> ValidatePathAsync(string path, bool shouldExist = true)
{
    try
    {
        // Normalize path (handle ~, ., .. etc.)
        path = _fileSystem.NormalizePath(path);
        
        if (shouldExist)
        {
            bool exists = await _fileSystem.FileExistsAsync(path) || 
                          await _fileSystem.DirectoryExistsAsync(path);
            
            if (!exists)
            {
                await WriteLineAsync($"Error: No such file or directory: {path}");
                return false;
            }
        }
        
        // Check permissions if needed
        // ...
        
        return true;
    }
    catch (Exception ex)
    {
        await WriteLineAsync($"Error validating path: {ex.Message}");
        return false;
    }
}
```

### Interactive Mode

For commands that can be interactive:

```csharp
private async Task RunInteractiveModeAsync()
{
    await WriteLineAsync($"Entering interactive {Name} mode. Type 'exit' to quit.");
    
    while (true)
    {
        await WriteAsync($"{Name}> ");
        string input = await ReadLineAsync();
        
        if (string.IsNullOrWhiteSpace(input))
        {
            continue;
        }
        
        if (input.ToLowerInvariant() == "exit" || input.ToLowerInvariant() == "quit")
        {
            break;
        }
        
        // Parse the input as arguments
        string[] args = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        try
        {
            // Process the command
            await ProcessCommandAsync(args, false);
        }
        catch (Exception ex)
        {
            await WriteLineAsync($"Error: {ex.Message}");
        }
    }
    
    await WriteLineAsync("Exiting interactive mode.");
}
```

## Troubleshooting Common Issues

### Command Not Found

Check:
- Verify command registration in the application registry
- Check command name case sensitivity
- Ensure the command is properly initialized

### Argument Parsing Issues

Check:
- Validate argument parsing logic
- Test with quoted arguments
- Handle edge cases (empty arguments, special characters)

### Output Formatting Issues

Check:
- Verify terminal escape sequences
- Test with different output widths
- Handle special characters in output

## Examples

For complete examples of migrated command applications, refer to:

1. `ListCommand.cs` - Directory listing command example
2. `CdCommand.cs` - Directory navigation command example (after migration)

## Testing Guidelines

1. **Unit Testing**
   - Test argument parsing logic
   - Test command execution with various inputs
   - Mock dependencies for controlled testing

2. **Integration Testing**
   - Test interaction with file system or other services
   - Verify command registration and discovery
   - Test command execution from terminal

3. **Error Handling Testing**
   - Test invalid arguments
   - Test permission issues
   - Test resource not found scenarios

4. **Documentation Testing**
   - Verify help text is accurate and comprehensive
   - Test examples from help text
   - Ensure all options are documented

## Final Validation Checklist

- [ ] Command is properly registered and discoverable
- [ ] Command executes with expected output
- [ ] All arguments and options work as expected
- [ ] Error handling is robust and user-friendly
- [ ] Help text is comprehensive and accurate
- [ ] Command follows the new architecture guidelines
- [ ] Performance is equivalent or better than original implementation
