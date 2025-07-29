//using System;
//using System.Text.Json;
//using HackerOs.OS.Applications.Registry;
//using HackerOs.OS.User;

//namespace HackerOs.OS.Applications.Lifecycle;

///// <summary>
///// Defines the base interface that all applications must implement
///// </summary>
//public interface IApplication
//{
//    /// <summary>
//    /// The unique identifier for this application
//    /// </summary>
//    string Id { get; }

//    /// <summary>
//    /// The friendly name of this application
//    /// </summary>
//    string Name { get; }

//    /// <summary>
//    /// A description of this application
//    /// </summary>
//    string Description { get; }

//    /// <summary>
//    /// The version of this application
//    /// </summary>
//    string Version { get; }

//    /// <summary>
//    /// The path to the application's icon file
//    /// </summary>
//    string IconPath { get; }

//    /// <summary>
//    /// The type of application
//    /// </summary>
//    ApplicationType Type { get; }

//    /// <summary>
//    /// The application's manifest
//    /// </summary>
//    ApplicationMetadata Manifest { get; }

//    /// <summary>
//    /// The application's current state
//    /// </summary>
//    ApplicationState State { get; }

//    /// <summary>
//    /// The user session that owns this application instance
//    /// </summary>
//    UserSession OwnerSession { get; }

//    /// <summary>
//    /// The process ID of this application
//    /// </summary>
//    int ProcessId { get; }

//    /// <summary>
//    /// Event raised when the application's state changes
//    /// </summary>
//    event EventHandler<ApplicationStateChangedEventArgs>? StateChanged;

//    /// <summary>
//    /// Event raised when the application generates output
//    /// </summary>
//    event EventHandler<ApplicationOutputEventArgs>? OutputReceived;

//    /// <summary>
//    /// Event raised when the application encounters an error
//    /// </summary>
//    event EventHandler<ApplicationErrorEventArgs>? ErrorReceived;

//    /// <summary>
//    /// Starts the application
//    /// </summary>
//    Task StartAsync(ApplicationLaunchContext context);

//    /// <summary>
//    /// Stops the application
//    /// </summary>
//    Task StopAsync();

//    /// <summary>
//    /// Pauses the application
//    /// </summary>
//    Task PauseAsync();

//    /// <summary>
//    /// Resumes the application after being paused
//    /// </summary>
//    Task ResumeAsync();

//    /// <summary>
//    /// Terminates the application immediately
//    /// </summary>
//    Task TerminateAsync();

//    /// <summary>
//    /// Sends a message to the application
//    /// </summary>
//    Task SendMessageAsync(object message);

//    /// <summary>
//    /// Gets statistics about the application's current execution
//    /// </summary>
//    ApplicationStatistics GetStatistics();
//}

///// <summary>
///// Type of an application
///// </summary>
//public enum ApplicationType
//{
//    Console,
//    Window,
//    Service,
//    System
//}

///// <summary>
///// State of an application
///// </summary>
//public enum ApplicationState
//{
//    NotStarted,
//    Starting,
//    Running,
//    Paused,
//    Stopping,
//    Stopped,
//    Failed
//}

///// <summary>
///// Application statistics
///// </summary>
//public class ApplicationStatistics
//{
//    public DateTime StartTime { get; set; }
//    public TimeSpan Uptime { get; set; }
//    public long MemoryUsage { get; set; }
//    public double CpuUsage { get; set; }
//    public long ThreadCount { get; set; }
//    public long HandleCount { get; set; }
//    public Dictionary<string, JsonElement> CustomStats { get; set; } = new();
//}
