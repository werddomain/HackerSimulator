using System;

namespace HackerOs.OS.Applications.Attributes;

/// <summary>
/// Attribute used to mark an application as a handler for specific file types
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class OpenFileTypeAttribute : Attribute
{
    /// <summary>
    /// File extensions supported by the application (without the dot)
    /// </summary>
    public string[] Extensions { get; }
    
    /// <summary>
    /// Description of the file type
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Priority of this handler (higher values take precedence)
    /// </summary>
    public int Priority { get; set; } = 0;
    
    /// <summary>
    /// MIME type for the file type
    /// </summary>
    public string? MimeType { get; set; }
    
    /// <summary>
    /// Path to the icon representing this file type
    /// </summary>
    public string? IconPath { get; set; }
    
    /// <summary>
    /// Whether this application should be the default handler
    /// </summary>
    public bool IsDefault { get; set; } = false;
    
    /// <summary>
    /// Creates a new OpenFileType attribute with the specified extensions
    /// </summary>
    /// <param name="extensions">File extensions supported by the application (without the dot)</param>
    public OpenFileTypeAttribute(params string[] extensions)
    {
        if (extensions == null || extensions.Length == 0)
            throw new ArgumentException("At least one file extension must be provided", nameof(extensions));
            
        Extensions = extensions;
        Description = $"{extensions[0].ToUpperInvariant()} File";
    }
    
    /// <summary>
    /// Creates a new OpenFileType attribute with a description and extensions
    /// </summary>
    /// <param name="description">Description of the file type</param>
    /// <param name="extensions">File extensions supported by the application (without the dot)</param>
    public OpenFileTypeAttribute(string description, params string[] extensions)
        : this(extensions)
    {
        Description = description;
    }
    
    /// <summary>
    /// Converts this attribute to a FileTypeRegistration
    /// </summary>
    /// <param name="applicationId">ID of the application</param>
    /// <returns>A new FileTypeRegistration instance</returns>
    public FileTypeRegistration ToFileTypeRegistration(string applicationId)
    {
        return new FileTypeRegistration(Extensions)
        {
            Description = Description,
            ApplicationId = applicationId,
            IconPath = IconPath,
            MimeType = MimeType,
            Priority = Priority,
            IsDefault = IsDefault
        };
    }
}
