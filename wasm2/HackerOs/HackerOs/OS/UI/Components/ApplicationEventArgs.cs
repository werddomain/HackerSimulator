using System;

namespace HackerOs.OS.UI.Components
{
    /// <summary>
    /// Provides data for application-related events.
    /// </summary>
    public class ApplicationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the application ID.
        /// </summary>
        public string ApplicationId { get; }

        /// <summary>
        /// Gets the application name.
        /// </summary>
        public string ApplicationName { get; }

        /// <summary>
        /// Gets the event type.
        /// </summary>
        public ApplicationEventType EventType { get; }

        /// <summary>
        /// Gets additional data associated with the event.
        /// </summary>
        public object? Data { get; }

        /// <summary>
        /// Initializes a new instance of the ApplicationEventArgs class.
        /// </summary>
        public ApplicationEventArgs(string applicationId, string applicationName, ApplicationEventType eventType, object? data = null)
        {
            ApplicationId = applicationId ?? throw new ArgumentNullException(nameof(applicationId));
            ApplicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
            EventType = eventType;
            Data = data;
        }
    }

    /// <summary>
    /// Specifies the type of application event.
    /// </summary>
    public enum ApplicationEventType
    {
        /// <summary>
        /// Application was launched.
        /// </summary>
        Launched,

        /// <summary>
        /// Application was closed.
        /// </summary>
        Closed,

        /// <summary>
        /// Application was minimized.
        /// </summary>
        Minimized,

        /// <summary>
        /// Application was maximized.
        /// </summary>
        Maximized,

        /// <summary>
        /// Application was restored.
        /// </summary>
        Restored,

        /// <summary>
        /// Application gained focus.
        /// </summary>
        Focused,

        /// <summary>
        /// Application lost focus.
        /// </summary>
        Unfocused,

        /// <summary>
        /// Application encountered an error.
        /// </summary>
        Error
    }
}
