using System.Collections.Generic;
using HackerOs.OS.Applications.Registry;
using HackerOs.OS.IO;
using HackerOs.OS.User;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.Lifecycle;

/// <summary>
/// Context provided when launching an application
/// </summary>
public class ApplicationLaunchContext
{
    /// <summary>
    /// Application metadata
    /// </summary>
    public required ApplicationMetadata Metadata { get; init; }

    /// <summary>
    /// User session that launched the application
    /// </summary>
    public required UserSession Session { get; init; }

    /// <summary>
    /// User who launched the application
    /// </summary>
    public required User.User User { get; init; }

    /// <summary>
    /// Logger for the application
    /// </summary>
    public required ILogger Logger { get; init; }

    /// <summary>
    /// File system access
    /// </summary>
    public required HackerOs.OS.IO.IVirtualFileSystem FileSystem { get; init; }

    /// <summary>
    /// Launch arguments
    /// </summary>
    public List<string> Arguments { get; init; } = new();
}

/// <summary>
/// Event args for application output
/// </summary>
public class ApplicationOutputEventArgs : EventArgs
{
    /// <summary>
    /// The output text
    /// </summary>
    public string Output { get; }

    public ApplicationOutputEventArgs(string output)
    {
        Output = output;
    }
}

/// <summary>
/// Event args for application errors
/// </summary>
public class ApplicationErrorEventArgs : EventArgs
{
    /// <summary>
    /// The error message
    /// </summary>
    public string Error { get; }

    public ApplicationErrorEventArgs(string error)
    {
        Error = error;
    }
}

/// <summary>
/// Event args for application state changes
/// </summary>
public class ApplicationStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// The old state
    /// </summary>
    public ApplicationState OldState { get; }

    /// <summary>
    /// The new state
    /// </summary>
    public ApplicationState NewState { get; }

    public ApplicationStateChangedEventArgs(ApplicationState oldState, ApplicationState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}
