namespace HackerOs.Simulation.Abstractions.Processes;

/// <summary>
/// Where a per-process CPU or memory figure comes from (ADR 0066, section 4). Shown in
/// <c>/proc/&lt;pid&gt;/status</c> as <c>CpuSource:</c> and <c>MemSource:</c>.
/// </summary>
public enum ResourceMeasurementSource
{
    /// <summary>Drawn from the app's declared <see cref="ResourceProfile"/> by the <see cref="IResourceSimulator"/> (ADR 0012).</summary>
    Simulated,

    /// <summary>Really measured by the OS at context switches (ADR 0067, 0068).</summary>
    Measured
}

/// <summary>
/// Measured CPU time per process (ADR 0066, 0067). Platform Core only knows this contract and its
/// null implementation; the measuring implementation ships in
/// <c>HackerOs.Platform.ResourceAccounting</c>, which a host may leave out of its binary.
/// </summary>
/// <remarks>
/// When <see cref="TryGetCpuUsage"/> returns <see langword="false"/>, the reader falls back to the
/// <see cref="IResourceSimulator"/> exactly as before phase 5, and reports
/// <see cref="ResourceMeasurementSource.Simulated"/>.
/// </remarks>
public interface ICpuTimeAccountant
{
    /// <summary>Gets whether this accountant measures anything at all (<see langword="false"/> for the null accountant).</summary>
    bool IsMeasuring { get; }

    /// <summary>Gets the measured CPU usage of <paramref name="pid"/> over the last second.</summary>
    /// <param name="pid">The process to read.</param>
    /// <param name="coreEquivalents">The usage in core-equivalents (1.0 = one virtual core fully busy).</param>
    /// <returns><see langword="true"/> when the process is measured; otherwise the simulator applies.</returns>
    bool TryGetCpuUsage(ProcessId pid, out double coreEquivalents);
}

/// <summary>
/// Measured memory per process (ADR 0066, 0068). Same model as <see cref="ICpuTimeAccountant"/>:
/// a <see langword="false"/> answer means the simulated value applies.
/// </summary>
/// <remarks>
/// The reader passes the live process table with every call: the accountant uses it to split the
/// real managed heap between the processes still alive and to forget the ones that are gone, like
/// the kernel freeing a reaped task's pages. It never resolves the process table itself.
/// </remarks>
public interface IMemoryAccountant
{
    /// <summary>Gets whether this accountant measures anything at all (<see langword="false"/> for the null accountant).</summary>
    bool IsMeasuring { get; }

    /// <summary>Gets the measured memory of one process, layer by layer (ADR 0068, section 1).</summary>
    /// <param name="process">The process to read.</param>
    /// <param name="active">Every live process, among which the resident heap is split.</param>
    /// <param name="usage">The layers of <paramref name="process"/>.</param>
    /// <returns><see langword="true"/> when the process is measured; otherwise the simulator applies (a game-content process that runs no code).</returns>
    bool TryGetProcessMemory(ProcessRecord process, IReadOnlyList<ProcessRecord> active, out ProcessMemoryUsage usage);

    /// <summary>Gets the measured memory of the whole machine, behind <c>/proc/meminfo</c>.</summary>
    /// <param name="active">Every live process.</param>
    /// <param name="usage">The machine-wide totals, shared images counted once.</param>
    /// <returns><see langword="true"/> when memory is measured; otherwise the simulator applies.</returns>
    bool TryGetSystemMemory(IReadOnlyList<ProcessRecord> active, out SystemMemoryUsage usage);
}

/// <summary>
/// One change of the process running on the current thread, seen by the ambient-frame hook
/// (ADR 0067, section 1). <see langword="null"/> means "outside any process frame" (OS code,
/// Blazor rendering, idle).
/// </summary>
/// <param name="From">The process that stops running here, if any.</param>
/// <param name="To">The process that starts running here, if any.</param>
/// <param name="ThreadContextChanged">
/// <see langword="true"/> when the runtime restored an <see cref="ExecutionContext"/> (start or end
/// of a continuation, a timer callback, ...) rather than code assigning the frame explicitly.
/// </param>
public readonly record struct ExecutionContextSwitch(ProcessId? From, ProcessId? To, bool ThreadContextChanged);

/// <summary>
/// Receives every context switch between processes on the thread that runs app code: the common
/// hook shared by the CPU (ADR 0067) and memory (ADR 0068) measurements.
/// </summary>
/// <remarks>
/// <para>
/// The callback runs <b>inside</b> the runtime's execution-context restore, on the hot path of
/// every <c>await</c> continuation. Implementations must be allocation-free, lock-free on the main
/// thread, and must never throw (a throwing observer is skipped by the dispatcher, but the
/// measurement it was making is lost).
/// </para>
/// <para>
/// Switches where the process does not change (nested frames of the same process, or OS code
/// resuming OS code) are not reported.
/// </para>
/// </remarks>
public interface IExecutionContextSwitchObserver
{
    /// <summary>Called when the running process changes on the current thread.</summary>
    /// <param name="contextSwitch">The process left, the process entered, and how.</param>
    void OnContextSwitch(in ExecutionContextSwitch contextSwitch);
}
