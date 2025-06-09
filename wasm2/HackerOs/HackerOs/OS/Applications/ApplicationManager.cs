using HackerOs.OS.Applications;
using HackerOs.OS.Applications.BuiltIn;
using HackerOs.OS.User;
using HackerOs.OS.IO.FileSystem;
using HackerOs.OS.Shell;
using HackerOs.OS.Kernel.Process;
using HackerOs.OS.Kernel.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using System.Collections.Concurrent;
using System.Reflection;

namespace HackerOs.OS.Applications;

/// <summary>
/// Main application manager for HackerOS
/// </summary>
public class ApplicationManager : IApplicationManager
{
    private readonly ILogger<ApplicationManager> _logger;
    private readonly IUserManager _userManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, ApplicationManifest> _registeredApplications = new();
    private readonly ConcurrentDictionary<int, IApplication> _runningApplications = new();
    private readonly ConcurrentDictionary<string, Type> _applicationTypes = new();
    private readonly object _statsLock = new();
    private ApplicationManagerStatistics _statistics = new();
    private int _nextProcessId = 1000;

    /// <inheritdoc />
    public event EventHandler<ApplicationLaunchedEventArgs>? ApplicationLaunched;

    /// <inheritdoc />
    public event EventHandler<ApplicationTerminatedEventArgs>? ApplicationTerminated;

    /// <inheritdoc />
    public event EventHandler<ApplicationStateChangedEventArgs>? ApplicationStateChanged;    public ApplicationManager(ILogger<ApplicationManager> logger, IUserManager userManager, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _userManager = userManager;
        _serviceProvider = serviceProvider;
        
        // Initialize system start time
        _statistics.SystemUptime = TimeSpan.Zero;
        
        // Register built-in applications
        _ = Task.Run(RegisterBuiltInApplicationsAsync);
    }

