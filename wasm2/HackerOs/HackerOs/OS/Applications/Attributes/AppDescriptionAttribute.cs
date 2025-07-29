using System;

namespace HackerOs.OS.Applications.Attributes;

/// <summary>
/// Attribute used to provide an extended description for a HackerOS application
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class AppDescriptionAttribute : Attribute
{
    /// <summary>
    /// Extended description of the application
    /// </summary>
    public string Description { get; }
    
    /// <summary>
    /// Optional additional details about the application
    /// </summary>
    public string Details { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional usage instructions
    /// </summary>
    public string Usage { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional URL for more information
    /// </summary>
    public string InfoUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional support contact information
    /// </summary>
    public string SupportContact { get; set; } = string.Empty;
    
    /// <summary>
    /// Creates a new AppDescription attribute with the specified description
    /// </summary>
    /// <param name="description">Extended description of the application</param>
    public AppDescriptionAttribute(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));
            
        Description = description;
    }
}
