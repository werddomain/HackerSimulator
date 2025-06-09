using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.User;
using HackerOs.OS.IO.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace HackerOs.OS.Applications;

/// <summary>
/// Interface for the file type registration system
/// </summary>
public interface IFileTypeRegistry
{
    /// <summary>
    /// Registers a file type with the system
    /// </summary>
    /// <param name="registration">File type registration</param>
    /// <returns>True if successful</returns>
    Task<bool> RegisterFileTypeAsync(FileTypeRegistration registration);
    
    /// <summary>
    /// Removes a file type registration
    /// </summary>
    /// <param name="applicationId">ID of the application</param>
    /// <param name="extension">File extension</param>
    /// <returns>True if successful</returns>
    Task<bool> UnregisterFileTypeAsync(string applicationId, string extension);
    
    /// <summary>
    /// Gets all applications that can handle a specific file extension
    /// </summary>
    /// <param name="extension">File extension (with or without dot)</param>
    /// <returns>Collection of file type registrations</returns>
    IEnumerable<FileTypeRegistration> GetHandlersForExtension(string extension);
    
    /// <summary>
    /// Gets the default application for a file extension
    /// </summary>
    /// <param name="extension">File extension (with or without dot)</param>
    /// <returns>File type registration, or null if not found</returns>
    FileTypeRegistration? GetDefaultHandlerForExtension(string extension);
    
    /// <summary>
    /// Gets all file types handled by a specific application
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <returns>Collection of file type registrations</returns>
    IEnumerable<FileTypeRegistration> GetRegistrationsForApplication(string applicationId);
    
    /// <summary>
    /// Gets the recommended application for opening a file
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>Application ID, or null if not found</returns>
    Task<string?> GetRecommendedApplicationForFileAsync(string filePath);
    
    /// <summary>
    /// Registers file types from an application class with the OpenFileType attribute
    /// </summary>
    /// <param name="appType">Application type</param>
    /// <param name="applicationId">Application ID</param>
    /// <returns>Number of registrations created</returns>
    Task<int> RegisterFromAttributesAsync(Type appType, string applicationId);
    
    /// <summary>
    /// Discovers and registers all file type handlers in the assembly
    /// </summary>
    /// <returns>Number of registrations created</returns>
    Task<int> DiscoverAndRegisterHandlersAsync();
    
    /// <summary>
    /// Sets an application as the default handler for an extension
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="extension">File extension</param>
    /// <returns>True if successful</returns>
    Task<bool> SetDefaultHandlerAsync(string applicationId, string extension);
    
    /// <summary>
    /// Gets all file type registrations
    /// </summary>
    IEnumerable<FileTypeRegistration> GetAllRegistrations();
    
    /// <summary>
    /// Saves the file type registry to the file system
    /// </summary>
    Task SaveRegistryAsync();
    
    /// <summary>
    /// Loads the file type registry from the file system
    /// </summary>
    Task LoadRegistryAsync();
}

/// <summary>
/// Service for managing file type associations and handlers
/// </summary>
public class FileTypeRegistry : IFileTypeRegistry
{
    private readonly ILogger<FileTypeRegistry> _logger;
    private readonly IVirtualFileSystem _fileSystem;
    
    // Extension -> List of registrations for that extension
    private readonly ConcurrentDictionary<string, List<FileTypeRegistration>> _registrationsByExtension = new(StringComparer.OrdinalIgnoreCase);
    
    // ApplicationId -> List of registrations for that application
    private readonly ConcurrentDictionary<string, List<FileTypeRegistration>> _registrationsByApplication = new();
    
    // Registry file location
    private const string REGISTRY_FILE = "/etc/filetypes.json";
    
    /// <summary>
    /// Creates a new instance of the FileTypeRegistry
    /// </summary>
    public FileTypeRegistry(ILogger<FileTypeRegistry> logger, IVirtualFileSystem fileSystem)
    {
        _logger = logger;
        _fileSystem = fileSystem;
    }
    
