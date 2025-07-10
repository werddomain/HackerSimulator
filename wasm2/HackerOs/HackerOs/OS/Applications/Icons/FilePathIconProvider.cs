using HackerOs.OS.IO;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace HackerOs.OS.Applications.Icons;

/// <summary>
/// Icon provider for file-based icons (paths to image files in the file system)
/// </summary>
public class FilePathIconProvider : IIconProvider
{
    private readonly IVirtualFileSystem _fileSystem;
    
    public FilePathIconProvider(IVirtualFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }
    
    /// <summary>
    /// File-based icon provider has medium priority
    /// </summary>
    public int Priority => 10;
    
    /// <summary>
    /// Can handle any path that looks like a file path and exists in the file system
    /// </summary>
    public bool CanHandleIcon(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
            return false;
            
        // Check if it's a path with extension
        if (iconPath.StartsWith("/") && iconPath.Contains("."))
        {
            return _fileSystem.FileExistsAsync(iconPath).GetAwaiter().GetResult();
        }
        
        return false;
    }
    
    /// <summary>
    /// Returns an img element with the specified file path
    /// </summary>
    public RenderFragment GetIcon(string iconPath, string? cssClass = null)
    {
        return builder =>
        {
            builder.OpenElement(0, "img");
            builder.AddAttribute(1, "src", iconPath);
            builder.AddAttribute(2, "alt", "Application Icon");
            
            if (!string.IsNullOrWhiteSpace(cssClass))
                builder.AddAttribute(3, "class", cssClass);
                
            builder.CloseElement();
        };
    }
}
