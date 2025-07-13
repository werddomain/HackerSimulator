using System;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Interfaces;
using HackerOs.OS.Kernel.Process;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.Core
{
    /// <summary>
    /// Implementation of the IApplicationBridge interface that connects WindowBase components
    /// with the ApplicationManager and ProcessManager.
    /// </summary>
    public class ApplicationBridge : IApplicationBridge
    {
        private readonly IApplicationManager _applicationManager;
        private readonly IProcessManager _processManager;
        private readonly ILogger<ApplicationBridge> _logger;

        /// <summary>
        /// Creates a new ApplicationBridge
        /// </summary>
        /// <param name="applicationManager">Application manager service</param>
        /// <param name="processManager">Process manager service</param>
        /// <param name="logger">Logger instance</param>
        public ApplicationBridge(
            IApplicationManager applicationManager,
            IProcessManager processManager,
            ILogger<ApplicationBridge> logger)
        {
            _applicationManager = applicationManager;
            _processManager = processManager;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<bool> InitializeAsync(IProcess process, IApplication application)
        {
            try
            {
                _logger.LogDebug("Initializing application bridge for {AppId}", application.Id);

                // Subscribe to application events
                if (application is IApplicationEventSource eventSource)
                {
                    eventSource.StateChanged += async (sender, e) => 
                        await OnStateChangedAsync(e.Application, e.OldState, e.NewState);
                        
                    eventSource.OutputReceived += async (sender, e) => 
                        await OnOutputAsync(e.Application, e.Output, e.StreamType);
                        
                    eventSource.ErrorReceived += async (sender, e) => 
                        await OnErrorAsync(e.Application, e.ErrorMessage, e.Exception);
                }

                // Register with managers
                bool processRegistered = await RegisterProcessAsync(process);
                bool appRegistered = await RegisterApplicationAsync(application);

                return processRegistered && appRegistered;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize application bridge for {AppId}", application.Id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RegisterApplicationAsync(IApplication application)
        {
            try
            {
                _logger.LogDebug("Registering application {AppId} with application manager", application.Id);
                return await _applicationManager.RegisterApplicationInstanceAsync(application);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register application {AppId}", application.Id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UnregisterApplicationAsync(IApplication application)
        {
            try
            {
                _logger.LogDebug("Unregistering application {AppId} from application manager", application.Id);
                return await _applicationManager.UnregisterApplicationInstanceAsync(application);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unregister application {AppId}", application.Id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RegisterProcessAsync(IProcess process, int? processId = null)
        {
            try
            {
                _logger.LogDebug("Registering process {ProcessName} with process manager", process.Name);
                
                // Create process start info
                var startInfo = new ProcessStartInfo
                {
                    Name = process.Name,
                    Owner = process.Owner,
                    ParentProcessId = process.ParentProcessId,
                    WorkingDirectory = process.WorkingDirectory,
                    Environment = process.Environment
                };
                
                // Register with process manager
                var pid = processId.HasValue
                    ? await _processManager.CreateProcessWithIdAsync(startInfo, processId.Value)
                    : await _processManager.CreateProcessAsync(startInfo);
                    
                return pid > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register process {ProcessName}", process.Name);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> TerminateProcessAsync(IProcess process)
        {
            try
            {
                _logger.LogDebug("Terminating process {ProcessId}", process.ProcessId);
                return await _processManager.TerminateProcessAsync(process.ProcessId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to terminate process {ProcessId}", process.ProcessId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task OnStateChangedAsync(IApplication application, ApplicationState oldState, ApplicationState newState)
        {
            try
            {
                _logger.LogDebug("Application {AppId} state changed from {OldState} to {NewState}", 
                    application.Id, oldState, newState);

                // Notify application manager of state change
                await _applicationManager.NotifyApplicationStateChangedAsync(application, oldState, newState);
                
                // Handle special states
                if (newState == ApplicationState.Terminated)
                {
                    // Automatically unregister terminated applications
                    await UnregisterApplicationAsync(application);
                    await TerminateProcessAsync(application as IProcess);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling state change for application {AppId}", application.Id);
            }
        }

        /// <inheritdoc />
        public async Task OnOutputAsync(IApplication application, string output, OutputStreamType streamType)
        {
            try
            {
                _logger.LogTrace("Application {AppId} output ({StreamType}): {Output}", 
                    application.Id, streamType, output);

                // Could log or route output to appropriate destinations
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling output from application {AppId}", application.Id);
            }
        }

        /// <inheritdoc />
        public async Task OnErrorAsync(IApplication application, string error, Exception? exception = null)
        {
            try
            {
                _logger.LogWarning("Application {AppId} error: {Error}", application.Id, error);
                
                // Could log or route errors to appropriate destinations
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling error from application {AppId}", application.Id);
            }
        }
    }
}
