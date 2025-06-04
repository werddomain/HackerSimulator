using HackerOs.OS.Shell.Commands;
using HackerOs.OS.Shell.Commands.Applications;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Shell;

/// <summary>
/// Interface for command initialization service
/// </summary>
public interface ICommandInitializer
{
    /// <summary>
    /// Initialize and register all shell commands
    /// </summary>
    Task InitializeCommandsAsync();
}

/// <summary>
/// Service that initializes and registers all shell commands
/// </summary>
public class CommandInitializer : ICommandInitializer
{
    private readonly ICommandRegistry _commandRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CommandInitializer> _logger;

    public CommandInitializer(
        ICommandRegistry commandRegistry,
        IServiceProvider serviceProvider,
        ILogger<CommandInitializer> logger)
    {
        _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }    public Task InitializeCommandsAsync()
    {
        try
        {
            _logger.LogInformation("Initializing shell commands...");

            // Get all command instances from DI container
            var commands = new List<ICommand>
            {
                // File system commands
                _serviceProvider.GetRequiredService<CatCommand>(),
                _serviceProvider.GetRequiredService<CdCommand>(),
                _serviceProvider.GetRequiredService<CpCommand>(),
                _serviceProvider.GetRequiredService<EchoCommand>(),
                _serviceProvider.GetRequiredService<FindCommand>(),
                _serviceProvider.GetRequiredService<GrepCommand>(),
                _serviceProvider.GetRequiredService<LsCommand>(),
                _serviceProvider.GetRequiredService<MkdirCommand>(),
                _serviceProvider.GetRequiredService<MvCommand>(),
                _serviceProvider.GetRequiredService<PwdCommand>(),
                _serviceProvider.GetRequiredService<RmCommand>(),
                _serviceProvider.GetRequiredService<TouchCommand>(),
                _serviceProvider.GetRequiredService<ShCommand>(),
                
                // Application management commands
                _serviceProvider.GetRequiredService<Commands.Applications.InstallCommand>(),
                _serviceProvider.GetRequiredService<Commands.Applications.UninstallCommand>(),
                _serviceProvider.GetRequiredService<Commands.Applications.ListAppsCommand>()
            };

            // Register all commands
            _commandRegistry.RegisterCommands(commands);

            _logger.LogInformation("Successfully registered {CommandCount} shell commands", commands.Count);
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize shell commands");
            throw;
        }
    }
}
