using HackerOs.OS.Applications.Registry;
using HackerOs.OS.IO;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.Lifecycle;

/// <summary>
/// Interface for application lifecycle hooks
/// </summary>
public interface IApplicationLifecycle
{
    /// <summary>
    /// Called when the application is first started
    /// </summary>
    Task OnStartAsync(ApplicationLifecycleContext context);
    
    /// <summary>
    /// Called when the application gains focus
    /// </summary>
    Task OnActivateAsync(ApplicationLifecycleContext context);
    
    /// <summary>
    /// Called when the application loses focus
    /// </summary>
    Task OnDeactivateAsync(ApplicationLifecycleContext context);
    
    /// <summary>
    /// Called when the application is about to close
    /// </summary>
    /// <returns>True if the application can close, false to cancel closure</returns>
    Task<bool> OnCloseRequestAsync(ApplicationLifecycleContext context);
    
    /// <summary>
    /// Called when the application is closing
    /// </summary>
    Task OnCloseAsync(ApplicationLifecycleContext context);
    
    /// <summary>
    /// Called to save the application's state
    /// </summary>
    Task SaveStateAsync(ApplicationLifecycleContext context);
    
    /// <summary>
    /// Called to load the application's state
    /// </summary>
    Task LoadStateAsync(ApplicationLifecycleContext context);
}

/// <summary>
/// Context information for application lifecycle events
/// </summary>
public class ApplicationLifecycleContext
{
    /// <summary>
    /// Application metadata
    /// </summary>
    public required ApplicationMetadata Metadata { get; init; }
    
    /// <summary>
    /// Current user session
    /// </summary>
    public required UserSession Session { get; init; }
    
    /// <summary>
    /// User who launched the application
    /// </summary>
    public required User.User User { get; init; }
    
    /// <summary>
    /// Launch arguments
    /// </summary>
    public string[] Arguments { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Logger for the application
    /// </summary>
    public ILogger Logger { get; init; } = null!;
    
    /// <summary>
    /// File system for the application
    /// </summary>
    public HackerOs.OS.IO.IVirtualFileSystem FileSystem { get; init; } = null!;
    
    /// <summary>
    /// State dictionary that can be used to pass state between lifecycle methods
    /// </summary>
    public Dictionary<string, object> State { get; } = new();
}
