using System;

namespace HackerOs.OS.Applications.Lifecycle.Events;

/// <summary>
/// Event args for application output
/// </summary>
public class ApplicationOutputEventArgs : EventArgs
{
    public string Output { get; }

    public ApplicationOutputEventArgs(string output)
    {
        Output = output;
    }
}
