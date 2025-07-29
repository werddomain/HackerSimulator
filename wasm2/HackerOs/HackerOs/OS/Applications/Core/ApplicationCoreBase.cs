using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Interfaces;
using HackerOs.OS.Kernel.Process;
using HackerOs.OS.User;

namespace HackerOs.OS.Applications.Core
{
    /// <summary>
    /// Base class for all applications in HackerOS that implements both IProcess and IApplication interfaces.
    /// This class provides the foundation for all application types (window, service, command).
    /// </summary>
    public abstract class ApplicationCoreBase : IProcess, IApplication, IApplicationEventSource
    {
        #region IProcess Implementation

        /// <inheritdoc />
        public int ProcessId { get; protected set; }

        /// <inheritdoc />
        public int ParentProcessId { get; protected set; }

        /// <inheritdoc />
        public string Name { get; protected set; } = string.Empty;

        /// <inheritdoc />
        public string Owner { get; protected set; } = string.Empty;

        /// <inheritdoc />
        public ProcessState State { get; protected set; } = ProcessState.Creating;

        /// <inheritdoc />
        public DateTime StartTime { get; protected set; }

        /// <inheritdoc />
        public long MemoryUsage { get; protected set; }

        /// <inheritdoc />
        public TimeSpan CpuTime { get; protected set; }

        /// <inheritdoc />
        public string CommandLine { get; protected set; } = string.Empty;

        /// <inheritdoc />
        public string WorkingDirectory { get; set; } = "/";

        /// <inheritdoc />
        public Dictionary<string, string> Environment { get; protected set; } = new();

        /// <inheritdoc />
        public virtual async Task SendSignalAsync(ProcessSignal signal)
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

        /// <inheritdoc />
        public virtual Task<int> WaitForExitAsync()
        {
            // Default implementation returns 0 (success)
            // Subclasses should override this to provide actual exit code
            return Task.FromResult(0);
        }

        #endregion

        #region IApplication Implementation

        /// <inheritdoc />
        public string Id { get; protected set; } = string.Empty;

        /// <inheritdoc />
        public string Description { get; protected set; } = string.Empty;

        /// <inheritdoc />
        public string Version { get; protected set; } = "1.0.0";

        /// <inheritdoc />
        public string? IconPath { get; protected set; }

        /// <inheritdoc />
        public ApplicationType Type { get; protected set; }

        /// <inheritdoc />
        public ApplicationManifest Manifest { get; protected set; } = new();

        /// <inheritdoc />
        public ApplicationState State { 
            get => GetApplicationState(); 
            protected set => SetApplicationState(value); 
        }

        /// <inheritdoc />
        public UserSession? OwnerSession { get; protected set; }

        /// <inheritdoc />
        public event EventHandler<ApplicationStateChangedEventArgs>? StateChanged;

        /// <inheritdoc />
        public event EventHandler<ApplicationOutputEventArgs>? OutputReceived;

        /// <inheritdoc />
        public event EventHandler<ApplicationErrorEventArgs>? ErrorReceived;

