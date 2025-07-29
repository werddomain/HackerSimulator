using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace HackerOs.OS.Applications.Icons;

/// <summary>
/// Icon provider for Font Awesome icons
/// </summary>
public class FontAwesomeIconProvider : IIconProvider
{
    /// <summary>
    /// Font Awesome provider has high priority
    /// </summary>
    public int Priority => 20;
    
    /// <summary>
    /// Can handle any icon path starting with "fa-"
    /// </summary>
    public bool CanHandleIcon(string iconPath)
    {
        return !string.IsNullOrWhiteSpace(iconPath) && iconPath.StartsWith("fa-");
    }
    
    /// <summary>
    /// Returns an i element with the appropriate Font Awesome classes
    /// </summary>
    public RenderFragment GetIcon(string iconPath, string? cssClass = null)
    {
        return builder =>
        {
            builder.OpenElement(0, "i");
            
            string iconName = iconPath;
            string style = "fas"; // Default to solid style
            
            // Check if the icon path specifies a style
            if (iconPath.Contains(':'))
            {
                var parts = iconPath.Split(':');
                if (parts.Length == 2)
                {
                    style = GetFontAwesomeStyle(parts[0]);
                    iconName = parts[1];
                }
            }
            
            // If the icon name still has the fa- prefix, remove it
            if (iconName.StartsWith("fa-"))
                iconName = iconName.Substring(3);
                
            // Add the appropriate classes
            string faClasses = $"{style} fa-{iconName}";
            if (!string.IsNullOrWhiteSpace(cssClass))
                faClasses = $"{faClasses} {cssClass}";
                
            builder.AddAttribute(1, "class", faClasses);
            builder.CloseElement();
        };
    }
    
    /// <summary>
    /// Converts shorthand style to Font Awesome class
    /// </summary>
    private string GetFontAwesomeStyle(string style)
    {
        return style switch
        {
            "fas" or "solid" => "fas",
            "far" or "regular" => "far",
            "fab" or "brands" => "fab",
            "fal" or "light" => "fal",
            "fad" or "duotone" => "fad",
            _ => "fas" // Default to solid
        };
    }
}
