using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorWindowManager.Components;
using BlazorWindowManager.Models;
using HackerOs.OS.Applications.Interfaces;
using HackerOs.OS.Kernel.Process;
using HackerOs.OS.User;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.UI.Windows
{
    /// <summary>
    /// Enhanced WindowBase component that implements IProcess, IApplication, and IApplicationEventSource
    /// This provides a foundation for window-based applications in HackerOS
    /// </summary>
    public partial class WindowBase : BlazorWindowManager.Components.WindowBase, IApplication, IProcess, IApplicationEventSource
    {
        [Inject] private IApplicationBridge ApplicationBridge { get; set; }
        [Inject] private ILogger<WindowBase> Logger { get; set; }

        #region Parameters

        /// <summary>
        /// The application ID
        /// </summary>
        [Parameter] public string ApplicationId { get; set; }

        /// <summary>
        /// The application description
        /// </summary>
        [Parameter] public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The application version
        /// </summary>
        [Parameter] public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// The application type
        /// </summary>
        [Parameter] public ApplicationType Type { get; set; } = ApplicationType.WindowedApplication;

        /// <summary>
        /// The application manifest
        /// </summary>
        [Parameter] public ApplicationManifest Manifest { get; set; }

        /// <summary>
        /// The parent process ID
        /// </summary>
        [Parameter] public int ParentProcessId { get; set; } = 1; // Default to init process

        /// <summary>
        /// The owner of the process
        /// </summary>
        [Parameter] public string Owner { get; set; } = "system";

        #endregion

        #region IApplication Implementation

        /// <inheritdoc />
        public string Id => ApplicationId ?? GetType().Name;

        /// <inheritdoc />
        string IApplication.Name => Title;

        /// <inheritdoc />
        public string? IconPath => Manifest?.IconPath;

        /// <inheritdoc />
        public ApplicationState State { get; private set; } = ApplicationState.Stopped;

        /// <inheritdoc />
        public UserSession? OwnerSession { get; private set; }

        /// <inheritdoc />
        public event EventHandler<ApplicationStateChangedEventArgs>? StateChanged;

        /// <inheritdoc />
        public event EventHandler<ApplicationOutputEventArgs>? OutputReceived;

        /// <inheritdoc />
        public event EventHandler<ApplicationErrorEventArgs>? ErrorReceived;

        /// <inheritdoc />
        public async Task<bool> StartAsync(ApplicationLaunchContext context)
        {
            try
            {
                if (State != ApplicationState.Stopped)
                {
                    Logger.LogWarning("Cannot start application {ApplicationId} in state {State}", Id, State);
                    return false;
                }

                // Update properties from context
                ProcessId = context.ProcessId;
                Owner = context.User?.Username ?? Owner;
                OwnerSession = context.UserSession;
                WorkingDirectory = context.WorkingDirectory ?? WorkingDirectory;
                CommandLine = context.CommandLine ?? CommandLine;
                StartTime = DateTime.UtcNow;

                // Register with the application bridge
                await ApplicationBridge.InitializeAsync(this, this);

                // Update state
                await SetApplicationStateAsync(ApplicationState.Starting);

                // Perform application-specific startup
                bool success = await OnStartAsync(context);

                if (success)
                {
                    // Set state to running
                    await SetApplicationStateAsync(ApplicationState.Running);
                    
                    // Show the window
                    await ShowAsync();
                    
                    return true;
                }
                else
                {
                    // Restore stopped state
                    await SetApplicationStateAsync(ApplicationState.Stopped);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error starting application {ApplicationId}", Id);
                await RaiseErrorReceivedAsync($"Failed to start application: {ex.Message}", ex);
                await SetApplicationStateAsync(ApplicationState.Crashed);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> StopAsync()
        {
            try
            {
                if (State == ApplicationState.Stopped || State == ApplicationState.Terminated)
                {
                    return true;
                }

                // Set stopping state
                await SetApplicationStateAsync(ApplicationState.Stopping);

                // Perform application-specific shutdown
                bool success = await OnStopAsync();

                if (success)
                {
                    // Hide the window
                    await HideAsync();
                    
                    // Set stopped state
                    await SetApplicationStateAsync(ApplicationState.Stopped);
                    return true;
                }
                else
                {
                    // Restore previous state
                    await SetApplicationStateAsync(State);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error stopping application {ApplicationId}", Id);
                await RaiseErrorReceivedAsync($"Failed to stop application: {ex.Message}", ex);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> PauseAsync()
        {
            try
            {
                if (State != ApplicationState.Running && State != ApplicationState.Maximized && State != ApplicationState.Minimized)
                {
                    return false;
                }

                // Perform application-specific pause
                bool success = await OnPauseAsync();

                if (success)
                {
                    // Set paused state
                    await SetApplicationStateAsync(ApplicationState.Paused);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error pausing application {ApplicationId}", Id);
                await RaiseErrorReceivedAsync($"Failed to pause application: {ex.Message}", ex);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ResumeAsync()
        {
            try
            {
                if (State != ApplicationState.Paused && State != ApplicationState.Suspended)
                {
                    return false;
                }

                // Perform application-specific resume
                bool success = await OnResumeAsync();

                if (success)
                {
                    // Set running state
                    await SetApplicationStateAsync(ApplicationState.Running);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error resuming application {ApplicationId}", Id);
                await RaiseErrorReceivedAsync($"Failed to resume application: {ex.Message}", ex);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> TerminateAsync()
        {
            try
            {
                // Force termination
                await SetApplicationStateAsync(ApplicationState.Terminated);
                
                // Close the window forcefully
                await CloseAsync();
                
                // Perform application-specific termination
                await OnTerminateAsync();

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during termination of {ApplicationId}", Id);
                await RaiseErrorReceivedAsync($"Error during termination: {ex.Message}", ex);
                
                // Still consider the application terminated
                await SetApplicationStateAsync(ApplicationState.Terminated);
                return true;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SendMessageAsync(object message)
        {
            try
            {
                return await OnMessageReceivedAsync(message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing message in {ApplicationId}", Id);
                await RaiseErrorReceivedAsync($"Error processing message: {ex.Message}", ex);
                return false;
            }
        }

        /// <inheritdoc />
        public ApplicationStatistics GetStatistics()
        {
            return new ApplicationStatistics
            {
                ProcessId = ProcessId,
                Name = Title,
                State = State,
                StartTime = StartTime,
                RunningTime = DateTime.UtcNow - StartTime,
                MemoryUsage = MemoryUsage,
                CpuTime = CpuTime
            };
        }

        #endregion

        #region IProcess Implementation

        /// <inheritdoc />
        public int ProcessId { get; private set; }

        /// <inheritdoc />
        int IProcess.ParentProcessId => ParentProcessId;

        /// <inheritdoc />
        string IProcess.Name => Title;

        /// <inheritdoc />
        string IProcess.Owner => Owner;

        /// <inheritdoc />
        public ProcessState State { get; private set; } = ProcessState.Creating;

        /// <inheritdoc />
        public DateTime StartTime { get; private set; } = DateTime.UtcNow;

        /// <inheritdoc />
        public long MemoryUsage { get; private set; } = 0;

        /// <inheritdoc />
        public TimeSpan CpuTime { get; private set; } = TimeSpan.Zero;

        /// <inheritdoc />
        public string CommandLine { get; private set; } = string.Empty;

        /// <inheritdoc />
        public string WorkingDirectory { get; set; } = "/";

        /// <inheritdoc />
        public Dictionary<string, string> Environment { get; private set; } = new();

        /// <inheritdoc />
        public async Task SendSignalAsync(ProcessSignal signal)
        {
            try
            {
                switch (signal)
                {
                    case ProcessSignal.SIGTERM:
                        await StopAsync();
                        break;
                    case ProcessSignal.SIGKILL:
                        await TerminateAsync();
                        break;
                    case ProcessSignal.SIGSTOP:
                        await PauseAsync();
                        break;
                    case ProcessSignal.SIGCONT:
                        await ResumeAsync();
                        break;
                    case ProcessSignal.SIGINT:
                        await OnInterruptAsync();
                        break;
                    default:
                        await OnSignalReceivedAsync(signal);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling signal {Signal} for {ApplicationId}", signal, Id);
                await RaiseErrorReceivedAsync($"Error handling signal {signal}: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public Task<int> WaitForExitAsync()
        {
            // Default implementation returns 0 (success)
            // Override in derived classes if needed
            return Task.FromResult(0);
        }

        #endregion

        #region IApplicationEventSource Implementation

        /// <inheritdoc />
        public async Task RaiseStateChangedAsync(ApplicationState oldState, ApplicationState newState)
        {
            var args = new ApplicationStateChangedEventArgs(this, oldState, newState);
            StateChanged?.Invoke(this, args);
            
            try
            {
                // Notify the application bridge
                if (ApplicationBridge != null)
                {
                    await ApplicationBridge.OnStateChangedAsync(this, oldState, newState);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error notifying bridge of state change for {ApplicationId}", Id);
            }
        }

        /// <inheritdoc />
        public async Task RaiseOutputReceivedAsync(string output, OutputStreamType streamType = OutputStreamType.StandardOutput)
        {
            var args = new ApplicationOutputEventArgs(this, output, streamType);
            OutputReceived?.Invoke(this, args);
            
            try
            {
                // Notify the application bridge
                if (ApplicationBridge != null)
                {
                    await ApplicationBridge.OnOutputAsync(this, output, streamType);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error notifying bridge of output for {ApplicationId}", Id);
            }
        }

        /// <inheritdoc />
        public async Task RaiseErrorReceivedAsync(string error, Exception? exception = null)
        {
            var args = new ApplicationErrorEventArgs(this, error, exception);
            ErrorReceived?.Invoke(this, args);
            
            try
            {
                // Notify the application bridge
                if (ApplicationBridge != null)
                {
                    await ApplicationBridge.OnErrorAsync(this, error, exception);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error notifying bridge of error for {ApplicationId}", Id);
            }
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Called when the application is started
        /// </summary>
        /// <param name="context">Application launch context</param>
        /// <returns>True if the application started successfully</returns>
        protected virtual Task<bool> OnStartAsync(ApplicationLaunchContext context) => Task.FromResult(true);

        /// <summary>
        /// Called when the application is stopped
        /// </summary>
        /// <returns>True if the application stopped successfully</returns>
        protected virtual Task<bool> OnStopAsync() => Task.FromResult(true);

        /// <summary>
        /// Called when the application is paused
        /// </summary>
        /// <returns>True if the application paused successfully</returns>
        protected virtual Task<bool> OnPauseAsync() => Task.FromResult(true);

        /// <summary>
        /// Called when the application is resumed
        /// </summary>
        /// <returns>True if the application resumed successfully</returns>
        protected virtual Task<bool> OnResumeAsync() => Task.FromResult(true);

        /// <summary>
        /// Called when the application is forcefully terminated
        /// </summary>
        protected virtual Task OnTerminateAsync() => Task.CompletedTask;

        /// <summary>
        /// Called when the application receives an interrupt signal
        /// </summary>
        protected virtual Task OnInterruptAsync() => StopAsync();

        /// <summary>
        /// Called when the application receives a custom signal
        /// </summary>
        /// <param name="signal">The signal received</param>
        protected virtual Task OnSignalReceivedAsync(ProcessSignal signal) => Task.CompletedTask;

        /// <summary>
        /// Called when the application receives a message
        /// </summary>
        /// <param name="message">The message received</param>
        /// <returns>True if the message was handled successfully</returns>
        protected virtual Task<bool> OnMessageReceivedAsync(object message) => Task.FromResult(false);

        #endregion

        #region Private Methods

        /// <summary>
        /// Sets the application state and raises the StateChanged event
        /// </summary>
        /// <param name="newState">The new state</param>
        private async Task SetApplicationStateAsync(ApplicationState newState)
        {
            var oldState = State;
            
            if (oldState != newState)
            {
                State = newState;
                
                // Update process state based on application state
                UpdateProcessState(newState);
                
                // Raise the state changed event
                await RaiseStateChangedAsync(oldState, newState);
            }
        }

        /// <summary>
        /// Updates the process state based on the application state
        /// </summary>
        /// <param name="appState">The application state</param>
        private void UpdateProcessState(ApplicationState appState)
        {
            // Map from application state to process state
            switch (appState)
            {
                case ApplicationState.Starting:
                    State = ProcessState.Creating;
                    break;
                case ApplicationState.Running:
                case ApplicationState.Minimized:
                case ApplicationState.Maximized:
                    State = ProcessState.Running;
                    break;
                case ApplicationState.Paused:
                case ApplicationState.WaitingForInput:
                    State = ProcessState.Waiting;
                    break;
                case ApplicationState.Suspended:
                    State = ProcessState.Sleeping;
                    break;
                case ApplicationState.Stopping:
                    State = ProcessState.Stopped;
                    break;
                case ApplicationState.Stopped:
                    State = ProcessState.Terminated;
                    break;
                case ApplicationState.Crashed:
                case ApplicationState.Terminated:
                    State = ProcessState.Zombie;
                    break;
            }
        }

        #endregion

        #region Lifecycle Methods

        /// <summary>
        /// Called when the component is initialized
        /// </summary>
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            
            // Handle window events
            OnClose += async () => await StopAsync();
            OnMinimize += async () => await SetApplicationStateAsync(ApplicationState.Minimized);
            OnMaximize += async () => await SetApplicationStateAsync(ApplicationState.Maximized);
            OnRestore += async () => await SetApplicationStateAsync(ApplicationState.Running);
        }

        /// <summary>
        /// Called when the component parameters are set
        /// </summary>
        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            
            // Ensure title is properly set from name if needed
            if (string.IsNullOrEmpty(Title) && !string.IsNullOrEmpty(Id))
            {
                Title = Id;
            }
        }

        /// <summary>
        /// Called when the component is disposed
        /// </summary>
        public override async ValueTask DisposeAsync()
        {
            // Ensure the application is stopped and unregistered
            if (State != ApplicationState.Stopped && State != ApplicationState.Terminated)
            {
                await StopAsync();
            }
            
            await base.DisposeAsync();
        }

        #endregion

        /// <inheritdoc />
        public async Task<bool> SetStateAsync(ApplicationState state)
        {
            try
            {
                // Use the existing private method for state management
                await SetApplicationStateAsync(state);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error setting application state to {State} for {ApplicationId}", state, Id);
                await RaiseErrorReceivedAsync($"Failed to set application state: {ex.Message}", ex);
                return false;
            }
        }
    }
}
