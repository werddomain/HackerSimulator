using HackerOs.OS.Security;
using HackerOs.OS.Core.State;
using HackerOs.OS.User;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using HackerOs.OS.HSystem;

namespace HackerOs;

/// <summary>
/// Startup class for initializing the application
/// </summary>
public static class Startup
{
    /// <summary>
    /// Initializes the application
    /// </summary>
    /// <param name="host">WebAssembly host</param>
    public static async Task InitializeAsync(WebAssemblyHost host)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Initializing HackerOS...");
        
        try 
        {
            // Get services from the service provider
            var mainService = host.Services.GetRequiredService<IMainService>();
            var appState = host.Services.GetRequiredService<IAppStateService>();
            var authService = host.Services.GetRequiredService<IAuthenticationService>();
            var userManager = host.Services.GetRequiredService<IUserManager>();
            var sessionManager = host.Services.GetRequiredService<ISessionManager>();
            
            // Initialize app state first (required for all other services)
            logger.LogInformation("Initializing application state...");
            await appState.InitializeAsync();
            
            // Set initial loading state
            appState.SetLoadingState(true);
            
            // Initialize user management (creates default users if needed)
            logger.LogInformation("Initializing user management system...");
            await userManager.InitializeAsync();
            
            // Initialize authentication service
            logger.LogInformation("Initializing authentication service...");
            await authService.InitializeAsync();
            
            // Check for existing sessions
            var activeSession = await sessionManager.GetActiveSessionAsync();
            if (activeSession != null && activeSession.State == SessionState.Active)
            {
                logger.LogInformation("Found active session for user: {Username}", activeSession.User?.Username);
                
                // Validate and restore session
                var token = activeSession.Token;
                var isValid = await authService.ValidateSessionAsync(token);
                
                if (isValid)
                {
                    // Session is valid, update app state
                    appState.SetAuthenticationState(true, activeSession.User?.Username);
                    logger.LogInformation("Session restored successfully");
                }
                else
                {
                    // Session is invalid, clear it
                    logger.LogWarning("Session token invalid or expired, clearing session");
                    await sessionManager.EndSessionAsync(activeSession.SessionId);
                    appState.SetAuthenticationState(false);
                }
            }
            else
            {
                // No active session, user needs to log in
                logger.LogInformation("No active session found, user will need to log in");
                appState.SetAuthenticationState(false);
            }
            
            // Initialize the system (only after authentication is checked)
            logger.LogInformation("Initializing system services...");
            await mainService.InitializeAsync();
            
            // Clear loading state when complete
            appState.SetLoadingState(false);
            
            logger.LogInformation("HackerOS initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during HackerOS initialization");
            
            // Get app state service to set error state
            var appState = host.Services.GetRequiredService<IAppStateService>();
            appState.ErrorMessage = "Failed to initialize HackerOS. Please refresh the page and try again.";
            appState.SetLoadingState(false);
        }
    }
}