    /// <inheritdoc />
    public async Task<bool> RegisterFileTypeAsync(FileTypeRegistration registration)
    {
        if (registration == null) throw new ArgumentNullException(nameof(registration));
        if (string.IsNullOrEmpty(registration.ApplicationId))
            throw new ArgumentException("ApplicationId cannot be empty", nameof(registration));
        if (registration.Extensions.Count == 0)
            throw new ArgumentException("At least one extension must be specified", nameof(registration));
            
        try
        {
            // Add to application registrations
            _registrationsByApplication.AddOrUpdate(
                registration.ApplicationId,
                _ => new List<FileTypeRegistration> { registration.Clone() },
                (_, list) =>
                {
                    // Remove existing registrations for the same extensions
                    foreach (var ext in registration.Extensions)
                    {
                        list.RemoveAll(r => r.Extensions.Contains(ext));
                    }
                    list.Add(registration.Clone());
                    return list;
                });
            
            // Add to extension registrations
            foreach (var extension in registration.Extensions)
            {
                _registrationsByExtension.AddOrUpdate(
                    extension,
                    _ => new List<FileTypeRegistration> { registration.Clone() },
                    (_, list) =>
                    {
                        // Remove existing registration by the same app for this extension
                        list.RemoveAll(r => r.ApplicationId == registration.ApplicationId);
                        
                        // If this is the default handler, remove default flag from others
                        if (registration.IsDefault)
                        {
                            foreach (var reg in list)
                            {
                                reg.IsDefault = false;
                            }
                        }
                        
                        list.Add(registration.Clone());
                        // Sort by priority
                        list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                        return list;
                    });
            }
            
            await SaveRegistryAsync();
            _logger.LogInformation("Registered file type handler for {ExtCount} extensions with app {AppId}",
                registration.Extensions.Count, registration.ApplicationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register file type handler for {AppId}", registration.ApplicationId);
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> UnregisterFileTypeAsync(string applicationId, string extension)
    {
        if (string.IsNullOrEmpty(applicationId))
            throw new ArgumentException("ApplicationId cannot be empty", nameof(applicationId));
        if (string.IsNullOrEmpty(extension))
            throw new ArgumentException("Extension cannot be empty", nameof(extension));
            
        extension = extension.TrimStart('.');
        
        try
        {
            bool removed = false;
            
            // Remove from app registrations
            if (_registrationsByApplication.TryGetValue(applicationId, out var appRegs))
            {
                for (int i = appRegs.Count - 1; i >= 0; i--)
                {
                    var reg = appRegs[i];
                    if (reg.Extensions.Remove(extension))
                    {
                        removed = true;
                        if (reg.Extensions.Count == 0)
                        {
                            appRegs.RemoveAt(i);
                        }
                    }
                }
            }
            
            // Remove from extension registrations
            if (_registrationsByExtension.TryGetValue(extension, out var extRegs))
            {
                removed |= extRegs.RemoveAll(r => r.ApplicationId == applicationId) > 0;
                
                if (extRegs.Count == 0)
                {
                    _registrationsByExtension.TryRemove(extension, out _);
                }
            }
            
            if (removed)
            {
                await SaveRegistryAsync();
                _logger.LogInformation("Unregistered file type handler for extension {Ext} from app {AppId}",
                    extension, applicationId);
            }
            
            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister file type handler for extension {Ext} from app {AppId}",
                extension, applicationId);
            return false;
        }
    }
    
    /// <inheritdoc />
    public IEnumerable<FileTypeRegistration> GetHandlersForExtension(string extension)
    {
        extension = extension.TrimStart('.');
        
        if (_registrationsByExtension.TryGetValue(extension, out var registrations))
        {
            return registrations.OrderByDescending(r => r.Priority);
        }
        
        return Enumerable.Empty<FileTypeRegistration>();
    }
    
    /// <inheritdoc />
    public FileTypeRegistration? GetDefaultHandlerForExtension(string extension)
    {
        extension = extension.TrimStart('.');
        
        if (_registrationsByExtension.TryGetValue(extension, out var registrations))
        {
            // Return the default handler if one exists
            var defaultHandler = registrations.FirstOrDefault(r => r.IsDefault);
            if (defaultHandler != null)
                return defaultHandler;
                
            // Otherwise return the highest priority handler
            return registrations.OrderByDescending(r => r.Priority).FirstOrDefault();
        }
        
        return null;
    }
    
    /// <inheritdoc />
    public IEnumerable<FileTypeRegistration> GetRegistrationsForApplication(string applicationId)
    {
        if (_registrationsByApplication.TryGetValue(applicationId, out var registrations))
        {
            return registrations;
        }
        
        return Enumerable.Empty<FileTypeRegistration>();
    }
    
    /// <inheritdoc />
    public async Task<string?> GetRecommendedApplicationForFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
                return null;
                
            // Normalize path and verify file exists
            var normalizedPath = Path.GetFullPath(filePath);
            if (!await _fileSystem.FileExistsAsync(normalizedPath, UserManager.SystemUser))
                return null;
                
            // Extract extension
            var extension = System.IO.Path.GetExtension(normalizedPath);
            if (string.IsNullOrEmpty(extension))
                return null;
                
            extension = extension.Substring(1); // Remove the dot
            
            // Get default handler
            var handler = GetDefaultHandlerForExtension(extension);
            return handler?.ApplicationId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recommended application for file {Path}", filePath);
            return null;
        }
    }
    
