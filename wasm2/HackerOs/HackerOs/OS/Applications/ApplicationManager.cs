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
    private readonly IProcessManager _processManager;
    private readonly ConcurrentDictionary<string, ApplicationManifest> _registeredApplications = new();
    private readonly ConcurrentDictionary<int, IApplication> _runningApplications = new();
    private readonly ConcurrentDictionary<string, Type> _applicationTypes = new();
    private readonly object _statsLock = new();
    private ApplicationManagerStatistics _statistics = new();

    /// <inheritdoc />
    public event EventHandler<ApplicationLaunchedEventArgs>? ApplicationLaunched;

    /// <inheritdoc />
    public event EventHandler<ApplicationTerminatedEventArgs>? ApplicationTerminated;

    /// <inheritdoc />
    public event EventHandler<ApplicationStateChangedEventArgs>? ApplicationStateChanged;    
    
    public ApplicationManager(
        ILogger<ApplicationManager> logger, 
        IUserManager userManager, 
        IServiceProvider serviceProvider,
        IProcessManager processManager)
    {
        _logger = logger;
        _userManager = userManager;
        _serviceProvider = serviceProvider;
        _processManager = processManager;
        
        // Initialize system start time
        _statistics.SystemUptime = TimeSpan.Zero;
        
        // Register built-in applications
        _ = Task.Run(RegisterBuiltInApplicationsAsync);
        
        // Subscribe to process events from the kernel
        _processManager.OnProcessEvent += OnProcessEvent;
    }
    
    private void OnProcessEvent(object? sender, ProcessEvent e)
    {
        // Handle process events from the kernel
        if (e.EventType == ProcessEventType.Exited || e.EventType == ProcessEventType.Terminated)
        {
            // Remove from running applications if the process has exited
            if (_runningApplications.TryRemove(e.ProcessId, out var app))
            {
                // Update statistics
                lock (_statsLock)
                {
                    _statistics.TotalApplicationsTerminated++;
                    _statistics.RunningApplicationCount = _runningApplications.Count;
                }
                
                // Notify listeners
                ApplicationTerminated?.Invoke(this, new ApplicationTerminatedEventArgs(
                    app,
                    e.Data is int exitCode ? exitCode : 0,
                    e.EventType == ProcessEventType.Terminated,
                    e.Message));
            }
        }
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

            // Check permissions
            if (!await CheckApplicationPermissionsAsync(manifest.Id, manifest.RequiredPermissions, context.UserSession))
            {
                _logger.LogWarning("User {Username} does not have required permissions for application {ApplicationId}", 
                    context.UserSession.User.Username, manifest.Id);
                return null;
            }

            // Launch the application based on its type
            IApplication? application = null;
            
            switch (manifest.Type)
            {
                case ApplicationType.WindowedApplication:
                    application = await LaunchWindowedApplicationAsync(manifest, context);
                    break;
                    
                case ApplicationType.SystemService:
                    application = await LaunchServiceApplicationAsync(manifest, context);
                    break;
                    
                case ApplicationType.CommandLineTool:
                    application = await LaunchCommandLineApplicationAsync(manifest, context);
                    break;
                    
                case ApplicationType.SystemApplication:
                    // System applications are treated similarly to windowed applications by default
                    application = await LaunchWindowedApplicationAsync(manifest, context);
                    break;
                    
                default:
                    _logger.LogWarning("Unknown application type {Type} for {ApplicationId}", 
                        manifest.Type, manifest.Id);
                    return null;
            }
            
            if (application != null)
            {
                // Raise the application launched event
                ApplicationLaunched?.Invoke(this, new ApplicationLaunchedEventArgs(application, context));
            }
            
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

            // First try to gracefully stop the application
            bool terminated;
            if (!force)
            {
                terminated = await application.StopAsync();
                if (terminated)
                    return true;
            }
            
            // If that fails or force is true, use the process manager to terminate the process
            terminated = await _processManager.TerminateProcessAsync(processId);
            
            if (!terminated)
            {
                _logger.LogWarning("Failed to terminate process with PID {ProcessId}", processId);
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
            
            // Get process statistics from the kernel
            var processStats = _processManager.GetProcessStatisticsAsync().GetAwaiter().GetResult();
            
            // Update application statistics based on process statistics
            _statistics.TotalMemoryUsageBytes = processStats.TotalMemoryUsage;
            _statistics.TotalCpuTime = processStats.TotalCpuTime;
            
            // Update system uptime - assume the statistics already have a startup time reference point
            _statistics.SystemUptime = DateTime.UtcNow - processStats.Timestamp.AddMilliseconds(-processStats.TotalCpuTime.TotalMilliseconds);

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
            // Wait a small amount of time to ensure this runs as a truly async method
            await Task.Delay(1);
            
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
    /// Register built-in applications
    /// </summary>
    private async Task RegisterBuiltInApplicationsAsync()
    {
        try
        {
            // This would scan for built-in applications and register them
            // For now, we'll register some basic applications but in the final implementation
            // this would be dynamic based on the App attribute
            
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

    /// <summary>
    /// Helper method to add a running application to the tracking collection
    /// </summary>
    /// <param name="processId">Process ID</param>
    /// <param name="application">Application instance</param>
    public void AddRunningApplication(int processId, IApplication application)
    {
        _runningApplications[processId] = application;
        
        // Update statistics
        lock (_statsLock)
        {
            _statistics.RunningApplicationCount = _runningApplications.Count;
            
            if (!_statistics.ApplicationsByType.ContainsKey(application.Type))
                _statistics.ApplicationsByType[application.Type] = 0;
            _statistics.ApplicationsByType[application.Type]++;
            
            if (!_statistics.ApplicationsByState.ContainsKey(ApplicationState.Running))
                _statistics.ApplicationsByState[ApplicationState.Running] = 0;
            _statistics.ApplicationsByState[ApplicationState.Running]++;
        }
    }

    /// <summary>
    /// Launch a windowed application
    /// </summary>
    /// <param name="manifest">Application manifest</param>
    /// <param name="context">Launch context</param>
    /// <returns>Launched application if successful, null otherwise</returns>
    private async Task<IApplication?> LaunchWindowedApplicationAsync(ApplicationManifest manifest, ApplicationLaunchContext context)
    {
        _logger.LogInformation("Launching windowed application {ApplicationId}", manifest.Id);
        
        // Create application instance
        var application = await CreateApplicationInstanceAsync(manifest, context);
        if (application == null)
        {
            _logger.LogError("Failed to create windowed application instance for {ApplicationId}", manifest.Id);
            return null;
        }
        
        // Set process start info specific to windowed applications
        var processStartInfo = new ProcessStartInfo
        {
            Name = manifest.Name,
            ExecutablePath = manifest.EntryPoint,
            Arguments = context.Arguments?.ToArray() ?? Array.Empty<string>(),
            WorkingDirectory = context.WorkingDirectory ?? "/",
            Owner = context.UserSession.User.Username,
            ParentProcessId = context.ParentProcessId ?? 0,
            Environment = new Dictionary<string, string>
            {
                { "APPLICATION_ID", manifest.Id },
                { "APPLICATION_VERSION", manifest.Version },
                { "USER", context.UserSession.User.Username },
                { "APPLICATION_TYPE", "windowed" },
                { "WINDOW_TITLE", manifest.Name },
                { "WINDOW_ICON", manifest.IconPath ?? string.Empty }
            },
            CreateWindow = true, // Always create a window for windowed applications
            Priority = ProcessPriority.Normal, // Default priority for UI applications
            IsBackground = false // UI apps typically run in foreground
        };
        
        // Create the process
        var process = await _processManager.CreateProcessAsync(processStartInfo);
        if (process == null)
        {
            _logger.LogError("Failed to create process for windowed application {ApplicationId}", manifest.Id);
            return null;
        }
        
        // Assign the process ID to the application
        application.GetType().GetProperty("ProcessId")?.SetValue(application, process.ProcessId);
        
        // Subscribe to application events
        SubscribeToApplicationEvents(application);
        
        // Start the application with its context
        var started = await application.StartAsync(context);
        if (!started)
        {
            _logger.LogError("Failed to start windowed application {ApplicationId}", manifest.Id);
            await _processManager.TerminateProcessAsync(process.ProcessId);
            return null;
        }
        
        // Track the running application
        _runningApplications[application.ProcessId] = application;
        
        // Update statistics
        UpdateApplicationStatistics(manifest.Type, ApplicationState.Running);
        
        _logger.LogInformation("Successfully launched windowed application {ApplicationId} with PID {ProcessId}", 
            manifest.Id, application.ProcessId);
            
        return application;
    }
    
    /// <summary>
    /// Launch a service application
    /// </summary>
    /// <param name="manifest">Application manifest</param>
    /// <param name="context">Launch context</param>
    /// <returns>Launched application if successful, null otherwise</returns>
    private async Task<IApplication?> LaunchServiceApplicationAsync(ApplicationManifest manifest, ApplicationLaunchContext context)
    {
        _logger.LogInformation("Launching service application {ApplicationId}", manifest.Id);
        
        // Create application instance
        var application = await CreateApplicationInstanceAsync(manifest, context);
        if (application == null)
        {
            _logger.LogError("Failed to create service application instance for {ApplicationId}", manifest.Id);
            return null;
        }
        
        // Set process start info specific to service applications
        var processStartInfo = new ProcessStartInfo
        {
            Name = manifest.Name,
            ExecutablePath = manifest.EntryPoint,
            Arguments = context.Arguments?.ToArray() ?? Array.Empty<string>(),
            WorkingDirectory = context.WorkingDirectory ?? "/",
            Owner = context.UserSession.User.Username,
            ParentProcessId = context.ParentProcessId ?? 0,
            Environment = new Dictionary<string, string>
            {
                { "APPLICATION_ID", manifest.Id },
                { "APPLICATION_VERSION", manifest.Version },
                { "USER", context.UserSession.User.Username },
                { "APPLICATION_TYPE", "service" }
            },
            CreateWindow = false, // Services don't create windows
            Priority = ProcessPriority.BelowNormal, // Services typically run at lower priority
            IsBackground = true // Services run in the background
        };
        
        // Create the process
        var process = await _processManager.CreateProcessAsync(processStartInfo);
        if (process == null)
        {
            _logger.LogError("Failed to create process for service application {ApplicationId}", manifest.Id);
            return null;
        }
        
        // Assign the process ID to the application
        application.GetType().GetProperty("ProcessId")?.SetValue(application, process.ProcessId);
        
        // Subscribe to application events
        SubscribeToApplicationEvents(application);
        
        // Start the application with its context
        var started = await application.StartAsync(context);
        if (!started)
        {
            _logger.LogError("Failed to start service application {ApplicationId}", manifest.Id);
            await _processManager.TerminateProcessAsync(process.ProcessId);
            return null;
        }
        
        // Track the running application
        _runningApplications[application.ProcessId] = application;
        
        // Update statistics
        UpdateApplicationStatistics(manifest.Type, ApplicationState.Running);
        
        _logger.LogInformation("Successfully launched service application {ApplicationId} with PID {ProcessId}", 
            manifest.Id, application.ProcessId);
            
        return application;
    }
    
    /// <summary>
    /// Launch a command-line application
    /// </summary>
    /// <param name="manifest">Application manifest</param>
    /// <param name="context">Launch context</param>
    /// <returns>Launched application if successful, null otherwise</returns>
    private async Task<IApplication?> LaunchCommandLineApplicationAsync(ApplicationManifest manifest, ApplicationLaunchContext context)
    {
        _logger.LogInformation("Launching command-line application {ApplicationId}", manifest.Id);
        
        // Create application instance
        var application = await CreateApplicationInstanceAsync(manifest, context);
        if (application == null)
        {
            _logger.LogError("Failed to create command-line application instance for {ApplicationId}", manifest.Id);
            return null;
        }
        
        // Set process start info specific to command-line applications
        var processStartInfo = new ProcessStartInfo
        {
            Name = manifest.Name,
            ExecutablePath = manifest.EntryPoint,
            Arguments = context.Arguments?.ToArray() ?? Array.Empty<string>(),
            WorkingDirectory = context.WorkingDirectory ?? "/",
            Owner = context.UserSession.User.Username,
            ParentProcessId = context.ParentProcessId ?? 0,
            Environment = new Dictionary<string, string>
            {
                { "APPLICATION_ID", manifest.Id },
                { "APPLICATION_VERSION", manifest.Version },
                { "USER", context.UserSession.User.Username },
                { "APPLICATION_TYPE", "command" }
            },
            CreateWindow = false, // Command-line apps don't create windows
            Priority = ProcessPriority.Normal, // Default priority
            IsBackground = false // Command line tools typically run in foreground
        };
        
        // Create the process
        var process = await _processManager.CreateProcessAsync(processStartInfo);
        if (process == null)
        {
            _logger.LogError("Failed to create process for command-line application {ApplicationId}", manifest.Id);
            return null;
        }
        
        // Assign the process ID to the application
        application.GetType().GetProperty("ProcessId")?.SetValue(application, process.ProcessId);
        
        // Subscribe to application events
        SubscribeToApplicationEvents(application);
        
        // Start the application with its context
        var started = await application.StartAsync(context);
        if (!started)
        {
            _logger.LogError("Failed to start command-line application {ApplicationId}", manifest.Id);
            await _processManager.TerminateProcessAsync(process.ProcessId);
            return null;
        }
        
        // Track the running application
        _runningApplications[application.ProcessId] = application;
        
        // Update statistics
        UpdateApplicationStatistics(manifest.Type, ApplicationState.Running);
        
        _logger.LogInformation("Successfully launched command-line application {ApplicationId} with PID {ProcessId}", 
            manifest.Id, application.ProcessId);
            
        return application;
    }
    
    /// <summary>
    /// Helper method to update application statistics
    /// </summary>
    private void UpdateApplicationStatistics(ApplicationType applicationType, ApplicationState applicationState)
    {
        lock (_statsLock)
        {
            _statistics.TotalApplicationsLaunched++;
            _statistics.RunningApplicationCount = _runningApplications.Count;
            
            if (!_statistics.ApplicationsByType.ContainsKey(applicationType))
                _statistics.ApplicationsByType[applicationType] = 0;
            _statistics.ApplicationsByType[applicationType]++;
            
            if (!_statistics.ApplicationsByState.ContainsKey(applicationState))
                _statistics.ApplicationsByState[applicationState] = 0;
            _statistics.ApplicationsByState[applicationState]++;
        }
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
