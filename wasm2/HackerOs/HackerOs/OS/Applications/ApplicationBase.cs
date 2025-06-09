using BlazorWindowManager.Components;
using HackerOs.OS.User;
using System.Diagnostics;

namespace HackerOs.OS.Applications;

/// <summary>
/// Base class for all applications in HackerOS
/// </summary>
public abstract class ApplicationBase : WindowBase, IApplication
{
    private ApplicationState _state = ApplicationState.Stopped;
    private readonly object _stateLock = new();
    private ApplicationStatistics? _statistics;
    private CancellationTokenSource? _cancellationTokenSource;    /// <inheritdoc />
    public new virtual string Id { get; protected set; } = string.Empty;

    /// <inheritdoc />
    public new virtual string Name { get; protected set; } = string.Empty;

    /// <inheritdoc />
    public virtual string Description { get; protected set; } = string.Empty;

    /// <inheritdoc />
    public virtual string Version { get; protected set; } = "1.0.0";

    /// <inheritdoc />
    public virtual ApplicationType Type { get; protected set; } = ApplicationType.WindowedApplication;

    /// <inheritdoc />
    public ApplicationManifest Manifest { get; protected set; } = new()
    {
        Id = "unknown",
        Name = "Unknown Application",
        Version = "1.0.0",
        EntryPoint = "unknown"
    };

