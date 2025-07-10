//using HackerOs.OS.Applications.Registry;
//using HackerOs.OS.Applications.Lifecycle;
//using HackerOs.OS.IO;
//using HackerOs.OS.User;
//using Microsoft.Extensions.Logging;
//using System.Text.Json;

//namespace HackerOs.OS.Applications.Lifecycle;

///// <summary>
///// Base class for applications that handles common lifecycle operations
///// </summary>
//public abstract class ApplicationBase : IApplication, IApplicationLifecycle
//{
//    // IApplication implementation
//    public string Id => Metadata?.Id ?? string.Empty;
//    public string Name => Metadata?.Name ?? string.Empty;
//    public string Description => Metadata?.Description ?? string.Empty;
//    public string Version => Metadata?.Version ?? "1.0.0";
//    public string IconPath => Metadata?.IconPath ?? string.Empty;
//    public ApplicationType Type => ApplicationType.Console; // Default to Console type
//    public ApplicationMetadata Manifest => Metadata ?? new ApplicationMetadata { Id = "unknown", Name = "Unknown" };
//    public ApplicationState State { get; protected set; } = ApplicationState.NotStarted;
//    public UserSession OwnerSession => Session ?? throw new InvalidOperationException("Application not initialized");
//    public int ProcessId { get; } = Environment.CurrentManagedThreadId;

//    public event EventHandler<ApplicationStateChangedEventArgs>? StateChanged;
//    public event EventHandler<ApplicationOutputEventArgs>? OutputReceived;
//    public event EventHandler<ApplicationErrorEventArgs>? ErrorReceived;

//    protected ILogger? Logger { get; private set; }
//    protected IVirtualFileSystem? FileSystem { get; private set; }
//    protected UserSession? Session { get; private set; }
//    protected ApplicationMetadata? Metadata { get; private set; }
//    protected User.User? User { get; private set; }
    
//    /// <summary>
//    /// Get the application's state directory path
//    /// </summary>
//    protected string StateDirectory => User != null ? $"/home/{User.Username}/.config/{Metadata?.Id}" : string.Empty;
    
//    /// <summary>
//    /// Get the application's state file path
//    /// </summary>
//    protected string StateFile => $"{StateDirectory}/state.json";

//    protected void OnStateChanged(ApplicationState oldState, ApplicationState newState)
//    {
//        State = newState;
//        StateChanged?.Invoke(this, new ApplicationStateChangedEventArgs(oldState, newState));
//    }

//    protected void OnOutput(string output)
//    {
//        OutputReceived?.Invoke(this, new ApplicationOutputEventArgs(output));
//    }

//    protected void OnError(string error)
//    {
//        ErrorReceived?.Invoke(this, new ApplicationErrorEventArgs(error));
//    }

//    /// <summary>
//    /// Implementation of application startup
//    /// </summary>
//    public async Task StartAsync(ApplicationLaunchContext context)
//    {
//        var oldState = State;
//        State = ApplicationState.Starting;

//        try
//        {
//            await OnStartAsync(new ApplicationLifecycleContext 
//            { 
//                Metadata = context.Metadata,
//                Session = context.Session,
//                User = context.User,
//                Logger = context.Logger,
//                FileSystem = context.FileSystem,
//                Arguments = context.Arguments.ToArray()
//            });

//            State = ApplicationState.Running;
//            OnStateChanged(oldState, State);
//        }
//        catch (Exception ex)
//        {
//            State = ApplicationState.Failed;
//            OnStateChanged(oldState, State);
//            OnError($"Failed to start application: {ex.Message}");
//            throw;
//        }
//    }

//    public async Task StopAsync()
//    {
//        var oldState = State;
//        State = ApplicationState.Stopping;

//        try
//        {
//            await OnStopAsync();
//            State = ApplicationState.Stopped;
//            OnStateChanged(oldState, State);
//        }
//        catch (Exception ex)
//        {
//            State = ApplicationState.Failed;
//            OnStateChanged(oldState, State);
//            OnError($"Failed to stop application: {ex.Message}");
//            throw;
//        }
//    }

//    public async Task PauseAsync()
//    {
//        if (State != ApplicationState.Running)
//            throw new InvalidOperationException("Application must be running to pause");

//        var oldState = State;
//        try
//        {
//            await OnPauseAsync();
//            State = ApplicationState.Paused;
//            OnStateChanged(oldState, State);
//        }
//        catch (Exception ex)
//        {
//            OnError($"Failed to pause application: {ex.Message}");
//            throw;
//        }
//    }

//    public async Task ResumeAsync()
//    {
//        if (State != ApplicationState.Paused)
//            throw new InvalidOperationException("Application must be paused to resume");

//        var oldState = State;
//        try
//        {
//            await OnResumeAsync();
//            State = ApplicationState.Running;
//            OnStateChanged(oldState, State);
//        }
//        catch (Exception ex)
//        {
//            OnError($"Failed to resume application: {ex.Message}");
//            throw;
//        }
//    }

//    public async Task TerminateAsync()
//    {
//        var oldState = State;
//        try
//        {
//            await OnTerminateAsync();
//            State = ApplicationState.Stopped;
//            OnStateChanged(oldState, State);
//        }
//        catch (Exception ex)
//        {
//            State = ApplicationState.Failed;
//            OnStateChanged(oldState, State);
//            OnError($"Failed to terminate application: {ex.Message}");
//            throw;
//        }
//    }

