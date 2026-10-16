using System.Globalization;

namespace HackerOs.AppSdk.Diagnostics;

/// <summary>
/// Thrown by every OS gateway called on behalf of a process the OS has killed (ADR 0069,
/// section 3): <c>kill -9</c>, "Force kill", or a <c>SIGTERM</c> whose grace period ran out. The
/// process no longer exists; the code still running for it is a leftover, like a thread of a Linux
/// process after <c>SIGKILL</c>, and is cut off from the OS.
/// </summary>
/// <remarks>
/// It derives from <see cref="OperationCanceledException"/> so the usual cancellation paths unwind it
/// quietly. App code has nothing to do with it: there is no one left to report to.
/// </remarks>
public sealed class HackProcessKilledException : OperationCanceledException
{
    /// <summary>Initializes the exception for one killed process.</summary>
    /// <param name="processId">PID of the killed process.</param>
    public HackProcessKilledException(long processId)
        : base($"Process {processId.ToString(CultureInfo.InvariantCulture)} was killed; the OS refuses its calls.")
    {
        ProcessId = processId;
    }

    /// <summary>Gets the PID of the killed process.</summary>
    public long ProcessId { get; }
}