    /// <inheritdoc />
    public async Task<int> RegisterFromAttributesAsync(Type appType, string applicationId)
    {
        if (appType == null)
            throw new ArgumentNullException(nameof(appType));
        if (string.IsNullOrEmpty(applicationId))
            throw new ArgumentException("ApplicationId cannot be empty", nameof(applicationId));
            
        try
        {
            int count = 0;
            
            // Get all OpenFileType attributes on the class
            var attributes = appType.GetCustomAttributes<OpenFileTypeAttribute>(true);
            foreach (var attribute in attributes)
            {
                var registration = attribute.ToFileTypeRegistration(applicationId);
                if (await RegisterFileTypeAsync(registration))
                {
                    count++;
                }
            }
            
            _logger.LogInformation("Registered {Count} file type handlers from attributes for app {AppId}", count, applicationId);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register file type handlers from attributes for app {AppId}", applicationId);
            return 0;
        }
    }
    
    /// <inheritdoc />
    public async Task<int> DiscoverAndRegisterHandlersAsync()
    {
        try
        {
            int count = 0;
            
            // Get all types with the App attribute
            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<AppAttribute>(false) != null);
                
            foreach (var type in types)
            {
                var appAttr = type.GetCustomAttribute<AppAttribute>(false);
                if (appAttr != null)
                {
                    count += await RegisterFromAttributesAsync(type, appAttr.Id);
                }
            }
            
            _logger.LogInformation("Discovered and registered {Count} file type handlers from assembly", count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover and register file type handlers");
            return 0;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> SetDefaultHandlerAsync(string applicationId, string extension)
    {
        extension = extension.TrimStart('.');
        
        try
        {
            if (!_registrationsByExtension.TryGetValue(extension, out var registrations))
                return false;
                
            var registration = registrations.FirstOrDefault(r => r.ApplicationId == applicationId);
            if (registration == null)
                return false;
                
            // Clear default flag from all registrations
            foreach (var reg in registrations)
            {
                reg.IsDefault = false;
            }
            
            // Set default flag on this registration
            registration.IsDefault = true;
            
            await SaveRegistryAsync();
            _logger.LogInformation("Set {AppId} as default handler for {Ext} files", applicationId, extension);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set default handler for extension {Ext}", extension);
            return false;
        }
    }
    
    /// <inheritdoc />
    public IEnumerable<FileTypeRegistration> GetAllRegistrations()
    {
        var allRegistrations = new HashSet<FileTypeRegistration>();
        
        foreach (var registrations in _registrationsByApplication.Values)
        {
            foreach (var registration in registrations)
            {
                allRegistrations.Add(registration);
            }
        }
        
        return allRegistrations;
    }
    
    /// <inheritdoc />
    public async Task SaveRegistryAsync()
    {
        try
        {
            // Ensure /etc directory exists
            if (!await _fileSystem.DirectoryExistsAsync("/etc", UserManager.SystemUser))
            {
                await _fileSystem.CreateDirectoryAsync("/etc", UserManager.SystemUser);
            }
            
            // Convert to serializable format
            var registrations = GetAllRegistrations().ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(registrations);
            
            // Write to file
            await _fileSystem.WriteAllTextAsync(REGISTRY_FILE, json, UserManager.SystemUser);
            _logger.LogInformation("Saved file type registry to {Path}", REGISTRY_FILE);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file type registry");
        }
    }
    
    /// <inheritdoc />
    public async Task LoadRegistryAsync()
    {
        try
        {
            // Clear existing registrations
            _registrationsByApplication.Clear();
            _registrationsByExtension.Clear();
            
            // Check if registry file exists
            if (!await _fileSystem.FileExistsAsync(REGISTRY_FILE, UserManager.SystemUser))
            {
                _logger.LogInformation("File type registry not found, creating new one");
                return;
            }
            
            // Read from file
            var json = await _fileSystem.ReadAllTextAsync(REGISTRY_FILE, UserManager.SystemUser);
            var registrations = System.Text.Json.JsonSerializer.Deserialize<List<FileTypeRegistration>>(json);
            
            if (registrations != null)
            {
                // Register each file type
                foreach (var registration in registrations)
                {
                    await RegisterFileTypeAsync(registration);
                }
                
                _logger.LogInformation("Loaded {Count} file type registrations from registry", registrations.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load file type registry");
        }
    }
}
