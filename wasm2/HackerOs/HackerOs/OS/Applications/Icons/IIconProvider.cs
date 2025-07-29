using Microsoft.AspNetCore.Components;

namespace HackerOs.OS.Applications.Icons;

/// <summary>
/// Interface for icon providers that can render different types of icons
/// </summary>
public interface IIconProvider
{
    /// <summary>
    /// Determines if this provider can handle the specified icon path
    /// </summary>
    /// <param name="iconPath">Path or identifier for the icon</param>
    /// <returns>True if this provider can handle the icon path</returns>
    bool CanHandleIcon(string iconPath);
    
    /// <summary>
    /// Gets the icon as a RenderFragment that can be displayed in the UI
    /// </summary>
    /// <param name="iconPath">Path or identifier for the icon</param>
    /// <param name="cssClass">Optional CSS class to apply to the icon</param>
    /// <returns>A RenderFragment representing the icon</returns>
    RenderFragment GetIcon(string iconPath, string? cssClass = null);
    
    /// <summary>
    /// Gets the priority of this provider (higher priority providers are checked first)
    /// </summary>
    int Priority { get; }
}
