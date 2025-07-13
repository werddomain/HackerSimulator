using System;
using System.Threading.Tasks;
using HackerOs.OS.Kernel.Process;

namespace HackerOs.OS.Applications.Interfaces
{
    /// <summary>
    /// Interface for bridging WindowBase with ApplicationCoreBase functionality.
    /// This bridge enables a component to act as both a UI element and a process/application.
    /// </summary>
    public interface IApplicationBridge
    {
        /// <summary>
        /// Initializes the bridge with process and application interfaces
        /// </summary>
        /// <param name="process">Process interface</param>
        /// <param name="application">Application interface</param>
        Task<bool> InitializeAsync(IProcess process, IApplication application);

        /// <summary>
        /// Registers an application with the application manager
        /// </summary>
        /// <param name="application">Application to register</param>
        /// <returns>True if registration was successful</returns>
        Task<bool> RegisterApplicationAsync(IApplication application);

        /// <summary>
        /// Unregisters an application from the application manager
        /// </summary>
        /// <param name="application">Application to unregister</param>
        /// <returns>True if unregistration was successful</returns>
        Task<bool> UnregisterApplicationAsync(IApplication application);

        /// <summary>
        /// Registers a process with the process manager
        /// </summary>
        /// <param name="process">Process to register</param>
        /// <param name="processId">Optional specific process ID to request</param>
        /// <returns>True if registration was successful</returns>
        Task<bool> RegisterProcessAsync(IProcess process, int? processId = null);

        /// <summary>
        /// Terminates a process with the process manager
        /// </summary>
        /// <param name="process">Process to terminate</param>
        /// <returns>True if termination was successful</returns>
        Task<bool> TerminateProcessAsync(IProcess process);

        /// <summary>
        /// Handles application state changes and propagates them
        /// </summary>
        /// <param name="application">Application that changed state</param>
        /// <param name="oldState">Previous application state</param>
        /// <param name="newState">New application state</param>
        Task OnStateChangedAsync(IApplication application, ApplicationState oldState, ApplicationState newState);

        /// <summary>
        /// Handles application output and propagates it
        /// </summary>
        /// <param name="application">Application that produced output</param>
        /// <param name="output">Output text</param>
        /// <param name="streamType">Type of output stream</param>
        Task OnOutputAsync(IApplication application, string output, OutputStreamType streamType);

        /// <summary>
        /// Handles application errors and propagates them
        /// </summary>
        /// <param name="application">Application that produced error</param>
        /// <param name="error">Error message</param>
        /// <param name="exception">Optional exception that caused the error</param>
        Task OnErrorAsync(IApplication application, string error, Exception? exception = null);
    }
}
