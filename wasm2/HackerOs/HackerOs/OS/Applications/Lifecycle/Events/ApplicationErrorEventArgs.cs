using System;

namespace HackerOs.OS.Applications.Lifecycle.Events;

/// <summary>
/// Event args for application errors
/// </summary>
public class ApplicationErrorEventArgs : EventArgs
{
    public string Error { get; }

    public ApplicationErrorEventArgs(string error)
    {
        Error = error;
    }
}
