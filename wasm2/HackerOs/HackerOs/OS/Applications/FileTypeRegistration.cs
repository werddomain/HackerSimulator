using System.Collections.Generic;

namespace HackerOs.OS.Applications;

/// <summary>
/// Represents a file type registration associated with an application
/// </summary>
public class FileTypeRegistration
{
    /// <summary>
    /// The file extensions supported by this registration (without the dot)
    /// </summary>
    public HashSet<string> Extensions { get; } = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Descriptive name of the file type
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// ID of the application that handles this file type
    /// </summary>
    public string ApplicationId { get; set; } = string.Empty;
    
    /// <summary>
    /// Path to the icon representing this file type
    /// </summary>
    public string? IconPath { get; set; }
    
    /// <summary>
    /// MIME type for the file type
    /// </summary>
    public string? MimeType { get; set; }
    
    /// <summary>
    /// Priority of this handler (higher values take precedence)
    /// </summary>
    public int Priority { get; set; } = 0;
    
    /// <summary>
    /// Whether this is the default handler for the extensions
    /// </summary>
    public bool IsDefault { get; set; }
    public string FileExtension { get; internal set; }
    public string ApplicationName { get; internal set; }

    /// <summary>
    /// Creates a new file type registration
    /// </summary>
    public FileTypeRegistration() { }
    
    /// <summary>
    /// Creates a new file type registration with the specified extensions
    /// </summary>
    /// <param name="extensions">File extensions supported by this registration (without the dot)</param>
    public FileTypeRegistration(IEnumerable<string> extensions)
    {
        foreach (var extension in extensions)
        {
            // Ensure extensions don't have leading dots
            var cleanExtension = extension.TrimStart('.');
            Extensions.Add(cleanExtension);
        }
    }
    
    /// <summary>
    /// Creates a new file type registration with description and application ID
    /// </summary>
    /// <param name="description">Description of the file type</param>
    /// <param name="applicationId">ID of the application that handles this file type</param>
    /// <param name="extensions">File extensions supported (without the dot)</param>
    public FileTypeRegistration(string description, string applicationId, params string[] extensions)
        : this(extensions)
    {
        Description = description;
        ApplicationId = applicationId;
    }
    
    /// <summary>
    /// Checks if this registration supports a specific file extension
    /// </summary>
    /// <param name="extension">File extension (with or without dot)</param>
    /// <returns>True if supported</returns>
    public bool SupportsExtension(string extension)
    {
        var cleanExtension = extension.TrimStart('.');
        return Extensions.Contains(cleanExtension);
    }
    
    /// <summary>
    /// Creates a copy of this registration
    /// </summary>
    public FileTypeRegistration Clone()
    {
        return new FileTypeRegistration(Extensions)
        {
            Description = Description,
            ApplicationId = ApplicationId,
            IconPath = IconPath,
            MimeType = MimeType,
            Priority = Priority,
            IsDefault = IsDefault
        };
    }
}
