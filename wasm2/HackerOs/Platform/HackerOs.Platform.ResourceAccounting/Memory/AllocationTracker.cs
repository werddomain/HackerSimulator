using System.Collections.Concurrent;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.ResourceAccounting.Memory;

/// <summary>
/// The allocation layer of ADR 0068: at every context switch, reads the thread's allocation counter
/// and charges the bytes allocated since the previous switch to the process that was running.
/// </summary>
/// <remarks>
/// <para>
/// One thread runs app code at a time on WebAssembly, so the difference between two readings belongs
/// to whoever ran in between: <see cref="ExecutionContextSwitch.From"/>. Bytes allocated outside any
/// process frame (the compositor, Blazor rendering, OS services) go to <see cref="OsAllocatedBytes"/>.
/// The last reading is kept per thread, so the counts stay right on a multithreaded host too.
/// </para>
/// <para>
/// The hot path is allocation-free once a process has its counter (one dictionary lookup and one
/// interlocked add), lock-free, and never throws.
/// </para>
/// </remarks>
public sealed class AllocationTracker : IExecutionContextSwitchObserver
{
    private readonly IManagedHeapProbe _probe;
    private readonly ConcurrentDictionary<long, Counter> _counters = new();
    private readonly ThreadLocal<long[]> _lastReading = new(static () => [-1]);
    private long _osBytes;

    /// <summary>Creates a tracker over <paramref name="probe"/>.</summary>
    /// <param name="probe">The allocation counter to read; <see cref="GcManagedHeapProbe.Instance"/> in a host.</param>
    public AllocationTracker(IManagedHeapProbe probe)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    /// <summary>Gets the bytes allocated outside any process frame, by the OS itself.</summary>
    public long OsAllocatedBytes => Interlocked.Read(ref _osBytes);

    /// <inheritdoc />
    public void OnContextSwitch(in ExecutionContextSwitch contextSwitch)
    {
        long now = _probe.GetAllocatedBytesForCurrentThread();
        long[] last = _lastReading.Value!;
        long previous = last[0];
        last[0] = now;
        if (previous < 0 || now <= previous)
        {
            // First switch seen on this thread: nobody can be charged for what came before.
            return;
        }

        long delta = now - previous;
        if (contextSwitch.From is { } pid)
        {
            if (!_counters.TryGetValue(pid.Value, out Counter? counter))
            {
                counter = _counters.GetOrAdd(pid.Value, static _ => new Counter());
            }

            Interlocked.Add(ref counter.Bytes, delta);
        }
        else
        {
            Interlocked.Add(ref _osBytes, delta);
        }
    }

    /// <summary>Gets the bytes <paramref name="pid"/> allocated since it started (<c>HackAllocBytes</c>).</summary>
    /// <param name="pid">The process.</param>
    public long GetAllocatedBytes(ProcessId pid) =>
        _counters.TryGetValue(pid.Value, out Counter? counter) ? Interlocked.Read(ref counter.Bytes) : 0;

    /// <summary>Gets whether <paramref name="pid"/> has ever run code and left a frame (it allocated, or at least ran).</summary>
    /// <param name="pid">The process.</param>
    public bool HasRun(ProcessId pid) => _counters.ContainsKey(pid.Value);

    /// <summary>Forgets every process not in <paramref name="live"/>, like the kernel freeing a reaped task.</summary>
    /// <param name="live">The processes still alive.</param>
    public void Retain(IReadOnlySet<long> live)
    {
        ArgumentNullException.ThrowIfNull(live);
        foreach (long pid in _counters.Keys)
        {
            if (!live.Contains(pid))
            {
                _counters.TryRemove(pid, out _);
            }
        }
    }

    private sealed class Counter
    {
        public long Bytes;
    }
}
