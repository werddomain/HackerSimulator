using System;

namespace HackerOs.OS.Applications.Lifecycle.Events;

/// <summary>
/// Event args for when an application's state changes
/// </summary>
public class ApplicationStateChangedEventArgs : EventArgs
{
    public ApplicationState OldState { get; }
    public ApplicationState NewState { get; }

    public ApplicationStateChangedEventArgs(ApplicationState oldState, ApplicationState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}
