using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.Metrics;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.Core.Metrics;

/// <summary>
/// In-memory <see cref="ICpuTimeAccounting"/> behind <c>/proc/stat</c> (ADR 0057 section 2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Units.</b> Linux counts CPU time in <c>USER_HZ</c>, 100 per second. One sampling interval of
/// <c>t</c> simulated seconds gives each core <c>100 × t</c> units to split between <c>user</c>,
/// <c>system</c> and <c>idle</c>, so the columns of a core always add up to its uptime.
/// </para>
/// <para>
/// <b>Scheduling.</b> The simulator reports a process's usage in core-equivalents, not per core.
/// Like a scheduler with CPU affinity, each process starts on core <c>pid mod cores</c> and spills
/// onto the next cores once that one is full, so two busy processes land on different cores and
/// <c>cpu0</c> and <c>cpu1</c> differ the way they do on real hardware.
/// </para>
/// <para>
/// <b>User or system.</b> The simulator does not model a kernel: busy time goes to <c>system</c>
/// for OS processes (services the host declared system apps, and processes the catalog does not
/// know) and to <c>user</c> for every other app.
/// </para>
/// </remarks>
public sealed class CpuTimeAccounting : ICpuTimeAccounting
{
    /// <summary>Linux's <c>USER_HZ</c>: CPU-time units per second.</summary>
    public const int UserHz = 100;

    private readonly Func<ProcessRecord, bool> _isSystemProcess;
    private readonly Lock _gate = new();
    private readonly double[] _user;
    private readonly double[] _system;
    private readonly double[] _idle;

    /// <summary>Initializes the accounting for the cores of <paramref name="hardware"/>.</summary>
    /// <param name="hardware">Virtual hardware; the core count is <c>ceil(CpuCapacity)</c>, as in <c>/proc/cpuinfo</c>.</param>
    /// <param name="isSystemProcess">
    /// Tells whether a process is part of the OS (its time goes to <c>system</c>). Defaults to
    /// "every service": without a catalog, services are the closest match to kernel threads.
    /// </param>
    public CpuTimeAccounting(VirtualHardwareProfile hardware, Func<ProcessRecord, bool>? isSystemProcess = null)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        CoreCount = Math.Max(1, (int)Math.Ceiling(hardware.CpuCapacity));
        _isSystemProcess = isSystemProcess ?? (static process => process.Kind == AppKind.Service);
        _user = new double[CoreCount];
        _system = new double[CoreCount];
        _idle = new double[CoreCount];
    }

    /// <inheritdoc />
    public int CoreCount { get; }

    /// <inheritdoc />
    public void Record(IReadOnlyList<ProcessResourceSample> samples, IReadOnlyList<ProcessRecord> processes, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(processes);
        if (interval <= TimeSpan.Zero)
        {
            return;
        }

        Dictionary<ProcessId, ProcessRecord> byPid = processes.ToDictionary(static process => process.Pid);
        double[] userLoad = new double[CoreCount];
        double[] systemLoad = new double[CoreCount];
        foreach (ProcessResourceSample sample in samples)
        {
            bool isSystem = byPid.TryGetValue(sample.Pid, out ProcessRecord? process) && _isSystemProcess(process);
            Schedule(sample.Pid, sample.CpuUsage, isSystem ? systemLoad : userLoad, userLoad, systemLoad);
        }

        double units = interval.TotalSeconds * UserHz;
        lock (_gate)
        {
            for (int core = 0; core < CoreCount; core++)
            {
                _user[core] += userLoad[core] * units;
                _system[core] += systemLoad[core] * units;
                _idle[core] += Math.Max(0, 1 - userLoad[core] - systemLoad[core]) * units;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<CpuTimes> GetCores()
    {
        lock (_gate)
        {
            return
            [
                .. Enumerable.Range(0, CoreCount).Select(core =>
                    new CpuTimes((long)_user[core], 0, (long)_system[core], (long)_idle[core]))
            ];
        }
    }

    /// <summary>
    /// Places <paramref name="usage"/> core-equivalents on the cores, starting with the process's
    /// home core and spilling over to the next ones while it is full.
    /// </summary>
    private void Schedule(ProcessId pid, double usage, double[] target, double[] userLoad, double[] systemLoad)
    {
        double remaining = Math.Max(0, usage);
        int home = (int)(pid.Value % CoreCount);
        for (int offset = 0; offset < CoreCount && remaining > 0; offset++)
        {
            int core = (home + offset) % CoreCount;
            double free = Math.Max(0, 1 - userLoad[core] - systemLoad[core]);
            double taken = Math.Min(free, remaining);
            target[core] += taken;
            remaining -= taken;
        }
    }
}
