using HackerOs.Simulation.Abstractions.Time;

namespace HackerOs.Platform.Core.Time;

/// <summary>
/// Deterministic manual <see cref="ISimulationClock"/> used by production hosts driving ticks
/// from one platform timer and by headless tests advancing ticks explicitly.
/// </summary>
public sealed class ManualSimulationClock : ISimulationClock
{
    private readonly List<ScheduledCallback> _pending = [];
    private readonly List<SimulationSchedulerFault> _faults = [];
    private long _insertionSequence;

    /// <summary>Initializes the clock at a starting UTC time with a fixed tick duration.</summary>
    /// <param name="startUtc">Starting simulated UTC time; must use UTC offset zero.</param>
    /// <param name="tickDuration">Fixed positive duration represented by one tick.</param>
    public ManualSimulationClock(DateTimeOffset startUtc, TimeSpan tickDuration)
    {
        if (startUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The simulation clock requires a UTC start time.", nameof(startUtc));
        }

        if (tickDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(tickDuration), "Tick duration must be positive.");
        }

        UtcNow = startUtc;
        TickDuration = tickDuration;
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; private set; }

    /// <inheritdoc />
    public long CurrentTick { get; private set; }

    /// <inheritdoc />
    public TimeSpan TickDuration { get; }

    /// <summary>Gets every callback fault recorded so far, in the order it occurred.</summary>
    public IReadOnlyList<SimulationSchedulerFault> Faults => _faults;

    /// <inheritdoc />
    public IDisposable Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Delay cannot be negative.");
        }

        long ticksToWait = delay == TimeSpan.Zero
            ? 0
            : (long)Math.Ceiling(delay / TickDuration);
        ScheduledCallback entry = new(CurrentTick + ticksToWait, _insertionSequence++, callback);
        _pending.Add(entry);
        return new ScheduledCallbackHandle(_pending, entry);
    }

    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        TaskCompletionSource completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable scheduled = Schedule(delay, () => completionSource.TrySetResult());

        CancellationTokenRegistration registration = default;
        if (cancellationToken.CanBeCanceled)
        {
            registration = cancellationToken.Register(() =>
            {
                scheduled.Dispose();
                completionSource.TrySetCanceled(cancellationToken);
            });
        }

        return completionSource.Task.ContinueWith(
            static (task, state) =>
            {
                ((CancellationTokenRegistration)state!).Dispose();
                return task;
            },
            registration,
            TaskScheduler.Default).Unwrap();
    }

    /// <summary>
    /// Advances the clock by one or more ticks, running every callback due at or before the new
    /// tick in due-tick then insertion-sequence order. A callback exception is isolated and
    /// recorded in <see cref="Faults"/> without preventing later callbacks from running.
    /// </summary>
    /// <param name="tickCount">Number of ticks to advance; must be at least one.</param>
    public void Advance(int tickCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tickCount, 1);

        for (int i = 0; i < tickCount; i++)
        {
            CurrentTick++;
            UtcNow += TickDuration;
            RunDueCallbacks();
        }
    }

    private void RunDueCallbacks()
    {
        List<ScheduledCallback> due = [.. _pending
            .Where(callback => callback.DueTick <= CurrentTick)
            .OrderBy(callback => callback.DueTick)
            .ThenBy(callback => callback.Sequence)];

        foreach (ScheduledCallback callback in due)
        {
            _pending.Remove(callback);
            try
            {
                callback.Callback();
            }
            catch (Exception exception)
            {
                _faults.Add(new SimulationSchedulerFault(CurrentTick, exception));
            }
        }
    }

    private sealed record ScheduledCallback(long DueTick, long Sequence, Action Callback);

    private sealed class ScheduledCallbackHandle(List<ScheduledCallback> pending, ScheduledCallback entry) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            pending.Remove(entry);
        }
    }
}
