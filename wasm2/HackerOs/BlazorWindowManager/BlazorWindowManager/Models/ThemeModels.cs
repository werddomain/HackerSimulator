using System.ComponentModel;

namespace BlazorWindowManager.Models;

/// <summary>
/// Interface for defining themes in the window manager
/// </summary>
public interface ITheme
{
    /// <summary>
    /// Unique identifier for the theme
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Human-readable name of the theme
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of the theme
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// CSS class name to apply to the root container
    /// </summary>
    string CssClass { get; }
    
    /// <summary>
    /// Path to the theme's CSS file
    /// </summary>
    string CssFilePath { get; }
    
    /// <summary>
    /// Theme category for organization
    /// </summary>
    ThemeCategory Category { get; }
    
    /// <summary>
    /// Whether this theme supports dark mode aesthetics
    /// </summary>
    bool IsDarkTheme { get; }
    
    /// <summary>
    /// Custom CSS variables specific to this theme
    /// </summary>
    Dictionary<string, string> CustomVariables { get; }
}

/// <summary>
/// Built-in theme definition implementation
/// </summary>
public class ThemeDefinition : ITheme
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CssClass { get; set; } = string.Empty;
    public string CssFilePath { get; set; } = string.Empty;
    public ThemeCategory Category { get; set; }
    public bool IsDarkTheme { get; set; }
    public Dictionary<string, string> CustomVariables { get; set; } = new();
}

/// <summary>
/// Categories for organizing themes
/// </summary>
public enum ThemeCategory
{
    [Description("Modern themes with contemporary design")]
    Modern,
    
    [Description("Classic Windows operating system themes")]
    Windows,
    
    [Description("Apple macOS inspired themes")]
    MacOS,
    
    [Description("Linux and Unix inspired themes")]
    Linux,
    
    [Description("Retro and vintage computer themes")]
    Retro,
    
    [Description("Gaming and hacker-themed designs")]
    Gaming,
    
    [Description("User-defined custom themes")]
    Custom
}

/// <summary>
/// Event arguments for theme change events
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    public ITheme? PreviousTheme { get; set; }
    public ITheme NewTheme { get; set; } = null!;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Theme preferences and settings
/// </summary>
public class ThemeSettings
{
    /// <summary>
    /// Currently active theme ID
    /// </summary>
    public string ActiveThemeId { get; set; } = "hacker-matrix";
    
    /// <summary>
    /// Whether to apply theme transitions when switching
    /// </summary>
    public bool EnableThemeTransitions { get; set; } = true;
    
    /// <summary>
    /// Duration of theme transition animations
    /// </summary>
    public TimeSpan TransitionDuration { get; set; } = TimeSpan.FromMilliseconds(300);
    
    /// <summary>
    /// Whether to persist theme selection across sessions
    /// </summary>
    public bool PersistThemeSelection { get; set; } = true;
    
    /// <summary>
    /// Custom user-defined CSS variables that override theme defaults
    /// </summary>
    public Dictionary<string, string> UserCustomVariables { get; set; } = new();
}
