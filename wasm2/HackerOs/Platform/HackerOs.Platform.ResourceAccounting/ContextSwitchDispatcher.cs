using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.ResourceAccounting;

/// <summary>
/// The single observer installed on the ambient frame (<see cref="ContextSwitchHook"/>), fanning each
/// context switch out to the measurements that registered (CPU, ADR 0067; memory, ADR 0068).
/// </summary>
/// <remarks>
/// Registration is copy-on-write, so the hot path reads one array reference without a lock and
/// without allocating. An observer that throws is skipped for that switch; the others still run.
/// </remarks>
public sealed class ContextSwitchDispatcher : IExecutionContextSwitchObserver
{
    private readonly Lock _gate = new();
    private IExecutionContextSwitchObserver[] _observers = [];

    /// <summary>Gets the number of registered observers.</summary>
    public int ObserverCount => Volatile.Read(ref _observers).Length;

    /// <summary>Registers a measurement; dispose the result to unregister it.</summary>
    /// <param name="observer">The measurement to call on every context switch.</param>
    /// <returns>A registration whose disposal removes <paramref name="observer"/>.</returns>
    public IDisposable Register(IExecutionContextSwitchObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_gate)
        {
            Volatile.Write(ref _observers, [.. _observers, observer]);
        }

        return new Registration(this, observer);
    }

    /// <inheritdoc />
    public void OnContextSwitch(in ExecutionContextSwitch contextSwitch)
    {
        IExecutionContextSwitchObserver[] observers = Volatile.Read(ref _observers);
        for (int i = 0; i < observers.Length; i++)
        {
            try
            {
                observers[i].OnContextSwitch(in contextSwitch);
            }
            catch (Exception)
            {
                // Deliberately swallowed: this runs inside the runtime's ExecutionContext restore.
            }
        }
    }

    private void Unregister(IExecutionContextSwitchObserver observer)
    {
        lock (_gate)
        {
            int index = Array.IndexOf(_observers, observer);
            if (index >= 0)
            {
                Volatile.Write(ref _observers, [.. _observers[..index], .. _observers[(index + 1)..]]);
            }
        }
    }

    private sealed class Registration(ContextSwitchDispatcher owner, IExecutionContextSwitchObserver observer) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Unregister(observer);
            }
        }
    }
}