//    public async Task SendMessageAsync(object message)
//    {
//        try
//        {
//            await OnMessageReceivedAsync(message);
//        }
//        catch (Exception ex)
//        {
//            OnError($"Failed to process message: {ex.Message}");
//            throw;
//        }
//    }

//    public ApplicationStatistics GetStatistics()
//    {
//        return new ApplicationStatistics
//        {
//            StartTime = DateTime.UtcNow, // TODO: Track actual start time
//            Uptime = TimeSpan.Zero, // TODO: Calculate actual uptime
//            MemoryUsage = GC.GetTotalMemory(false),
//            CpuUsage = 0, // TODO: Track CPU usage
//            ThreadCount = 1, // TODO: Track actual thread count
//            HandleCount = 0 // TODO: Track actual handle count
//        };
//    }
    
//    // IApplicationLifecycle implementation
//    public virtual Task OnStartAsync(ApplicationLifecycleContext context)
//    {
//        // Store context information
//        Logger = context.Logger;
//        FileSystem = context.FileSystem;
//        Session = context.Session;
//        Metadata = context.Metadata;
//        User = context.User;
        
//        // Log start
//        Logger?.LogInformation("Application {AppId} started by user {Username}", Metadata?.Id, User?.Username);
        
//        return Task.CompletedTask;
//    }
    
//    public abstract Task OnStopAsync();
    
//    protected virtual Task OnPauseAsync() => Task.CompletedTask;
//    protected virtual Task OnResumeAsync() => Task.CompletedTask;
//    protected virtual Task OnTerminateAsync() => StopAsync();
//    protected virtual Task OnMessageReceivedAsync(object message) => Task.CompletedTask;
    
//    /// <inheritdoc />
//    public virtual Task OnActivateAsync(ApplicationLifecycleContext context)
//    {
//        Logger?.LogDebug("Application {AppId} activated", Metadata?.Id);
//        return Task.CompletedTask;
//    }
    
//    /// <inheritdoc />
//    public virtual Task OnDeactivateAsync(ApplicationLifecycleContext context)
//    {
//        Logger?.LogDebug("Application {AppId} deactivated", Metadata?.Id);
//        return Task.CompletedTask;
//    }
    
//    /// <inheritdoc />
//    public virtual Task<bool> OnCloseRequestAsync(ApplicationLifecycleContext context)
//    {
//        // By default, allow closing
//        return Task.FromResult(true);
//    }
    
//    /// <inheritdoc />
//    public virtual Task OnCloseAsync(ApplicationLifecycleContext context)
//    {
//        Logger?.LogInformation("Application {AppId} closed", Metadata?.Id);
//        return Task.CompletedTask;
//    }
    
//    /// <inheritdoc />
//    public virtual async Task SaveStateAsync(ApplicationLifecycleContext context)
//    {
//        if (FileSystem == null || User == null || Metadata == null)
//            return;
            
//        try
//        {
//            // Ensure state directory exists
//            if (!await FileSystem.ExistsAsync(StateDirectory))
//            {
//                await FileSystem.CreateDirectoryAsync(StateDirectory);
//            }
            
//            // Get state to save
//            var state = GetStateForSerialization();
//            if (state.Count == 0)
//                return;
                
//            // Serialize state
//            var json = JsonSerializer.Serialize(state);
            
//            // Save to file
//            await FileSystem.WriteFileAsync(StateFile, System.Text.Encoding.UTF8.GetBytes(json));
            
//            Logger?.LogDebug("Saved state for application {AppId}", Metadata.Id);
//        }
//        catch (Exception ex)
//        {
//            Logger?.LogError(ex, "Failed to save state for application {AppId}", Metadata?.Id);
//        }
//    }
    
//    /// <inheritdoc />
//    public virtual async Task LoadStateAsync(ApplicationLifecycleContext context)
//    {
//        if (FileSystem == null || User == null || Metadata == null)
//            return;
            
//        try
//        {
//            // Check if state file exists
//            if (!await FileSystem.ExistsAsync(StateFile))
//                return;
                
//            // Read state file
//            var bytes = await FileSystem.ReadFileAsync(StateFile);
//            if (bytes == null)
//                return;
//            var json = System.Text.Encoding.UTF8.GetString(bytes);
            
//            // Deserialize state
//            var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
//            if (state == null)
//                return;
                
//            // Apply state
//            await ApplyStateFromSerializationAsync(state);
            
//            Logger?.LogDebug("Loaded state for application {AppId}", Metadata.Id);
//        }
//        catch (Exception ex)
//        {
//            Logger?.LogError(ex, "Failed to load state for application {AppId}", Metadata?.Id);
//        }
//    }
    
//    /// <summary>
//    /// Get the application state for serialization
//    /// </summary>
//    /// <returns>Dictionary of state values</returns>
//    protected virtual Dictionary<string, object> GetStateForSerialization()
//    {
//        // Base implementation returns empty dictionary
//        // Override in derived classes to save application-specific state
//        return new Dictionary<string, object>();
//    }
    
//    /// <summary>
//    /// Apply state from serialization
//    /// </summary>
//    /// <param name="state">Deserialized state</param>
//    protected virtual Task ApplyStateFromSerializationAsync(Dictionary<string, JsonElement> state)
//    {
//        // Base implementation does nothing
//        // Override in derived classes to load application-specific state
//        return Task.CompletedTask;
//    }
//}
