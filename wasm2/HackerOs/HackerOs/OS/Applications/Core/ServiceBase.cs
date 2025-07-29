using System;
using System.Threading;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Interfaces;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.Core
{
    /// <summary>
    /// Base class for all service applications in HackerOS.
    /// Services run in the background and don't have a user interface.
    /// </summary>
    public abstract class ServiceBase : ApplicationCoreBase
    {
        private CancellationTokenSource _cancellationTokenSource;
        private Task _backgroundTask;
        private readonly ILogger _logger;
        private bool _isInitialized = false;
        
        /// <summary>
        /// Gets whether the service is running in the background
        /// </summary>
        protected bool IsRunning { get; private set; }
        
        /// <summary>
        /// Gets whether the service should auto-restart on failure
        /// </summary>
        public bool AutoRestart { get; set; } = false;
        
        /// <summary>
        /// Gets or sets the interval between auto-restart attempts in milliseconds
        /// </summary>
        public int RestartInterval { get; set; } = 5000;
        
        /// <summary>
        /// Gets or sets the maximum number of restart attempts
        /// </summary>
        public int MaxRestartAttempts { get; set; } = 3;
        
        /// <summary>
        /// Gets the current number of restart attempts
        /// </summary>
        protected int RestartAttempts { get; private set; } = 0;
        
        /// <summary>
        /// Creates a new ServiceBase
        /// </summary>
        /// <param name="logger">Logger for this service</param>
        protected ServiceBase(ILogger logger)
        {
            _logger = logger;
            Type = ApplicationType.ServiceApplication;
            _cancellationTokenSource = new CancellationTokenSource();
        }
        
        /// <summary>
        /// Initializes the service
        /// </summary>
        /// <returns>True if initialization was successful</returns>
        protected virtual Task<bool> InitializeAsync()
        {
            return Task.FromResult(true);
        }
        
        /// <summary>
        /// Main worker method for the service that runs in the background
        /// This should be implemented by derived classes to provide the service's functionality
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to monitor for stopping the service</param>
        protected abstract Task DoWorkAsync(CancellationToken cancellationToken);
        
        /// <summary>
        /// Cleans up resources when the service is stopping
        /// </summary>
        protected virtual Task CleanupAsync()
        {
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Loads configuration for the service
        /// </summary>
        /// <returns>True if configuration was loaded successfully</returns>
        protected virtual Task<bool> LoadConfigurationAsync()
        {
            return Task.FromResult(true);
        }
        
        /// <summary>
        /// Saves configuration for the service
        /// </summary>
        /// <returns>True if configuration was saved successfully</returns>
        protected virtual Task<bool> SaveConfigurationAsync()
        {
            return Task.FromResult(true);
        }
        
        /// <inheritdoc />
        protected override async Task<bool> OnStartAsync(ApplicationLaunchContext context)
        {
            try
            {
                // Initialize if not already initialized
                if (!_isInitialized)
                {
                    if (!await InitializeAsync())
                    {
                        await RaiseErrorReceivedAsync($"Failed to initialize service {Name}");
                        return false;
                    }
                    
                    if (!await LoadConfigurationAsync())
                    {
                        await RaiseErrorReceivedAsync($"Failed to load configuration for service {Name}");
                        return false;
                    }
                    
                    _isInitialized = true;
                }
                
                // Create new cancellation token source
                _cancellationTokenSource = new CancellationTokenSource();
                
                // Start background task
                _backgroundTask = Task.Run(() => RunServiceAsync(_cancellationTokenSource.Token));
                
                await RaiseOutputReceivedAsync($"Service {Name} started");
                return true;
            }
            catch (Exception ex)
            {
                await RaiseErrorReceivedAsync($"Error starting service {Name}: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <inheritdoc />
        protected override async Task<bool> OnStopAsync()
        {
            try
            {
                // Signal cancellation to the background task
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                }
                
                // Wait for the background task to complete
                if (_backgroundTask != null)
                {
                    await Task.WhenAny(_backgroundTask, Task.Delay(5000)); // Wait with timeout
                }
                
                // Cleanup
                await CleanupAsync();
                
                // Save configuration
                await SaveConfigurationAsync();
                
                // Reset restart counter
                RestartAttempts = 0;
                
                await RaiseOutputReceivedAsync($"Service {Name} stopped");
                return true;
            }
            catch (Exception ex)
            {
                await RaiseErrorReceivedAsync($"Error stopping service {Name}: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <inheritdoc />
        protected override async Task<bool> OnPauseAsync()
        {
            try
            {
                // Signal cancellation to the background task
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                }
                
                // Wait for the background task to complete
                if (_backgroundTask != null)
                {
                    await Task.WhenAny(_backgroundTask, Task.Delay(5000)); // Wait with timeout
                }
                
                IsRunning = false;
                await RaiseOutputReceivedAsync($"Service {Name} paused");
                return true;
            }
            catch (Exception ex)
            {
                await RaiseErrorReceivedAsync($"Error pausing service {Name}: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <inheritdoc />
        protected override async Task<bool> OnResumeAsync()
        {
            try
            {
                // Create new cancellation token source
                _cancellationTokenSource = new CancellationTokenSource();
                
                // Start background task
                _backgroundTask = Task.Run(() => RunServiceAsync(_cancellationTokenSource.Token));
                
                await RaiseOutputReceivedAsync($"Service {Name} resumed");
                return true;
            }
            catch (Exception ex)
            {
                await RaiseErrorReceivedAsync($"Error resuming service {Name}: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <inheritdoc />
        protected override async Task OnTerminateAsync()
        {
            // Force cancellation of the background task
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
            
            // Cleanup resources
            await CleanupAsync();
            
            // Save configuration
            await SaveConfigurationAsync();
            
            await RaiseOutputReceivedAsync($"Service {Name} terminated");
        }
        
        /// <summary>
        /// Runs the service and handles auto-restart
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        private async Task RunServiceAsync(CancellationToken cancellationToken)
        {
            IsRunning = true;
            
            try
            {
                // Run the service worker until cancellation is requested
                await DoWorkAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, do nothing
            }
            catch (Exception ex)
            {
                await RaiseErrorReceivedAsync($"Service {Name} encountered an error: {ex.Message}", ex);
                
                // Handle auto-restart logic
                if (AutoRestart && RestartAttempts < MaxRestartAttempts)
                {
                    RestartAttempts++;
                    await RaiseOutputReceivedAsync($"Auto-restarting service {Name} (attempt {RestartAttempts}/{MaxRestartAttempts})");
                    
                    // Wait before restart
                    await Task.Delay(RestartInterval, CancellationToken.None);
                    
                    // Create new cancellation token source
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        _cancellationTokenSource = new CancellationTokenSource();
                        _backgroundTask = Task.Run(() => RunServiceAsync(_cancellationTokenSource.Token));
                    }
                }
                else if (AutoRestart && RestartAttempts >= MaxRestartAttempts)
                {
                    await RaiseErrorReceivedAsync($"Service {Name} failed after {MaxRestartAttempts} restart attempts");
                    State = ApplicationState.Crashed;
                }
                else
                {
                    State = ApplicationState.Crashed;
                }
            }
            finally
            {
                IsRunning = false;
            }
        }
    }
}
