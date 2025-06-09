using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace HackerOs.OS.Applications;

/// <summary>
/// Service responsible for initializing the application system at startup
/// </summary>
public interface IApplicationSystemInitializer
{
    /// <summary>
    /// Initializes the application system
    /// </summary>
    Task InitializeAsync();
}

/// <summary>
/// Implementation of the application system initializer
/// </summary>
public class ApplicationSystemInitializer : IApplicationSystemInitializer
{
    private readonly ILogger<ApplicationSystemInitializer> _logger;
    private readonly IApplicationManager _applicationManager;
    private readonly IFileTypeRegistry _fileTypeRegistry;
    private readonly IApplicationDiscoveryService _applicationDiscoveryService;
    private readonly IVirtualFileSystem _fileSystem;
    private readonly IServiceProvider _serviceProvider;
    
    /// <summary>
    /// Creates a new instance of the ApplicationSystemInitializer
    /// </summary>
    public ApplicationSystemInitializer(
        ILogger<ApplicationSystemInitializer> logger,
        IApplicationManager applicationManager,
        IFileTypeRegistry fileTypeRegistry,
        IApplicationDiscoveryService applicationDiscoveryService,
        IVirtualFileSystem fileSystem,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _applicationManager = applicationManager;
        _fileTypeRegistry = fileTypeRegistry;
        _applicationDiscoveryService = applicationDiscoveryService;
        _fileSystem = fileSystem;
        _serviceProvider = serviceProvider;
    }
    
    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing application system");
        
        // Ensure application directory structure exists
        await EnsureDirectoryStructureAsync();
        
        // Load file type registry from filesystem if it exists
        await _fileTypeRegistry.LoadRegistryAsync();
        
        // Discover and register applications from attributes
        int appCount = await _applicationDiscoveryService.DiscoverApplicationsAsync();
        _logger.LogInformation("Discovered {Count} applications", appCount);
        
        // Initialize any startup applications
        await InitializeStartupApplicationsAsync();
        
        _logger.LogInformation("Application system initialization complete");
    }
    
    /// <summary>
    /// Ensures the application directory structure exists
    /// </summary>
    private async Task EnsureDirectoryStructureAsync()
    {
        // Create standard application directories
        string[] directories = new[]
        {
            "/usr/share/applications",  // Application manifests
            "/usr/bin",                 // System applications
            "/opt",                     // Optional/third-party applications
            "/etc/applications.d"       // Application configuration
        };
        
        foreach (var dir in directories)
        {
            if (!await _fileSystem.DirectoryExistsAsync(dir, UserManager.SystemUser))
            {
                await _fileSystem.CreateDirectoryAsync(dir, UserManager.SystemUser);
                _logger.LogInformation("Created application directory: {Dir}", dir);
            }
        }
    }
    
    /// <summary>
    /// Launches applications that are configured to start automatically
    /// </summary>
    private async Task InitializeStartupApplicationsAsync()
    {
        // Get applications marked for auto-start
        var autoStartApps = _applicationManager.GetAvailableApplications()
            .Where(app => app.AutoStart)
            .ToList();
            
        _logger.LogInformation("Found {Count} applications configured for auto-start", autoStartApps.Count);
        
        // Launch each auto-start application
        foreach (var app in autoStartApps)
        {
            _logger.LogInformation("Auto-starting application: {AppId}", app.Id);
            
            try
            {
                // Note: System applications are started with special system session
                // For now, we'll skip actually launching them as user sessions aren't initialized yet
                // This would be handled in the full system initialization sequence
                
                _logger.LogInformation("Application {AppId} marked for auto-start", app.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-start application {AppId}", app.Id);
            }
        }
    }
}
