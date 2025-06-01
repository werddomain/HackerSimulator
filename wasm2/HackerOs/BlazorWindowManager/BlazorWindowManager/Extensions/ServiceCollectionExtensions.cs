using BlazorWindowManager.Models;
using BlazorWindowManager.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorWindowManager.Extensions;

/// <summary>
/// Extension methods for registering Blazor Window Manager services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Blazor Window Manager services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBlazorWindowManager(this IServiceCollection services)
    {
        // Register the core window manager service as singleton
        services.AddSingleton<WindowManagerService>();
        
        // Register the snapping service as singleton
        services.AddSingleton<SnappingService>();
        
        // Register the dialog service as scoped (for component lifecycle)
        services.AddScoped<DialogService>();
          // Register the theme service as singleton
        services.AddSingleton<ThemeService>();
          // Register the keyboard navigation service as singleton
        services.AddSingleton<KeyboardNavigationService>();
        
        // Register the performance optimization service as singleton
        services.AddSingleton<PerformanceOptimizationService>();

        services.AddScoped<WindowContext>();

        return services;
    }    
    /// <summary>
    /// Adds Blazor Window Manager services with configuration options
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configure">Configuration action for window manager options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBlazorWindowManager(
        this IServiceCollection services, 
        Action<WindowManagerOptions>? configure = null)
    {
        // Configure options if provided
        if (configure != null)
        {
            services.Configure(configure);
        }
        
        // Register the core services
        services.AddSingleton<WindowManagerService>();
        
        // Register the snapping service as singleton
        services.AddSingleton<SnappingService>();
        
        // Register the dialog service as scoped (for component lifecycle)
        services.AddScoped<DialogService>();
          // Register the theme service as singleton
        services.AddSingleton<ThemeService>();
          // Register the keyboard navigation service as singleton
        services.AddSingleton<KeyboardNavigationService>();
        
        // Register the performance optimization service as singleton
        services.AddSingleton<PerformanceOptimizationService>();

        services.AddScoped<WindowContext>();
        return services;
    }
}

/// <summary>
/// Configuration options for the Blazor Window Manager
/// </summary>
public class WindowManagerOptions
{
    /// <summary>
    /// Default width for new windows
    /// </summary>
    public double DefaultWidth { get; set; } = 600;
    
    /// <summary>
    /// Default height for new windows
    /// </summary>
    public double DefaultHeight { get; set; } = 400;
    
    /// <summary>
    /// Default minimum width for windows
    /// </summary>
    public double DefaultMinWidth { get; set; } = 200;
    
    /// <summary>
    /// Default minimum height for windows
    /// </summary>
    public double DefaultMinHeight { get; set; } = 150;
    
    /// <summary>
    /// Offset amount for cascading new windows
    /// </summary>
    public double CascadeOffset { get; set; } = 64;
    
    /// <summary>
    /// Height of the title bar in pixels
    /// </summary>
    public double TitleBarHeight { get; set; } = 32;
    
    /// <summary>
    /// Whether to enable window snapping by default
    /// </summary>
    public bool EnableSnapping { get; set; } = true;
    
    /// <summary>
    /// Distance threshold for snapping in pixels
    /// </summary>
    public double SnapThreshold { get; set; } = 10;
    
    /// <summary>
    /// Maximum number of windows that can be open simultaneously (0 = unlimited)
    /// </summary>
    public int MaxWindows { get; set; } = 0;
    
    /// <summary>
    /// Whether to enable animations for window operations
    /// </summary>
    public bool EnableAnimations { get; set; } = true;
    
    /// <summary>
    /// Duration of window animations in milliseconds
    /// </summary>
    public int AnimationDuration { get; set; } = 300;
}
