using System;
using System.Threading.Tasks;

namespace HackerOs.OS.Applications.Interfaces
{
    /// <summary>
    /// Interface for standardized application event handling
    /// </summary>
    public interface IApplicationEventSource
    {
        /// <summary>
        /// Event raised when application state changes
        /// </summary>
        event EventHandler<ApplicationStateChangedEventArgs>? StateChanged;

        /// <summary>
        /// Event raised when application produces output
        /// </summary>
        event EventHandler<ApplicationOutputEventArgs>? OutputReceived;

        /// <summary>
        /// Event raised when application encounters an error
        /// </summary>
        event EventHandler<ApplicationErrorEventArgs>? ErrorReceived;

        /// <summary>
        /// Raises the StateChanged event
        /// </summary>
        /// <param name="oldState">Previous application state</param>
        /// <param name="newState">New application state</param>
        Task RaiseStateChangedAsync(ApplicationState oldState, ApplicationState newState);

        /// <summary>
        /// Raises the OutputReceived event
        /// </summary>
        /// <param name="output">Output text</param>
        /// <param name="streamType">Type of output stream (stdout, stderr, etc.)</param>
        Task RaiseOutputReceivedAsync(string output, OutputStreamType streamType = OutputStreamType.StandardOutput);

        /// <summary>
        /// Raises the ErrorReceived event
        /// </summary>
        /// <param name="error">Error message</param>
        /// <param name="exception">Optional exception that caused the error</param>
        Task RaiseErrorReceivedAsync(string error, Exception? exception = null);
    }

    /// <summary>
    /// Types of output streams
    /// </summary>
    public enum OutputStreamType
    {
        /// <summary>
        /// Standard output stream (stdout)
        /// </summary>
        StandardOutput,

        /// <summary>
        /// Standard error stream (stderr)
        /// </summary>
        StandardError,

        /// <summary>
        /// Application log
        /// </summary>
        Log,

        /// <summary>
        /// Debug information
        /// </summary>
        Debug,

        /// <summary>
        /// Other output type
        /// </summary>
        Other
    }
}
