using System;

namespace HackerOs.OS.Applications.Attributes;

/// <summary>
/// Attribute used to mark a class as a HackerOS application and provide metadata
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class AppAttribute : Attribute
{
    /// <summary>
    /// Display name of the application
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Unique identifier for the application
    /// </summary>
    public string Id { get; }
    
    /// <summary>
    /// Path to the application icon
    /// </summary>
    public string? IconPath { get; set; }
    
    /// <summary>
    /// Description of what the application does
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Version string of the application
    /// </summary>
    public string Version { get; set; } = "1.0.0";
    
    /// <summary>
    /// Author or publisher of the application
    /// </summary>
    public string Author { get; set; } = "HackerOS System";
    
    /// <summary>
    /// Type of application
    /// </summary>
    public ApplicationType Type { get; set; } = ApplicationType.WindowedApplication;
    
    /// <summary>
    /// Whether multiple instances of this application can run
    /// </summary>
    public bool AllowMultipleInstances { get; set; } = true;
    
    /// <summary>
    /// Categories this application belongs to
    /// </summary>
    public string[] Categories { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Whether the application should auto-start with the system
    /// </summary>
    public bool AutoStart { get; set; } = false;
    
    /// <summary>
    /// Whether this is a system application
    /// </summary>
    public bool IsSystemApplication { get; set; } = false;
    
    /// <summary>
    /// Creates a new App attribute with name and ID
    /// </summary>
    /// <param name="name">Display name of the application</param>
    /// <param name="id">Unique identifier for the application</param>
    public AppAttribute(string name, string id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Application name cannot be empty", nameof(name));
            
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Application ID cannot be empty", nameof(id));
            
        Name = name;
        Id = id;
    }
    
    /// <summary>
    /// Converts this attribute to an ApplicationManifest
    /// </summary>
    /// <returns>A new ApplicationManifest instance</returns>
    public ApplicationManifest ToManifest()
    {
        var manifest = new ApplicationManifest
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Version = Version,
            Type = Type,
            Author = Author,
            IconPath = IconPath,
            EntryPoint = Id,
            AllowMultipleInstances = AllowMultipleInstances,
            IsSystemApplication = IsSystemApplication,
            AutoStart = AutoStart
        };
        
        foreach (var category in Categories)
        {
            manifest.Categories.Add(category);
        }
        
        return manifest;
    }
}
