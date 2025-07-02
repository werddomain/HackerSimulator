using HackerOs.OS.System;
using HackerOs.OS.Security;
using HackerOs.OS.User;
using HackerOs.OS.Core.State;
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
    private readonly IAuthenticationService _authService;
    private readonly HackerOs.OS.Security.ISessionManager _sessionManager;
    private readonly IUserManager _userManager;
    private readonly IAppStateService _appState;
    private bool _isInitialized = false;
    
    /// <summary>
    /// Creates a new instance of the MainService
    /// </summary>
    public MainService(
        ILogger<MainService> logger, 
        ISystemBootService systemBootService,
        IAuthenticationService authService,
        HackerOs.OS.Security.ISessionManager sessionManager,
        IUserManager userManager,
        IAppStateService appState)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemBootService = systemBootService ?? throw new ArgumentNullException(nameof(systemBootService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _appState = appState ?? throw new ArgumentNullException(nameof(appState));
        
        // Subscribe to authentication state changes
        _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }
    
    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            _logger.LogWarning("MainService already initialized, skipping initialization");
            return;
        }
        
        _logger.LogInformation("MainService initializing");
        
        try
        {
            // Check if there's an active authenticated session
            var isAuthenticated = await _authService.IsAuthenticatedAsync();
            
            if (isAuthenticated)
            {
                _logger.LogInformation("Initializing system with authenticated user");
                
                // Get the active session
                var activeSession = await _sessionManager.GetActiveSessionAsync();
                if (activeSession != null)
                {
                    _logger.LogInformation("Active session found for user {Username}", activeSession.User?.Username);
                    
                    // Initialize user-specific environment
                    await InitializeUserEnvironmentAsync(activeSession);
                }
            }
            else
            {
                _logger.LogInformation("Initializing system without authenticated user");
            }
            
            // Boot the system
            await _systemBootService.BootAsync();
            _isInitialized = true;
            _logger.LogInformation("System initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during system initialization");
            _appState.ErrorMessage = "System initialization failed. Please refresh and try again.";
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
            _isInitialized = false;
            _logger.LogInformation("System cleanup completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during system cleanup");
        }
    }
    
    /// <summary>
    /// Initializes the user environment for the active session.
    /// </summary>
    /// <param name="session">The active user session.</param>
    private async Task InitializeUserEnvironmentAsync(IUserSession session)
    {
        if (session?.User == null)
            return;
            
        try
        {
            _logger.LogInformation("Initializing environment for user {Username}", session.User.Username);
            
            // Ensure user home directory exists
            var homePath = $"/home/{session.User.Username}";
            
            // Additional user environment initialization can be added here
            await Task.Delay(1); // Ensure method is properly async
            
            _logger.LogInformation("User environment initialized for {Username}", session.User.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing user environment for {Username}", session.User?.Username);
        }
    }
    
    /// <summary>
    /// Handles authentication state changes.
    /// </summary>
    private void OnAuthenticationStateChanged(object? sender, AuthenticationStateChangedEventArgs e)
    {
        _logger.LogInformation("Authentication state changed. Authenticated: {IsAuthenticated}, Username: {Username}", 
            e.IsAuthenticated, e.Username ?? "none");
            
        // If a user has logged in and the system is already initialized,
        // initialize their environment
        if (e.IsAuthenticated && _isInitialized && !string.IsNullOrEmpty(e.Username))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var activeSession = await _sessionManager.GetActiveSessionAsync();
                    if (activeSession != null)
                    {
                        await InitializeUserEnvironmentAsync(activeSession);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling authentication state change");
                }
            });
        }
    }
}
