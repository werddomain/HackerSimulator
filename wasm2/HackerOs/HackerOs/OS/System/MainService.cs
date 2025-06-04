using HackerOs.OS.System;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace HackerOs.OS.System;

/// <summary>
/// Main service that runs during application startup
/// </summary>
public interface IMainService
{
    /// <summary>
    /// Initializes the system during application startup
    /// </summary>
    Task InitializeAsync();
    
    /// <summary>
    /// Cleans up resources when the application is shutting down
    /// </summary>
    Task CleanupAsync();
}

/// <summary>
/// Default implementation of the main service
/// </summary>
public class MainService : IMainService
{
    private readonly ILogger<MainService> _logger;
    private readonly ISystemBootService _systemBootService;
    
    /// <summary>
    /// Creates a new instance of the MainService
    /// </summary>
    public MainService(ILogger<MainService> logger, ISystemBootService systemBootService)
    {
        _logger = logger;
        _systemBootService = systemBootService;
    }
    
    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _logger.LogInformation("MainService initializing");
        
        try
        {
            // Boot the system
            await _systemBootService.BootAsync();
            _logger.LogInformation("System initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during system initialization");
        }
    }
    
    /// <inheritdoc />
    public async Task CleanupAsync()
    {
        _logger.LogInformation("MainService cleaning up resources");
        
        try
        {
            // Shutdown the system
            await _systemBootService.ShutdownAsync();
            _logger.LogInformation("System cleanup completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during system cleanup");
        }
    }
}
