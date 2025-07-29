using HackerOs.OS.Applications.Registry;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace HackerOs.OS.Applications.Launcher;

/// <summary>
/// Implementation of the application launcher service
/// </summary>
public class ApplicationLauncher : IApplicationLauncher
{
    private readonly ILogger<ApplicationLauncher> _logger;
    private readonly IApplicationRegistry _appRegistry;
    private readonly IApplicationManager _appManager;
    private readonly IUserManager _userManager;
    
    /// <summary>
    /// Create a new ApplicationLauncher
    /// </summary>
    public ApplicationLauncher(
        ILogger<ApplicationLauncher> logger,
        IApplicationRegistry appRegistry,
        IApplicationManager appManager,
        IUserManager userManager)
    {
        _logger = logger;
        _appRegistry = appRegistry;
        _appManager = appManager;
        _userManager = userManager;
    }
    
    /// <inheritdoc />
    public async Task<bool> LaunchApplicationAsync(string applicationId, UserSession? session = null, string[]? args = null)
    {
        try
        {
            // Get application metadata
            var metadata = _appRegistry.GetApplicationById(applicationId);
            if (metadata == null)
            {
                _logger.LogWarning("Application not found: {ApplicationId}", applicationId);
                return false;
            }
            
            return await LaunchApplicationAsync(metadata, session, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch application: {ApplicationId}", applicationId);
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> LaunchApplicationAsync(ApplicationMetadata metadata, UserSession? session = null, string[]? args = null)
    {
        try
        {
            // Get application manifest
            var manifest = _appManager.GetApplication(metadata.Id);
            if (manifest == null)
            {
                _logger.LogWarning("Application manifest not found: {ApplicationId}", metadata.Id);
                return false;
            }
            
            // Get user session
            var userSession = session ?? _userManager.GetCurrentSession();
            if (userSession == null)
            {
                _logger.LogWarning("No user session available for launching application: {ApplicationId}", metadata.Id);
                return false;
            }
            
            // Create launch context
            var context = new ApplicationLaunchContext
            {
                Session = userSession,
                Arguments = args ?? Array.Empty<string>()
            };
            
            // Launch the application
            var app = await _appManager.LaunchApplicationAsync(manifest, context);
            if (app == null)
            {
                _logger.LogWarning("Failed to launch application: {ApplicationId}", metadata.Id);
                return false;
            }
            
            // Record the launch
            await _appRegistry.RecordApplicationLaunchAsync(metadata.Id);
            
            _logger.LogInformation("Successfully launched application: {ApplicationId}", metadata.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch application: {ApplicationId}", metadata.Id);
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> CloseApplicationAsync(string applicationId, bool force = false)
    {
        try
        {
            // Check if application is running
            var app = _appManager.GetRunningApplication(applicationId);
            if (app == null)
            {
                _logger.LogWarning("Application not running: {ApplicationId}", applicationId);
                return false;
            }
            
            // Terminate the application
            var result = await _appManager.TerminateApplicationAsync(applicationId, force);
            
            _logger.LogInformation("Application {ApplicationId} {Result}", applicationId, result ? "closed successfully" : "failed to close");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close application: {ApplicationId}", applicationId);
            return false;
        }
    }
    
    /// <inheritdoc />
    public IEnumerable<string> GetRunningApplications()
    {
        var runningApps = _appManager.GetRunningApplications();
        
        return runningApps
            .Select(app => app.Manifest?.Id ?? string.Empty)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();
    }
    
    /// <inheritdoc />
    public bool IsApplicationRunning(string applicationId)
    {
        return _appManager.GetRunningApplication(applicationId) != null;
    }
}
