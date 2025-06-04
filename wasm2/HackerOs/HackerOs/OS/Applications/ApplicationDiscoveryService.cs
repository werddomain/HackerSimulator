using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.IO.FileSystem;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications;

/// <summary>
/// Interface for application discovery and registration from attributes
/// </summary>
public interface IApplicationDiscoveryService
{
    /// <summary>
    /// Discovers applications marked with App attribute
    /// </summary>
    /// <returns>Number of applications discovered</returns>
    Task<int> DiscoverApplicationsAsync();
    
    /// <summary>
    /// Gets discovered application types with their manifests
    /// </summary>
    IDictionary<Type, ApplicationManifest> DiscoveredApplications { get; }
}

/// <summary>
/// Service that discovers and registers applications from attributes
/// </summary>
public class ApplicationDiscoveryService : IApplicationDiscoveryService
{
    private readonly ILogger<ApplicationDiscoveryService> _logger;
    private readonly IApplicationManager _applicationManager;
    private readonly IFileTypeRegistry _fileTypeRegistry;
    private readonly IVirtualFileSystem _fileSystem;
    private readonly Dictionary<Type, ApplicationManifest> _discoveredApplications = new();
    
    // Application manifest directory
    private const string MANIFEST_DIRECTORY = "/usr/share/applications";
    
    /// <inheritdoc />
    public IDictionary<Type, ApplicationManifest> DiscoveredApplications => _discoveredApplications;
    
    /// <summary>
    /// Creates a new instance of the ApplicationDiscoveryService
    /// </summary>
    public ApplicationDiscoveryService(
        ILogger<ApplicationDiscoveryService> logger,
        IApplicationManager applicationManager,
        IFileTypeRegistry fileTypeRegistry,
        IVirtualFileSystem fileSystem)
    {
        _logger = logger;
        _applicationManager = applicationManager;
        _fileTypeRegistry = fileTypeRegistry;
        _fileSystem = fileSystem;
    }
    
    /// <inheritdoc />
    public async Task<int> DiscoverApplicationsAsync()
    {
        try
        {
            // Clear existing discovered applications
            _discoveredApplications.Clear();
            
            // Get all types with the App attribute
            var assembly = Assembly.GetExecutingAssembly();
            var appTypes = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<AppAttribute>(false) != null)
                .ToList();
                
            _logger.LogInformation("Found {Count} types with App attribute", appTypes.Count);
            
            // Register each application
            foreach (var type in appTypes)
            {
                var appAttr = type.GetCustomAttribute<AppAttribute>(false);
                if (appAttr != null)
                {
                    // Create manifest from attribute
                    var manifest = appAttr.ToManifest();
                    
                    // Set full type name as entry point
                    manifest.EntryPoint = type.FullName!;
                    
                    // Register the manifest
                    if (await _applicationManager.RegisterApplicationAsync(manifest))
                    {
                        _discoveredApplications.Add(type, manifest);
                        _logger.LogInformation("Registered application {AppId} from type {TypeName}", manifest.Id, type.Name);
                        
                        // Register file type handlers
                        await _fileTypeRegistry.RegisterFromAttributesAsync(type, manifest.Id);
                        
                        // Save manifest to file system
                        await SaveManifestAsync(manifest);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to register application {AppId} from type {TypeName}", manifest.Id, type.Name);
                    }
                }
            }
            
            _logger.LogInformation("Discovered and registered {Count} applications", _discoveredApplications.Count);
            return _discoveredApplications.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover applications");
            return 0;
        }
    }
    
    /// <summary>
    /// Saves an application manifest to the file system
    /// </summary>
    private async Task SaveManifestAsync(ApplicationManifest manifest)
    {
        try
        {
            // Ensure the manifest directory exists
            if (!await _fileSystem.DirectoryExistsAsync(MANIFEST_DIRECTORY))
            {
                await _fileSystem.CreateDirectoryAsync(MANIFEST_DIRECTORY, true);
            }
            
            // Create manifest filename
            var manifestPath = $"{MANIFEST_DIRECTORY}/{manifest.Id}.app.json";
            
            // Serialize the manifest
            var json = System.Text.Json.JsonSerializer.Serialize(manifest);
            
            // Write to file
            await _fileSystem.WriteAllTextAsync(manifestPath, json);
            _logger.LogInformation("Saved manifest for {AppId} to {Path}", manifest.Id, manifestPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save manifest for application {AppId}", manifest.Id);
        }
    }
}
