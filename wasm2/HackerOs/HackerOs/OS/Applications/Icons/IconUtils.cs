using Microsoft.AspNetCore.Components;

namespace HackerOs.OS.Applications.Icons;

/// <summary>
/// Utility methods for working with icons
/// </summary>
public static class IconUtils
{
    /// <summary>
    /// Determine if an icon path represents a Font Awesome icon
    /// </summary>
    public static bool IsFontAwesomeIcon(string iconPath)
    {
        return !string.IsNullOrWhiteSpace(iconPath) && 
               (iconPath.StartsWith("fa-") || iconPath.StartsWith("fas:") || 
                iconPath.StartsWith("far:") || iconPath.StartsWith("fab:") || 
                iconPath.StartsWith("fal:") || iconPath.StartsWith("fad:"));
    }
    
    /// <summary>
    /// Determine if an icon path represents a Material Design icon
    /// </summary>
    public static bool IsMaterialIcon(string iconPath)
    {
        return !string.IsNullOrWhiteSpace(iconPath) && 
               (iconPath.StartsWith("md-") || iconPath.StartsWith("material-"));
    }
    
    /// <summary>
    /// Determine if an icon path represents a file path
    /// </summary>
    public static bool IsFilePath(string iconPath)
    {
        return !string.IsNullOrWhiteSpace(iconPath) && 
               iconPath.StartsWith("/") && iconPath.Contains(".");
    }
    
    /// <summary>
    /// Get a default icon RenderFragment for an application
    /// </summary>
    public static RenderFragment GetDefaultAppIcon(string appName = "App")
    {
        return builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "default-app-icon");
            
            string firstLetter = string.IsNullOrEmpty(appName) ? "A" : appName[0].ToString().ToUpper();
            
            builder.OpenElement(2, "span");
            builder.AddAttribute(3, "class", "app-icon-letter");
            builder.AddContent(4, firstLetter);
            builder.CloseElement();
            
            builder.CloseElement();
        };
    }
}