    /// <inheritdoc />
    public async Task<IApplication?> LaunchApplicationAsync(string applicationId, ApplicationLaunchContext context)
    {
        try
        {
            if (!_registeredApplications.TryGetValue(applicationId, out var manifest))
            {
                _logger.LogWarning("Application {ApplicationId} not found", applicationId);
                return null;
            }

            return await LaunchApplicationAsync(manifest, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch application {ApplicationId}", applicationId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IApplication?> LaunchApplicationAsync(ApplicationManifest manifest, ApplicationLaunchContext context)
    {
        try
        {
            // Validate manifest
            var validationResult = manifest.Validate();
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Invalid manifest for application {ApplicationId}: {Errors}", 
                    manifest.Id, string.Join(", ", validationResult.Errors));
                return null;
            }

            // Check if multiple instances are allowed
            if (!manifest.AllowMultipleInstances)
            {
                var existingApp = _runningApplications.Values
                    .FirstOrDefault(app => app.Id == manifest.Id);
                
                if (existingApp != null)
                {
                    _logger.LogWarning("Application {ApplicationId} is already running and multiple instances are not allowed", manifest.Id);
                    return existingApp;
                }
            }

            // Check permissions            if (!await CheckApplicationPermissionsAsync(manifest.Id, manifest.RequiredPermissions, context.UserSession))
            {
                _logger.LogWarning("User {Username} does not have required permissions for application {ApplicationId}", 
                    context.UserSession.User.Username, manifest.Id);
                return null;
            }

            // Create application instance
            var application = await CreateApplicationInstanceAsync(manifest, context);
            if (application == null)
            {
                _logger.LogError("Failed to create application instance for {ApplicationId}", manifest.Id);
                return null;
            }

            // Assign process ID
            application.GetType().GetProperty("ProcessId")?.SetValue(application, GetNextProcessId());

            // Subscribe to application events
            SubscribeToApplicationEvents(application);

            // Start the application
            var started = await application.StartAsync(context);
            if (!started)
            {
                _logger.LogError("Failed to start application {ApplicationId}", manifest.Id);
                return null;
            }

            // Track the running application
            _runningApplications[application.ProcessId] = application;

            // Update statistics
            lock (_statsLock)
            {
                _statistics.TotalApplicationsLaunched++;
                _statistics.RunningApplicationCount = _runningApplications.Count;
                
                if (!_statistics.ApplicationsByType.ContainsKey(manifest.Type))
                    _statistics.ApplicationsByType[manifest.Type] = 0;
                _statistics.ApplicationsByType[manifest.Type]++;
                
                if (!_statistics.ApplicationsByState.ContainsKey(ApplicationState.Running))
                    _statistics.ApplicationsByState[ApplicationState.Running] = 0;
                _statistics.ApplicationsByState[ApplicationState.Running]++;
            }

            _logger.LogInformation("Successfully launched application {ApplicationId} with PID {ProcessId}", 
                manifest.Id, application.ProcessId);

            ApplicationLaunched?.Invoke(this, new ApplicationLaunchedEventArgs(application, context));
            return application;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception launching application {ApplicationId}", manifest.Id);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RegisterApplicationAsync(ApplicationManifest manifest)
    {
        try
        {
            // Validate manifest
            var validationResult = manifest.Validate();
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Cannot register invalid manifest for {ApplicationId}: {Errors}", 
                    manifest.Id, string.Join(", ", validationResult.Errors));
                return false;
            }

            // Check if already registered
            if (_registeredApplications.ContainsKey(manifest.Id))
            {
                _logger.LogWarning("Application {ApplicationId} is already registered", manifest.Id);
                return false;
            }

            _registeredApplications[manifest.Id] = manifest;
            
            lock (_statsLock)
            {
                _statistics.RegisteredApplicationCount = _registeredApplications.Count;
            }

            _logger.LogInformation("Registered application {ApplicationId} v{Version}", manifest.Id, manifest.Version);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register application {ApplicationId}", manifest.Id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UnregisterApplicationAsync(string applicationId)
    {
        try
        {
            // Terminate any running instances first
            var runningInstances = _runningApplications.Values
                .Where(app => app.Id == applicationId)
                .ToList();

            foreach (var instance in runningInstances)
            {
                await instance.TerminateAsync();
            }

            var removed = _registeredApplications.TryRemove(applicationId, out _);
            
            if (removed)
            {
                lock (_statsLock)
                {
                    _statistics.RegisteredApplicationCount = _registeredApplications.Count;
                }
                
                _logger.LogInformation("Unregistered application {ApplicationId}", applicationId);
            }

            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister application {ApplicationId}", applicationId);
            return false;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ApplicationManifest> GetAvailableApplications()
    {
        return _registeredApplications.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<IApplication> GetRunningApplications()
    {
        return _runningApplications.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<IApplication> GetRunningApplications(UserSession session)
    {        return _runningApplications.Values
            .Where(app => app.OwnerSession?.SessionId == session.SessionId)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public ApplicationManifest? GetApplication(string applicationId)
    {
        _registeredApplications.TryGetValue(applicationId, out var manifest);
        return manifest;
    }

    /// <inheritdoc />
    public IApplication? GetRunningApplication(int processId)
    {
        _runningApplications.TryGetValue(processId, out var application);
        return application;
    }

    /// <inheritdoc />
    public IApplication? GetRunningApplication(string applicationId)
    {
        return _runningApplications.Values.FirstOrDefault(a => a.Id == applicationId);
    }

    /// <inheritdoc />
    public async Task<bool> TerminateApplicationAsync(string applicationId, bool force = false)
    {
        try
        {
            var applications = _runningApplications.Values
                .Where(app => app.Id == applicationId)
                .ToList();

            if (!applications.Any())
                return true;

            var allTerminated = true;
            foreach (var app in applications)
            {
                bool terminated;
                if (force)
                {
                    terminated = await app.TerminateAsync();
                }
                else
                {
                    terminated = await app.StopAsync();
                }

                if (!terminated)
                    allTerminated = false;
            }

            return allTerminated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate application {ApplicationId}", applicationId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TerminateApplicationAsync(int processId, bool force = false)
    {
        try
        {
            if (!_runningApplications.TryGetValue(processId, out var application))
                return true;

            bool terminated;
            if (force)
            {
                terminated = await application.TerminateAsync();
            }
            else
            {
                terminated = await application.StopAsync();
            }

            return terminated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate application with PID {ProcessId}", processId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> TerminateSessionApplicationsAsync(UserSession session, bool force = false)
    {
        try
        {
            var sessionApps = GetRunningApplications(session);
            int terminatedCount = 0;

            foreach (var app in sessionApps)
            {
                bool terminated;
                if (force)
                {
                    terminated = await app.TerminateAsync();
                }
                else
                {
                    terminated = await app.StopAsync();
                }

                if (terminated)
                    terminatedCount++;
            }

            return terminatedCount;
        }        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate session applications for user {Username}", session.User.Username);
            return 0;
        }
    }    /// <inheritdoc />
    public async Task<bool> CheckApplicationPermissionsAsync(string applicationId, IEnumerable<string> permissions, UserSession session)
    {
        try
        {
            // Get user groups
            var userGroups = await _userManager.GetUserGroupsAsync(session.User.Username);
            var groupNames = userGroups.Select(g => g.GroupName.ToLower()).ToHashSet();

            // Basic permission checking - can be enhanced with more sophisticated security model
            foreach (var permission in permissions)
            {
                switch (permission.ToLower())
                {
                    case "filesystem.read":
                    case "filesystem.write":
                        // All users have basic filesystem access
                        break;
                    
                    case "network.access":
                        // Network access requires user to be in network group or admin
                        if (!groupNames.Contains("network") && !groupNames.Contains("admin"))
                            return false;
                        break;
                    
                    case "system.admin":
                        // System admin requires admin group membership
                        if (!groupNames.Contains("admin"))
                            return false;
                        break;
                    
                    default:
                        _logger.LogWarning("Unknown permission requested: {Permission}", permission);
                        break;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permissions for application {ApplicationId}", applicationId);
            return false;
        }
    }

    /// <inheritdoc />
    public ApplicationManagerStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            // Update runtime statistics
            _statistics.RunningApplicationCount = _runningApplications.Count;
            _statistics.TotalMemoryUsageBytes = _runningApplications.Values
                .Sum(app => app.GetStatistics().MemoryUsageBytes);
            _statistics.TotalCpuTime = TimeSpan.FromTicks(_runningApplications.Values
                .Sum(app => app.GetStatistics().CpuTime.Ticks));

            return _statistics;
        }
    }

    /// <summary>
    /// Creates a render fragment to display the application's user interface.
    /// </summary>
    /// <param name="application">Application instance.</param>
    public RenderFragment GetApplicationContentRenderer(IApplication application)
    {
        if (application is ComponentBase)
        {
            var type = application.GetType();
            return builder =>
            {
                builder.OpenComponent(0, type);
                builder.CloseComponent();
            };
        }

        return builder => builder.AddContent(0, $"Application {application.Name} has no UI.");
    }

    /// <summary>
    /// Create an application instance from a manifest
    /// </summary>
    private async Task<IApplication?> CreateApplicationInstanceAsync(ApplicationManifest manifest, ApplicationLaunchContext context)
    {
        try
        {
            // Create built-in applications based on their ID
            return manifest.Id switch
            {
                "system.terminal" => new TerminalEmulator(_serviceProvider.GetRequiredService<Shell.IShell>()),
                "system.filemanager" => new FileManager(_serviceProvider.GetRequiredService<IO.FileSystem.IVirtualFileSystem>()),
                "system.texteditor" => new TextEditor(_serviceProvider.GetRequiredService<IO.FileSystem.IVirtualFileSystem>()),
                "system.systemmonitor" => new SystemMonitor(
                    _serviceProvider.GetRequiredService<Kernel.Process.IProcessManager>(),
                    _serviceProvider.GetRequiredService<Kernel.Memory.IMemoryManager>()),
                _ => CreateGenericApplication(manifest)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create application instance for {ApplicationId}", manifest.Id);
            return null;
        }
    }
    
    /// <summary>
    /// Create a generic application for unknown types
    /// </summary>
    private IApplication CreateGenericApplication(ApplicationManifest manifest)
    {
        return new GenericApplication(manifest);
    }

    /// <summary>
    /// Subscribe to application events
    /// </summary>
    private void SubscribeToApplicationEvents(IApplication application)
    {
        application.StateChanged += OnApplicationStateChanged;
    }

    /// <summary>
    /// Handle application state changes
    /// </summary>
    private void OnApplicationStateChanged(object? sender, ApplicationStateChangedEventArgs e)
    {
        if (e.NewState == ApplicationState.Stopped || e.NewState == ApplicationState.Crashed)
        {
            // Remove from running applications
            _runningApplications.TryRemove(e.Application.ProcessId, out _);
            
            // Update statistics
            lock (_statsLock)
            {
                _statistics.TotalApplicationsTerminated++;
                _statistics.RunningApplicationCount = _runningApplications.Count;
                
                if (e.NewState == ApplicationState.Crashed)
                    _statistics.CrashCount++;
            }

            ApplicationTerminated?.Invoke(this, new ApplicationTerminatedEventArgs(
                e.Application, 
                0, // Exit code - could be enhanced
                e.NewState == ApplicationState.Crashed,
                e.Reason));
        }

        ApplicationStateChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Get the next available process ID
    /// </summary>
    private int GetNextProcessId()
    {
        return Interlocked.Increment(ref _nextProcessId);
    }    /// <summary>
    /// Register built-in applications
    /// </summary>
    private async Task RegisterBuiltInApplicationsAsync()
    {
        try
        {
            // This would scan for built-in applications and register them
            // For now, we'll register some basic applications
            
            var terminalManifest = new ApplicationManifest
            {
                Id = "system.terminal",
                Name = "Terminal",
                Description = "Command line terminal emulator",
                Version = "1.0.0",
                Type = ApplicationType.WindowedApplication,
                EntryPoint = "terminal",
                Author = "HackerOS System",
                IsSystemApplication = true,
                RequiredPermissions = { "filesystem.read", "filesystem.write" },
                Categories = { "System", "Terminal" }
            };

            await RegisterApplicationAsync(terminalManifest);

            var fileManagerManifest = new ApplicationManifest
            {
                Id = "system.filemanager",
                Name = "File Manager",
                Description = "Graphical file manager",
                Version = "1.0.0",
                Type = ApplicationType.WindowedApplication,
                EntryPoint = "filemanager",
                Author = "HackerOS System",
                IsSystemApplication = true,
                RequiredPermissions = { "filesystem.read", "filesystem.write" },
                Categories = { "System", "File Management" }
            };

            await RegisterApplicationAsync(fileManagerManifest);

            _logger.LogInformation("Registered {Count} built-in applications", _registeredApplications.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register built-in applications");
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ApplicationManifest> GetAllApplications()
    {
        return _registeredApplications.Values.ToList().AsReadOnly();
    }
}

/// <summary>
/// Generic application implementation for simple applications
/// </summary>
internal class GenericApplication : ApplicationBase
{
    public GenericApplication(ApplicationManifest manifest) : base(manifest)
    {
    }

    protected override Task<bool> OnStartAsync(ApplicationLaunchContext context)
    {
        OnOutput($"Generic application {Name} started", OutputStreamType.StandardOutput);
        return Task.FromResult(true);
    }

    protected override Task<bool> OnStopAsync()
    {
        OnOutput($"Generic application {Name} stopped", OutputStreamType.StandardOutput);
        return Task.FromResult(true);
    }
}
