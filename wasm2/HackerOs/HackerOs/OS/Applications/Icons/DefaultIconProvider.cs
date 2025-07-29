using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace HackerOs.OS.Applications.Icons;

/// <summary>
/// Default icon provider for when no other provider can handle the icon path
/// </summary>
public class DefaultIconProvider : IIconProvider
{
    /// <summary>
    /// Default provider has lowest priority
    /// </summary>
    public int Priority => -100;
    
    /// <summary>
    /// Can handle any icon path as a fallback
    /// </summary>
    public bool CanHandleIcon(string iconPath)
    {
        return true; // Always returns true as the fallback provider
    }
    
    /// <summary>
    /// Returns a default application icon
    /// </summary>
    public RenderFragment GetIcon(string iconPath, string? cssClass = null)
    {
        return builder =>
        {
            builder.OpenElement(0, "div");
            
            string classes = "default-app-icon";
            if (!string.IsNullOrWhiteSpace(cssClass))
                classes = $"{classes} {cssClass}";
                
            builder.AddAttribute(1, "class", classes);
            
            // Create a simple app icon with first letter
            string firstLetter = string.IsNullOrEmpty(iconPath) ? "A" : iconPath[0].ToString().ToUpper();
            
            builder.OpenElement(2, "span");
            builder.AddAttribute(3, "class", "app-icon-letter");
            builder.AddContent(4, firstLetter);
            builder.CloseElement();
            
            builder.CloseElement();
        };
    }
}
