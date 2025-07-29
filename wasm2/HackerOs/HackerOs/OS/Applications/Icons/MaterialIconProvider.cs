using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace HackerOs.OS.Applications.Icons;

/// <summary>
/// Icon provider for Material Design Icons
/// </summary>
public class MaterialIconProvider : IIconProvider
{
    /// <summary>
    /// Material icon provider has high priority
    /// </summary>
    public int Priority => 20;
    
    /// <summary>
    /// Can handle any icon path starting with "md-" or "material-"
    /// </summary>
    public bool CanHandleIcon(string iconPath)
    {
        return !string.IsNullOrWhiteSpace(iconPath) && 
               (iconPath.StartsWith("md-") || iconPath.StartsWith("material-"));
    }
    
    /// <summary>
    /// Returns a span element with the Material Icons font and the icon name
    /// </summary>
    public RenderFragment GetIcon(string iconPath, string? cssClass = null)
    {
        return builder =>
        {
            builder.OpenElement(0, "span");
            
            string iconName = iconPath;
            
            // Remove the prefix
            if (iconName.StartsWith("md-"))
                iconName = iconName.Substring(3);
            else if (iconName.StartsWith("material-"))
                iconName = iconName.Substring(9);
                
            // Add the appropriate classes
            string classes = "material-icons";
            if (!string.IsNullOrWhiteSpace(cssClass))
                classes = $"{classes} {cssClass}";
                
            builder.AddAttribute(1, "class", classes);
            builder.AddContent(2, iconName);
            builder.CloseElement();
        };
    }
}
