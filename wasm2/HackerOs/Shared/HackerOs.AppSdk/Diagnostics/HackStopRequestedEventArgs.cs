using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.AppSdk.Diagnostics;

/// <summary>
/// The OS asks the process to stop (ADR 0069, section 1): the <c>SIGTERM</c> it receives before
/// its <see cref="IAppExecutionContext.CancellationToken"/> is cancelled. Raised once per process,
/// on <see cref="IAppExecutionContext.StopRequested"/> and <see cref="HackCurrentProcess.StopRequested"/>.
/// </summary>
/// <remarks>
/// The handler runs synchronously, inside the process's own ambient context, while the process is
/// already <c>Stopping</c> and before its token is cancelled: it is the place to stop accepting work,
/// flush a buffer or tell a worker loop to finish. A long cleanup belongs to the code that observes
/// the token, not to the handler. Past <see cref="Deadline"/>, the OS kills the process: its pending
/// continuations are dropped and its gateways throw <see cref="HackProcessKilledException"/>.
/// </remarks>
public sealed class HackStopRequestedEventArgs : EventArgs
{
    /// <summary>Initializes the event data.</summary>
    /// <param name="reason">Why the process is asked to stop.</param>
    /// <param name="requestedAt">Simulated time of the request.</param>
    /// <param name="deadline">Simulated time at which the OS forces the process.</param>
    public HackStopRequestedEventArgs(ProcessExitReason reason, DateTimeOffset requestedAt, DateTimeOffset deadline)
    {
        Reason = reason;
        RequestedAt = requestedAt;
        Deadline = deadline;
    }

    /// <summary>
    /// Gets why the process is asked to stop: <see cref="ProcessExitReason.CloseRequested"/> (its window
    /// is closing), <see cref="ProcessExitReason.Terminated"/> (<c>kill</c>, "End task"),
    /// <see cref="ProcessExitReason.Logout"/> or <see cref="ProcessExitReason.Shutdown"/>.
    /// </summary>
    public ProcessExitReason Reason { get; }

    /// <summary>Gets the simulated time of the request.</summary>
    public DateTimeOffset RequestedAt { get; }

    /// <summary>Gets the simulated time at which the OS forces the process if it is still running (<c>SIGKILL</c>).</summary>
    public DateTimeOffset Deadline { get; }

    /// <summary>Gets the grace period the OS gave, from <see cref="RequestedAt"/> to <see cref="Deadline"/>.</summary>
    public TimeSpan GracePeriod => Deadline - RequestedAt;
}
