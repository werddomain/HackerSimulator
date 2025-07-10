using Microsoft.AspNetCore.Components;

namespace HackerOs.OS.Applications.Registry;

/// <summary>
/// Metadata for an application, including statistics and icon information
/// </summary>
public class ApplicationMetadata
{
    /// <summary>
    /// Unique identifier for the application
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the application
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of what the application does
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Path or identifier for the application's icon
    /// </summary>
    public string? IconPath { get; set; }
    
    /// <summary>
    /// Categories this application belongs to
    /// </summary>
    public List<string> Categories { get; set; } = new();
    
    /// <summary>
    /// Whether the application is pinned to the taskbar or start menu
    /// </summary>
    public bool IsPinned { get; set; }
    
    /// <summary>
    /// Number of times the application has been launched
    /// </summary>
    public int LaunchCount { get; set; }
    
    /// <summary>
    /// Last time the application was launched
    /// </summary>
    public DateTime LastLaunched { get; set; }
    
    /// <summary>
    /// The application type
    /// </summary>
    public ApplicationType Type { get; set; }
    
    /// <summary>
    /// Whether the application is a system application
    /// </summary>
    public bool IsSystemApplication { get; set; }
    
    /// <summary>
    /// Whether multiple instances of this application can run
    /// </summary>
    public bool AllowMultipleInstances { get; set; }
    
    /// <summary>
    /// Whether the application should auto-start with the system
    /// </summary>
    public bool AutoStart { get; set; }
    
    /// <summary>
    /// The author or publisher of the application
    /// </summary>
    public string Author { get; set; } = string.Empty;
    
    /// <summary>
    /// The version of the application
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional details about the application
    /// </summary>
    public string Details { get; set; } = string.Empty;
    
    /// <summary>
    /// Usage instructions for the application
    /// </summary>
    public string Usage { get; set; } = string.Empty;
    
    /// <summary>
    /// URL for more information about the application
    /// </summary>
    public string InfoUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Support contact information for the application
    /// </summary>
    public string SupportContact { get; set; } = string.Empty;
    
    /// <summary>
    /// Create metadata from an application manifest
    /// </summary>
    /// <param name="manifest">Application manifest</param>
    /// <returns>Application metadata</returns>
    public static ApplicationMetadata FromManifest(ApplicationManifest manifest)
    {
        return new ApplicationMetadata
        {
            Id = manifest.Id,
            Name = manifest.Name,
            Description = manifest.Description,
            IconPath = manifest.IconPath,
            Categories = manifest.Categories.ToList(),
            IsSystemApplication = manifest.IsSystemApplication,
            AllowMultipleInstances = manifest.AllowMultipleInstances,
            AutoStart = manifest.AutoStart,
            Author = manifest.Author,
            Version = manifest.Version,
            Type = manifest.Type
        };
    }
}
