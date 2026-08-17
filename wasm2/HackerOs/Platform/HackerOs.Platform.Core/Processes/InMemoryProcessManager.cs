using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Simulation.Abstractions.Time;

namespace HackerOs.Platform.Core.Processes;

/// <summary>
/// In-memory <see cref="IProcessManager"/> implementing the ADR 0012 process lifecycle,
/// cancellation, bounded stop, kill/child-cleanup, and bounded history retention.
/// </summary>
public sealed class InMemoryProcessManager : IProcessManager
{
    private readonly ISimulationClock _clock;
    private readonly ISessionService _session;
    private readonly IEventBus _eventBus;
    private readonly int _maxHistory;

    private readonly Lock _gate = new();
    private long _nextPid = 1;
    private readonly Dictionary<ProcessId, Entry> _active = [];
    private readonly List<ProcessRecord> _history = [];

    /// <summary>Initializes an in-memory process manager.</summary>
    /// <param name="clock">Simulation clock used for every timestamp and bounded-stop deadline.</param>
    /// <param name="session">Active session whose root token every process token links to.</param>
    /// <param name="eventBus">Bus used to publish <see cref="ProcessStateChangedEvent"/> transitions.</param>
    /// <param name="maxHistory">Maximum retained terminal process records; oldest is evicted first.</param>
    public InMemoryProcessManager(
        ISimulationClock clock, ISessionService session, IEventBus eventBus, int maxHistory = 200)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxHistory, 1);

        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _maxHistory = maxHistory;
    }

    /// <inheritdoc />
    public ProcessRecord Start(ProcessStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            if (request.ParentPid is { } parentPid && !_active.ContainsKey(parentPid))
            {
                throw new InvalidOperationException($"Parent process '{parentPid}' is not active.");
            }

            ProcessId pid = ProcessId.FromInt64(_nextPid++);
            CancellationTokenSource tokenSource = _session.CreateLinkedCancellationSource();
            DateTimeOffset now = _clock.UtcNow;

            ProcessRecord record = new(
                pid,
                request.ParentPid,
                request.AppId,
                request.AppInstanceId,
                request.Kind,
                request.UserId,
                request.SessionId,
                ProcessState.Starting,
                request.ResourceProfile,
                serviceHealth: request.Kind == AppKind.Service ? ServiceHealth.Unknown : null,
                createdAtUtc: now,
                startedAtUtc: null,
                stoppedAtUtc: null,
                exitCode: null,
                exitReason: null);

            _active[pid] = new Entry(record, tokenSource);
            _eventBus.Publish(new ProcessStateChangedEvent(pid, ProcessState.Created, ProcessState.Starting, now));
            return record;
        }
    }

    /// <inheritdoc />
    public ProcessRecord MarkRunning(ProcessId pid)
    {
        lock (_gate)
        {
            Entry entry = GetActiveEntry(pid);
            ProcessRecord old = entry.Record;
            if (old.State != ProcessState.Starting)
            {
                throw new InvalidOperationException($"Only a Starting process may become Running (was {old.State}).");
            }

            DateTimeOffset now = _clock.UtcNow;
            ProcessRecord updated = new(
                old.Pid, old.ParentPid, old.AppId, old.AppInstanceId, old.Kind, old.UserId, old.SessionId,
                ProcessState.Running, old.ResourceProfile, old.ServiceHealth, old.CreatedAtUtc,
                startedAtUtc: now, stoppedAtUtc: null, exitCode: null, exitReason: null);

            entry.Record = updated;
            _eventBus.Publish(new ProcessStateChangedEvent(pid, old.State, updated.State, now));
            return updated;
        }
    }

    /// <inheritdoc />
    public ProcessRecord Complete(ProcessId pid, int exitCode)
    {
        lock (_gate)
        {
            Entry entry = GetActiveEntry(pid);
            return Finish(entry, ProcessState.Stopped, exitCode, ProcessExitReason.Completed);
        }
    }

    /// <inheritdoc />
    public ProcessRecord Fault(ProcessId pid, ProcessExitReason reason = ProcessExitReason.Fault)
    {
        lock (_gate)
        {
            Entry entry = GetActiveEntry(pid);
            return Finish(entry, ProcessState.Faulted, exitCode: null, reason);
        }
    }

    /// <inheritdoc />
    public async Task<ProcessRecord> StopAsync(
        ProcessId pid,
        TimeSpan timeout,
        ProcessExitReason reason = ProcessExitReason.CloseRequested,
        CancellationToken cancellationToken = default)
    {
        Entry entry;
        TaskCompletionSource<ProcessRecord> stopSignal;
        lock (_gate)
        {
            entry = GetActiveEntry(pid);
            stopSignal = new TaskCompletionSource<ProcessRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
            entry.StopSignal = stopSignal;
            TransitionToStopping(entry);
            entry.TokenSource.Cancel();
        }

        Task delay = _clock.DelayAsync(timeout, cancellationToken);
        Task completed = await Task.WhenAny(stopSignal.Task, delay).ConfigureAwait(false);
        if (completed == stopSignal.Task)
        {
            return await stopSignal.Task.ConfigureAwait(false);
        }

        lock (_gate)
        {
            if (_active.TryGetValue(pid, out Entry? stillActive) && ReferenceEquals(stillActive, entry))
            {
                return ForceTerminal(entry, ProcessExitReason.Timeout);
            }
        }

        // Resolved concurrently between the timeout firing and re-acquiring the lock.
        return await stopSignal.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ProcessRecord Kill(ProcessId pid, ProcessExitReason reason = ProcessExitReason.Killed)
    {
        lock (_gate)
        {
            Entry entry = GetActiveEntry(pid);
            KillDescendants(pid);
            return ForceTerminal(entry, reason);
        }
    }

    /// <inheritdoc />
    public bool TryGetActive(ProcessId pid, out ProcessRecord process)
    {
        lock (_gate)
        {
            if (_active.TryGetValue(pid, out Entry? entry))
            {
                process = entry.Record;
                return true;
            }

            process = null!;
            return false;
        }
    }

    /// <inheritdoc />
    public bool TryGetSingleton(string appId, out ProcessRecord process)
    {
        lock (_gate)
        {
            Entry? match = _active.Values.FirstOrDefault(e => e.Record.AppId == appId);
            process = match?.Record!;
            return match is not null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ProcessRecord> GetActiveProcesses()
    {
        lock (_gate)
        {
            return [.. _active.Values.Select(e => e.Record)];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ProcessRecord> GetHistory()
    {
        lock (_gate)
        {
            return [.. _history];
        }
    }

    /// <inheritdoc />
    public CancellationToken GetCancellationToken(ProcessId pid)
    {
        lock (_gate)
        {
            return GetActiveEntry(pid).TokenSource.Token;
        }
    }

    private void KillDescendants(ProcessId parentPid)
    {
        List<ProcessId> children = [.. _active.Values
            .Where(e => e.Record.ParentPid == parentPid)
            .Select(e => e.Record.Pid)];

        foreach (ProcessId childPid in children)
        {
            if (_active.TryGetValue(childPid, out Entry? childEntry))
            {
                KillDescendants(childPid);
                ForceTerminal(childEntry, ProcessExitReason.DependencyStop);
            }
        }
    }

    private void TransitionToStopping(Entry entry)
    {
        ProcessRecord old = entry.Record;
        if (old.State == ProcessState.Stopping)
        {
            return;
        }

        if (old.State is not (ProcessState.Starting or ProcessState.Running))
        {
            throw new InvalidOperationException($"Cannot stop a process in state {old.State}.");
        }

        DateTimeOffset now = _clock.UtcNow;
        DateTimeOffset started = old.StartedAtUtc ?? now;
        ProcessRecord updated = new(
            old.Pid, old.ParentPid, old.AppId, old.AppInstanceId, old.Kind, old.UserId, old.SessionId,
            ProcessState.Stopping, old.ResourceProfile, old.ServiceHealth, old.CreatedAtUtc,
            started, stoppedAtUtc: null, exitCode: null, exitReason: null);

        entry.Record = updated;
        _eventBus.Publish(new ProcessStateChangedEvent(old.Pid, old.State, updated.State, now));
    }

    private ProcessRecord Finish(Entry entry, ProcessState terminalState, int? exitCode, ProcessExitReason reason)
    {
        ProcessRecord old = entry.Record;
        DateTimeOffset now = _clock.UtcNow;
        ProcessRecord updated = new(
            old.Pid, old.ParentPid, old.AppId, old.AppInstanceId, old.Kind, old.UserId, old.SessionId,
            terminalState, old.ResourceProfile, old.ServiceHealth, old.CreatedAtUtc, old.StartedAtUtc,
            stoppedAtUtc: now, exitCode, reason);

        _active.Remove(old.Pid);
        entry.TokenSource.Cancel();
        entry.TokenSource.Dispose();
        AddHistory(updated);
        _eventBus.Publish(new ProcessStateChangedEvent(old.Pid, old.State, updated.State, now));
        entry.StopSignal?.TrySetResult(updated);
        return updated;
    }

    private ProcessRecord ForceTerminal(Entry entry, ProcessExitReason reason) =>
        Finish(entry, ProcessState.Stopped, exitCode: null, reason);

    private void AddHistory(ProcessRecord record)
    {
        _history.Add(record);
        if (_history.Count > _maxHistory)
        {
            _history.RemoveAt(0);
        }
    }

    private Entry GetActiveEntry(ProcessId pid) => _active.TryGetValue(pid, out Entry? entry)
        ? entry
        : throw new KeyNotFoundException($"No active process with PID '{pid}'.");

    private sealed class Entry(ProcessRecord record, CancellationTokenSource tokenSource)
    {
        public ProcessRecord Record { get; set; } = record;
        public CancellationTokenSource TokenSource { get; } = tokenSource;
        public TaskCompletionSource<ProcessRecord>? StopSignal { get; set; }
    }
}