    /// <inheritdoc />
    public ApplicationState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
        private set
        {
            ApplicationState oldState;
            lock (_stateLock)
            {
                oldState = _state;
                _state = value;
            }

            if (oldState != value)
            {
                _statistics?.UpdateActivity();
                if (_statistics != null)
                    _statistics.State = value;

                StateChanged?.Invoke(this, new ApplicationStateChangedEventArgs(this, oldState, value));
            }
        }
    }    /// <inheritdoc />
    public UserSession? OwnerSession { get; protected set; }

    /// <inheritdoc />
    public int ProcessId { get; protected set; }

    /// <summary>
    /// Application launch context
    /// </summary>
    public ApplicationLaunchContext? Context { get; protected set; }

    /// <inheritdoc />
    public event EventHandler<ApplicationStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public event EventHandler<ApplicationOutputEventArgs>? OutputReceived;

    /// <inheritdoc />
    public event EventHandler<ApplicationErrorEventArgs>? ErrorReceived;

    /// <summary>
    /// Cancellation token for application operations
    /// </summary>
    protected CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;    /// <summary>
    /// Initialize the application with a manifest
    /// </summary>
    /// <param name="manifest">Application manifest</param>
    protected ApplicationBase(ApplicationManifest manifest)
    {
        Manifest = manifest;
        Id = manifest.Id;
        Name = manifest.Name;
        Description = manifest.Description;
        Version = manifest.Version;
        Type = manifest.Type;

        // Generate a unique process ID
        ProcessId = Environment.TickCount + Random.Shared.Next(1000, 9999);
    }

    /// <summary>
    /// Initialize the application with default manifest
    /// </summary>
    protected ApplicationBase()
    {
        var typeName = GetType().Name;
        Manifest = new ApplicationManifest
        {
            Id = typeName.ToLowerInvariant(),
            Name = typeName,
            Description = $"{typeName} application",
            Version = "1.0.0",
            EntryPoint = typeName,
            Type = ApplicationType.WindowedApplication
        };
        
        Id = Manifest.Id;
        Name = Manifest.Name;
        Description = Manifest.Description;
        Version = Manifest.Version;
        Type = Manifest.Type;

        // Generate a unique process ID
        ProcessId = Environment.TickCount + Random.Shared.Next(1000, 9999);
    }

    /// <inheritdoc />
    public virtual async Task<bool> StartAsync(ApplicationLaunchContext context)
    {
        try
        {
            if (State != ApplicationState.Stopped)
            {
                OnError("Application is already running or starting", ErrorSeverity.Warning);
                return false;
            }            State = ApplicationState.Starting;
            OwnerSession = context.UserSession;
            Context = context;
            _cancellationTokenSource = new CancellationTokenSource();

            InitializeStatistics();

            // Call the derived class implementation
            var result = await OnStartAsync(context);

            if (result)
            {
                State = ApplicationState.Running;
                OnOutput($"Application {Name} started successfully", OutputStreamType.StandardOutput);
            }
            else
            {
                State = ApplicationState.Stopped;
                OnError("Failed to start application", ErrorSeverity.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            State = ApplicationState.Crashed;
            OnError($"Exception during application startup: {ex.Message}", ErrorSeverity.Fatal, ex);
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> StopAsync()
    {
        try
        {
            if (State == ApplicationState.Stopped)
                return true;

            if (State == ApplicationState.Stopping)
                return false;

            State = ApplicationState.Stopping;

            // Cancel any ongoing operations
            _cancellationTokenSource?.Cancel();

            // Call the derived class implementation
            var result = await OnStopAsync();

            State = ApplicationState.Stopped;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            OnOutput($"Application {Name} stopped", OutputStreamType.StandardOutput);
            return result;
        }
        catch (Exception ex)
        {
            State = ApplicationState.Crashed;
            OnError($"Exception during application shutdown: {ex.Message}", ErrorSeverity.Fatal, ex);
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> PauseAsync()
    {
        try
        {
            if (State != ApplicationState.Running)
                return false;            var result = await OnPauseAsync();
            if (result)
            {
                State = ApplicationState.Paused;
                if (_statistics != null)
                {
                    _statistics.CustomMetrics.TryGetValue("PauseCount", out var pauseCountObj);
                    var pauseCount = pauseCountObj as int? ?? 0;
                    _statistics.CustomMetrics["PauseCount"] = pauseCount + 1;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            OnError($"Exception during application pause: {ex.Message}", ErrorSeverity.Error, ex);
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> ResumeAsync()
    {
        try
        {
            if (State != ApplicationState.Paused)
                return false;

            var result = await OnResumeAsync();
            if (result)
            {
                State = ApplicationState.Running;
            }

            return result;
        }
        catch (Exception ex)
        {
            OnError($"Exception during application resume: {ex.Message}", ErrorSeverity.Error, ex);
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> TerminateAsync()
    {
        try
        {
            State = ApplicationState.Stopping;
            _cancellationTokenSource?.Cancel();

            await OnTerminateAsync();

            State = ApplicationState.Stopped;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            OnOutput($"Application {Name} terminated", OutputStreamType.StandardOutput);
            return true;
        }
        catch (Exception ex)
        {
            State = ApplicationState.Crashed;
            OnError($"Exception during application termination: {ex.Message}", ErrorSeverity.Fatal, ex);
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> SendMessageAsync(object message)
    {
        try
        {
            return await OnMessageReceivedAsync(message);
        }
        catch (Exception ex)
        {
            OnError($"Exception handling message: {ex.Message}", ErrorSeverity.Error, ex);
            return false;
        }
    }

    /// <inheritdoc />
    public virtual ApplicationStatistics GetStatistics()
    {
        return _statistics ?? new ApplicationStatistics
        {
            ApplicationId = Id,
            ProcessId = ProcessId,
            State = State
        };
    }

    /// <summary>
    /// Called when the application should start
    /// </summary>
    /// <param name="context">Launch context</param>
    /// <returns>True if started successfully</returns>
    protected abstract Task<bool> OnStartAsync(ApplicationLaunchContext context);

    /// <summary>
    /// Called when the application should stop
    /// </summary>
    /// <returns>True if stopped successfully</returns>
    protected abstract Task<bool> OnStopAsync();

    /// <summary>
    /// Called when the application should pause
    /// </summary>
    /// <returns>True if paused successfully</returns>
    protected virtual Task<bool> OnPauseAsync()
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Called when the application should resume
    /// </summary>
    /// <returns>True if resumed successfully</returns>
    protected virtual Task<bool> OnResumeAsync()
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Called when the application should terminate immediately
    /// </summary>
    protected virtual Task OnTerminateAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a message is sent to the application
    /// </summary>
    /// <param name="message">Message to process</param>
    /// <returns>True if message was handled</returns>
    protected virtual Task<bool> OnMessageReceivedAsync(object message)
    {
        return Task.FromResult(false);
    }    /// <summary>
    /// Send output from the application
    /// </summary>
    /// <param name="text">Output text</param>
    /// <param name="streamType">Stream type</param>
    protected void OnOutput(string text, OutputStreamType streamType = OutputStreamType.StandardOutput)
    {
        _statistics?.UpdateActivity();
        OutputReceived?.Invoke(this, new ApplicationOutputEventArgs(this, text, streamType));
    }

    /// <summary>
    /// Send output from the application asynchronously
    /// </summary>
    /// <param name="text">Output text</param>
    /// <param name="streamType">Stream type</param>
    protected Task OnOutputAsync(string text, OutputStreamType streamType = OutputStreamType.StandardOutput)
    {
        OnOutput(text, streamType);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Report an error from the application
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="severity">Error severity</param>
    /// <param name="exception">Exception details</param>
    protected void OnError(string message, ErrorSeverity severity, Exception? exception = null)
    {
        _statistics?.UpdateActivity();
        
        if (_statistics != null)
        {
            if (severity >= ErrorSeverity.Error)
                _statistics.ErrorCount++;
            else if (severity == ErrorSeverity.Warning)
                _statistics.WarningCount++;
        }

        ErrorReceived?.Invoke(this, new ApplicationErrorEventArgs(this, message, severity, exception));
    }

    /// <summary>
    /// Report an error from the application asynchronously
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="severity">Error severity</param>
    /// <param name="exception">Exception details</param>
    protected Task OnErrorAsync(string message, ErrorSeverity severity = ErrorSeverity.Error, Exception? exception = null)
    {
        OnError(message, severity, exception);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Initialize application statistics
    /// </summary>
    private void InitializeStatistics()
    {
        _statistics = new ApplicationStatistics
        {
            ApplicationId = Id,
            ProcessId = ProcessId,
            StartTime = DateTime.UtcNow,
            State = State
        };
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public virtual void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}
