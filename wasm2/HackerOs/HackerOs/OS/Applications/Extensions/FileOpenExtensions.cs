using System;
using System.IO;
using System.Threading.Tasks;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;

namespace HackerOs.OS.Applications.Extensions;

/// <summary>
/// Extension methods for opening files with associated applications
/// </summary>
public static class FileOpenExtensions
{
    /// <summary>
    /// Opens a file with the default associated application
    /// </summary>
    /// <param name="fileSystem">Virtual file system</param>
    /// <param name="filePath">Path to the file to open</param>
    /// <param name="applicationManager">Application manager</param>
    /// <param name="fileTypeRegistry">File type registry</param>
    /// <param name="userSession">User session</param>
    /// <returns>True if the file was opened successfully</returns>
    public static async Task<bool> OpenWithDefaultApplicationAsync(
        this IVirtualFileSystem fileSystem,
        string filePath,
        IApplicationManager applicationManager,
        IFileTypeRegistry fileTypeRegistry,
        UserSession userSession)
    {
        if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be empty", nameof(filePath));
        if (applicationManager == null) throw new ArgumentNullException(nameof(applicationManager));
        if (fileTypeRegistry == null) throw new ArgumentNullException(nameof(fileTypeRegistry));
        if (userSession == null) throw new ArgumentNullException(nameof(userSession));
        
        try
        {
            // Normalize path and check if file exists
            var normalizedPath = fileSystem.NormalizePath(filePath);
            if (!await fileSystem.FileExistsAsync(normalizedPath))
            {
                return false;
            }
            
            // Get file extension
            var extension = Path.GetExtension(normalizedPath);
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }
            
            // Remove the dot
            extension = extension.Substring(1);
            
            // Get default handler
            var handler = fileTypeRegistry.GetDefaultHandlerForExtension(extension);
            if (handler == null)
            {
                return false;
            }
            
            // Launch the application
            var appId = handler.ApplicationId;
            var context = new ApplicationLaunchContext
            {
                UserSession = userSession,
                Arguments = new[] { normalizedPath },
                WorkingDirectory = Path.GetDirectoryName(normalizedPath)
            };
            
            var app = await applicationManager.LaunchApplicationAsync(appId, context);
            return app != null;
        }
        catch (Exception ex)
        {
            // Log error if there's a logger available, but don't expose it to callers
            // Logger would typically be injected, but since this is an extension method, we just handle the exception
            Console.Error.WriteLine($"Error opening file {filePath}: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Opens a file with a specific application
    /// </summary>
    /// <param name="fileSystem">Virtual file system</param>
    /// <param name="filePath">Path to the file to open</param>
    /// <param name="applicationId">ID of the application to use</param>
    /// <param name="applicationManager">Application manager</param>
    /// <param name="userSession">User session</param>
    /// <returns>True if the file was opened successfully</returns>
    public static async Task<bool> OpenWithApplicationAsync(
        this IVirtualFileSystem fileSystem,
        string filePath,
        string applicationId,
        IApplicationManager applicationManager,
        UserSession userSession)
    {
        if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be empty", nameof(filePath));
        if (string.IsNullOrEmpty(applicationId)) throw new ArgumentException("Application ID cannot be empty", nameof(applicationId));
        if (applicationManager == null) throw new ArgumentNullException(nameof(applicationManager));
        if (userSession == null) throw new ArgumentNullException(nameof(userSession));
        
        try
        {
            // Normalize path and check if file exists
            var normalizedPath = fileSystem.NormalizePath(filePath);
            if (!await fileSystem.FileExistsAsync(normalizedPath))
            {
                return false;
            }
            
            // Launch the application with the file as an argument
            var context = new ApplicationLaunchContext
            {
                UserSession = userSession,
                Arguments = new[] { normalizedPath },
                WorkingDirectory = Path.GetDirectoryName(normalizedPath)
            };
            
            var app = await applicationManager.LaunchApplicationAsync(applicationId, context);
            return app != null;
        }
        catch (Exception ex)
        {
            // Log error if there's a logger available, but don't expose it to callers
            Console.Error.WriteLine($"Error opening file {filePath} with application {applicationId}: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Sets an application as the default handler for a file type based on file extension
    /// </summary>
    /// <param name="filePath">Path to a file with the target extension</param>
    /// <param name="applicationId">ID of the application to make default</param>
    /// <param name="fileTypeRegistry">File type registry</param>
    /// <returns>True if the default was set successfully</returns>
    public static async Task<bool> SetDefaultApplicationForFileTypeAsync(
        string filePath,
        string applicationId,
        IFileTypeRegistry fileTypeRegistry)
    {
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be empty", nameof(filePath));
        if (string.IsNullOrEmpty(applicationId)) throw new ArgumentException("Application ID cannot be empty", nameof(applicationId));
        if (fileTypeRegistry == null) throw new ArgumentNullException(nameof(fileTypeRegistry));
        
        try
        {
            // Get file extension
            var extension = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }
            
            // Remove the dot
            extension = extension.Substring(1);
            
            // Set default handler
            return await fileTypeRegistry.SetDefaultHandlerAsync(applicationId, extension);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error setting default application for file type: {ex.Message}");
            return false;
        }
    }
}
