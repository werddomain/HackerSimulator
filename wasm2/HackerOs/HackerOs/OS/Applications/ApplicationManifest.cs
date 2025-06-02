using System.ComponentModel.DataAnnotations;

namespace HackerOs.OS.Applications;

/// <summary>
/// Application manifest containing metadata and configuration
/// </summary>
public class ApplicationManifest
{
    /// <summary>
    /// Unique identifier for the application
    /// </summary>
    [Required]
    public required string Id { get; set; }

    /// <summary>
    /// Display name of the application
    /// </summary>
    [Required]
    public required string Name { get; set; }

    /// <summary>
    /// Description of what the application does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Application version string
    /// </summary>
    [Required]
    public required string Version { get; set; }

    /// <summary>
    /// Type of application
    /// </summary>
    public ApplicationType Type { get; set; } = ApplicationType.WindowedApplication;

    /// <summary>
    /// Author or publisher of the application
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Path to the application icon
    /// </summary>
    public string? IconPath { get; set; }

    /// <summary>
    /// Main executable or entry point
    /// </summary>
    [Required]
    public required string EntryPoint { get; set; }

    /// <summary>
    /// Working directory for the application
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Command line arguments template
    /// </summary>
    public List<string> DefaultArguments { get; set; } = new();

    /// <summary>
    /// Environment variables required by the application
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Required permissions for the application
    /// </summary>
    public List<string> RequiredPermissions { get; set; } = new();

    /// <summary>
    /// Supported file types for file associations
    /// </summary>
    public List<string> SupportedFileTypes { get; set; } = new();

    /// <summary>
    /// Categories this application belongs to
    /// </summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>
    /// Whether the application is a system application
    /// </summary>
    public bool IsSystemApplication { get; set; } = false;

    /// <summary>
    /// Whether the application should auto-start with the system
    /// </summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>
    /// Whether multiple instances of this application can run
    /// </summary>
    public bool AllowMultipleInstances { get; set; } = true;

    /// <summary>
    /// Minimum OS version required
    /// </summary>
    public string? MinimumOSVersion { get; set; }

    /// <summary>
    /// Dependencies on other applications or libraries
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Resource requirements
    /// </summary>
    public ApplicationResourceRequirements ResourceRequirements { get; set; } = new();

    /// <summary>
    /// Window configuration for windowed applications
    /// </summary>
    public ApplicationWindowConfig? WindowConfig { get; set; }

    /// <summary>
    /// Installation timestamp
    /// </summary>
    public DateTime InstallDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validate the manifest for correctness
    /// </summary>
    /// <returns>Validation result with any errors</returns>
    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
            errors.Add("Application ID is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Application name is required");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Application version is required");

        if (string.IsNullOrWhiteSpace(EntryPoint))
            errors.Add("Entry point is required");

        // Validate ID format (should be alphanumeric with dots and dashes)
        if (!string.IsNullOrWhiteSpace(Id) && !System.Text.RegularExpressions.Regex.IsMatch(Id, @"^[a-zA-Z0-9\.\-_]+$"))
            errors.Add("Application ID contains invalid characters");

        return new ValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
/// Resource requirements for an application
/// </summary>
public class ApplicationResourceRequirements
{
    /// <summary>
    /// Minimum required memory in MB
    /// </summary>
    public int MinMemoryMB { get; set; } = 64;

    /// <summary>
    /// Maximum allowed memory in MB (0 = unlimited)
    /// </summary>
    public int MaxMemoryMB { get; set; } = 0;

    /// <summary>
    /// Minimum required disk space in MB
    /// </summary>
    public int MinDiskSpaceMB { get; set; } = 10;

    /// <summary>
    /// CPU priority (0-100, where 100 is highest)
    /// </summary>
    public int CpuPriority { get; set; } = 50;

    /// <summary>
    /// Whether the application requires network access
    /// </summary>
    public bool RequiresNetwork { get; set; } = false;

    /// <summary>
    /// Whether the application requires file system access
    /// </summary>
    public bool RequiresFileSystem { get; set; } = true;
}

/// <summary>
/// Window configuration for windowed applications
/// </summary>
public class ApplicationWindowConfig
{
    /// <summary>
    /// Default window width
    /// </summary>
    public int DefaultWidth { get; set; } = 800;

    /// <summary>
    /// Default window height
    /// </summary>
    public int DefaultHeight { get; set; } = 600;

    /// <summary>
    /// Minimum window width
    /// </summary>
    public int MinWidth { get; set; } = 300;

    /// <summary>
    /// Minimum window height
    /// </summary>
    public int MinHeight { get; set; } = 200;

    /// <summary>
    /// Maximum window width (0 = unlimited)
    /// </summary>
    public int MaxWidth { get; set; } = 0;

    /// <summary>
    /// Maximum window height (0 = unlimited)
    /// </summary>
    public int MaxHeight { get; set; } = 0;

    /// <summary>
    /// Whether the window can be resized
    /// </summary>
    public bool Resizable { get; set; } = true;

    /// <summary>
    /// Whether the window should start maximized
    /// </summary>
    public bool StartMaximized { get; set; } = false;

    /// <summary>
    /// Whether the window should start minimized
    /// </summary>
    public bool StartMinimized { get; set; } = false;

    /// <summary>
    /// Whether the window should always stay on top
    /// </summary>
    public bool AlwaysOnTop { get; set; } = false;

    /// <summary>
    /// Whether the window has a title bar
    /// </summary>
    public bool HasTitleBar { get; set; } = true;

    /// <summary>
    /// Whether the window can be closed
    /// </summary>
    public bool CanClose { get; set; } = true;

    /// <summary>
    /// Whether the window can be minimized
    /// </summary>
    public bool CanMinimize { get; set; } = true;

    /// <summary>
    /// Whether the window can be maximized
    /// </summary>
    public bool CanMaximize { get; set; } = true;
}

/// <summary>
/// Validation result for manifest validation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Errors { get; }

    public ValidationResult(bool isValid, IEnumerable<string> errors)
    {
        IsValid = isValid;
        Errors = errors.ToList().AsReadOnly();
    }
}
