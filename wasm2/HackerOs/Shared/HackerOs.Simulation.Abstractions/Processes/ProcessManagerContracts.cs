using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Simulation.Abstractions.Processes;

/// <summary>Describes one new process to create, per ADR 0012.</summary>
/// <param name="ParentPid">The parent process's PID, or <see langword="null"/> for a root process.</param>
/// <param name="AppId">Non-empty ID of the app to host.</param>
/// <param name="AppInstanceId">Identifier of this specific running instance of the app.</param>
/// <param name="Kind">Lifecycle/hosting model of the hosted app.</param>
/// <param name="UserId">User the process will run as.</param>
/// <param name="SessionId">Session the process will belong to.</param>
/// <param name="ResourceProfile">Resource profile snapshot used to derive simulated resource ticks.</param>
/// <param name="Arguments">
/// The new process's <c>argv</c>, command name included (ADR 0050, section 5);
/// <see langword="null"/> or empty records the app ID alone.
/// </param>
/// <param name="WorkingDirectory">
/// Initial working directory; <see langword="null"/> inherits the parent's, or <c>/</c> for a root process.
/// </param>
/// <param name="Environment">
/// Variables applied over the inherited environment (the parent's, copied at creation; empty for a
/// root process). The platform passes the login environment for a root process.
/// </param>
/// <param name="Auxiliary">
/// Whether the process is an auxiliary of its app rather than the app's instance (ADR 0084,
/// section 3): the renderer of a Blazor site, as <c>chrome --type=renderer</c> is to its browser. An
/// auxiliary process is never found by <see cref="IProcessManager.TryGetSingleton"/>, so a web app's
/// on-demand activation still starts its site when only renderers run.
/// </param>
public sealed record ProcessStartRequest(
    ProcessId? ParentPid,
    string AppId,
    AppInstanceId AppInstanceId,
    AppKind Kind,
    LocalUserId UserId,
    SessionId SessionId,
    ResourceProfile ResourceProfile,
    IReadOnlyList<string>? Arguments = null,
    VirtualPath? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    bool Auxiliary = false);

/// <summary>
/// Creates, tracks, and terminates processes in memory, per ADR 0012. All state transitions are
/// platform-owned: callers only request starts, report a hosted app's own lifecycle progress
/// (<see cref="MarkRunning"/>, <see cref="Complete"/>, <see cref="Fault"/>), or request a stop
/// (<see cref="StopAsync"/>, <see cref="Kill"/>).
/// </summary>
public interface IProcessManager
{
    /// <summary>
    /// Creates a new process in the <see cref="ProcessState.Starting"/> state, linked to the
    /// current session's root cancellation token.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="request"/> declares a parent PID that is not currently active.</exception>
    ProcessRecord Start(ProcessStartRequest request);

    /// <summary>
    /// Reports that a <see cref="ProcessState.Starting"/> process has reached steady-state
    /// execution, recording the current simulation time as its start time.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No active process has this PID.</exception>
    /// <exception cref="InvalidOperationException">The process is not currently <see cref="ProcessState.Starting"/>.</exception>
    ProcessRecord MarkRunning(ProcessId pid);

    /// <summary>Reports that a process finished its own work and reached <see cref="ProcessState.Stopped"/>.</summary>
    /// <exception cref="KeyNotFoundException">No active process has this PID.</exception>
    ProcessRecord Complete(ProcessId pid, int exitCode);

    /// <summary>Reports that a process terminated abnormally and reached <see cref="ProcessState.Faulted"/>.</summary>
    /// <exception cref="KeyNotFoundException">No active process has this PID.</exception>
    ProcessRecord Fault(ProcessId pid, ProcessExitReason reason = ProcessExitReason.Fault);

    /// <summary>
    /// Requests a graceful stop: cancels the process's token and moves it to
    /// <see cref="ProcessState.Stopping"/>, then waits up to <paramref name="timeout"/> (measured by
    /// the simulation clock) for <see cref="Complete"/>, <see cref="Fault"/>, or <see cref="Kill"/> to
    /// resolve it. If the deadline elapses first, the process is force-stopped with
    /// <see cref="ProcessExitReason.Timeout"/>.
    /// </summary>
    /// <remarks>
    /// ADR 0069: the process first moves to <see cref="ProcessState.Stopping"/>, then receives one
    /// <see cref="ProcessStopRequest"/> carrying <paramref name="reason"/> and the deadline (raised
    /// once, even when several stops overlap), then its token is cancelled. Concurrent calls share
    /// the same outcome.
    /// </remarks>
    /// <exception cref="KeyNotFoundException">No active process has this PID.</exception>
    Task<ProcessRecord> StopAsync(
        ProcessId pid,
        TimeSpan timeout,
        ProcessExitReason reason = ProcessExitReason.CloseRequested,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Immediately force-stops one process and every active descendant (recorded with
    /// <see cref="ProcessExitReason.DependencyStop"/>), cancelling their tokens.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No active process has this PID.</exception>
    ProcessRecord Kill(ProcessId pid, ProcessExitReason reason = ProcessExitReason.Killed);

    /// <summary>Attempts to find one active (non-terminal) process by PID.</summary>
    bool TryGetActive(ProcessId pid, out ProcessRecord process);

    /// <summary>Attempts to find the first active (non-terminal) process hosting a given app, for single-instance apps.</summary>
    bool TryGetSingleton(string appId, out ProcessRecord process);

    /// <summary>Gets every currently active (non-terminal) process.</summary>
    IReadOnlyList<ProcessRecord> GetActiveProcesses();

    /// <summary>Gets bounded, most-recent terminal process history, oldest first.</summary>
    IReadOnlyList<ProcessRecord> GetHistory();

    /// <summary>Gets the cancellation token owned by one active process.</summary>
    /// <exception cref="KeyNotFoundException">No active process has this PID.</exception>
    CancellationToken GetCancellationToken(ProcessId pid);
}

/// <summary>
/// One request to stop a process (ADR 0069, section 1): the <c>SIGTERM</c> the OS delivers before
/// it cancels the process's token and waits for it to end.
/// </summary>
/// <param name="Pid">The process asked to stop.</param>
/// <param name="Reason">Why it is asked to stop (<see cref="ProcessExitReason.CloseRequested"/>, <see cref="ProcessExitReason.Terminated"/>…).</param>
/// <param name="RequestedAtUtc">Simulated time of the request.</param>
/// <param name="Deadline">Simulated time at which the OS forces the process (<c>SIGKILL</c>) if it is still running.</param>
public sealed record ProcessStopRequest(
    ProcessId Pid, ProcessExitReason Reason, DateTimeOffset RequestedAtUtc, DateTimeOffset Deadline);

/// <summary>Published whenever a process transitions between lifecycle states.</summary>
public sealed record ProcessStateChangedEvent(
    ProcessId Pid, ProcessState OldState, ProcessState NewState, DateTimeOffset AtUtc);