        /// <inheritdoc />
        public virtual async Task<bool> StartAsync(ApplicationLaunchContext context)
        {
            try
            {
                if (State != ApplicationState.Stopped)
                {
                    // Application is already running or in a non-stoppable state
                    await RaiseErrorReceivedAsync($"Cannot start application {Name} in state {State}");
                    return false;
                }

                // Set process and application info from context
                ProcessId = context.ProcessId;
                ParentProcessId = context.ParentProcessId;
                Owner = context.User?.Username ?? "system";
                OwnerSession = context.UserSession;
                WorkingDirectory = context.WorkingDirectory ?? "/";
                CommandLine = context.CommandLine ?? string.Empty;
                StartTime = DateTime.UtcNow;

                // Set starting state
                var oldState = State;
                State = ApplicationState.Starting;

                // Call implementation-specific startup
                bool success = await OnStartAsync(context);

                if (success)
                {
                    // Set running state on success
                    State = ApplicationState.Running;
                    return true;
                }
                else
                {
                    // Restore stopped state on failure
                    State = ApplicationState.Stopped;
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Handle startup exceptions
                await RaiseErrorReceivedAsync($"Error starting application {Name}: {ex.Message}", ex);
                State = ApplicationState.Crashed;
                return false;
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> StopAsync()
        {
            try
            {
                if (State == ApplicationState.Stopped || State == ApplicationState.Terminated)
                {
                    // Already stopped
                    return true;
                }

                // Set stopping state
                var oldState = State;
                State = ApplicationState.Stopping;

                // Call implementation-specific shutdown
                bool success = await OnStopAsync();

                if (success)
                {
                    // Set stopped state on success
                    State = ApplicationState.Stopped;
                    return true;
                }
                else
                {
                    // Restore previous state on failure
                    State = oldState;
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Handle shutdown exceptions
                await RaiseErrorReceivedAsync($"Error stopping application {Name}: {ex.Message}", ex);
                State = ApplicationState.Crashed;
                return false;
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> PauseAsync()
        {
            try
            {
                if (State != ApplicationState.Running)
                {
                    // Can only pause running applications
                    await RaiseErrorReceivedAsync($"Cannot pause application {Name} in state {State}");
                    return false;
                }

                // Call implementation-specific pause logic
                bool success = await OnPauseAsync();

                if (success)
                {
                    // Set paused state on success
                    var oldState = State;
                    State = ApplicationState.Paused;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                // Handle pause exceptions
                await RaiseErrorReceivedAsync($"Error pausing application {Name}: {ex.Message}", ex);
                return false;
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> ResumeAsync()
        {
            try
            {
                if (State != ApplicationState.Paused)
                {
                    // Can only resume paused applications
                    await RaiseErrorReceivedAsync($"Cannot resume application {Name} in state {State}");
                    return false;
                }

                // Call implementation-specific resume logic
                bool success = await OnResumeAsync();

                if (success)
                {
                    // Set running state on success
                    var oldState = State;
                    State = ApplicationState.Running;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                // Handle resume exceptions
                await RaiseErrorReceivedAsync($"Error resuming application {Name}: {ex.Message}", ex);
                return false;
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> TerminateAsync()
        {
            // Force termination, regardless of state
            try
            {
                var oldState = State;
                State = ApplicationState.Terminated;

                // Call implementation-specific terminate logic
                await OnTerminateAsync();

                return true;
            }
            catch (Exception ex)
            {
                // Even if termination fails, consider the application terminated
                await RaiseErrorReceivedAsync($"Error during termination of {Name}: {ex.Message}", ex);
                State = ApplicationState.Terminated;
                return true;
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
                await RaiseErrorReceivedAsync($"Error processing message in {Name}: {ex.Message}", ex);
                return false;
            }
        }

        /// <inheritdoc />
        public virtual ApplicationStatistics GetStatistics()
        {
            return new ApplicationStatistics
            {
                ProcessId = ProcessId,
                Name = Name,
                State = State,
                StartTime = StartTime,
                RunningTime = DateTime.UtcNow - StartTime,
                MemoryUsage = MemoryUsage,
                CpuTime = CpuTime
            };
        }

        #endregion

        #region IApplicationEventSource Implementation

        /// <inheritdoc />
        public virtual Task RaiseStateChangedAsync(ApplicationState oldState, ApplicationState newState)
        {
            var args = new ApplicationStateChangedEventArgs(this, oldState, newState);
            StateChanged?.Invoke(this, args);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual Task RaiseOutputReceivedAsync(string output, OutputStreamType streamType = OutputStreamType.StandardOutput)
        {
            var args = new ApplicationOutputEventArgs(this, output, streamType);
            OutputReceived?.Invoke(this, args);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual Task RaiseErrorReceivedAsync(string error, Exception? exception = null)
        {
            var args = new ApplicationErrorEventArgs(this, error, exception);
            ErrorReceived?.Invoke(this, args);
            return Task.CompletedTask;
        }

        #endregion

        #region Protected Abstract Methods

        /// <summary>
        /// Implementation-specific startup logic
        /// </summary>
        /// <param name="context">Application launch context</param>
        /// <returns>True if started successfully</returns>
        protected abstract Task<bool> OnStartAsync(ApplicationLaunchContext context);

        /// <summary>
        /// Implementation-specific shutdown logic
        /// </summary>
        /// <returns>True if stopped successfully</returns>
        protected abstract Task<bool> OnStopAsync();

        #endregion

        #region Protected Virtual Methods

        /// <summary>
        /// Implementation-specific pause logic
        /// </summary>
        /// <returns>True if paused successfully</returns>
        protected virtual Task<bool> OnPauseAsync() => Task.FromResult(true);

        /// <summary>
        /// Implementation-specific resume logic
        /// </summary>
        /// <returns>True if resumed successfully</returns>
        protected virtual Task<bool> OnResumeAsync() => Task.FromResult(true);

        /// <summary>
        /// Implementation-specific termination logic
        /// </summary>
        /// <returns>Task representing completion</returns>
        protected virtual Task OnTerminateAsync() => Task.CompletedTask;

        /// <summary>
        /// Implementation-specific interrupt handling (SIGINT)
        /// </summary>
        /// <returns>Task representing completion</returns>
        protected virtual Task OnInterruptAsync() => StopAsync();

        /// <summary>
        /// Implementation-specific signal handling for custom signals
        /// </summary>
        /// <param name="signal">Process signal received</param>
        /// <returns>Task representing completion</returns>
        protected virtual Task OnSignalReceivedAsync(ProcessSignal signal) => Task.CompletedTask;

        /// <summary>
        /// Implementation-specific message handling
        /// </summary>
        /// <param name="message">Message object</param>
        /// <returns>True if message was handled successfully</returns>
        protected virtual Task<bool> OnMessageReceivedAsync(object message) => Task.FromResult(false);

        #endregion

        #region Private Methods

        /// <summary>
        /// Maps process state to application state if necessary
        /// </summary>
        private ApplicationState GetApplicationState()
        {
            // If process state doesn't match application state, map between them
            return State;
        }

        /// <summary>
        /// Sets application state and raises the StateChanged event
        /// </summary>
        private void SetApplicationState(ApplicationState newState)
        {
            var oldState = State;

            if (oldState != newState)
            {
                // Update process state based on application state
                UpdateProcessState(newState);

                // Raise state changed event
                RaiseStateChangedAsync(oldState, newState).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Updates process state based on application state
        /// </summary>
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
    }
}
