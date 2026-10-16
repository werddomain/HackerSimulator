using System.Globalization;
using HackerOs.Apps.SystemMonitor.Metrics;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Apps.SystemMonitor.Tabs;

/// <summary>
/// The Performance tab (ADR 0058, section 2): the summary that was the old Overview tab, the CPU
/// chart (total or per core, from <c>/proc/stat</c>), the memory chart (<c>/proc/meminfo</c>), one
/// minute of history each, and the hardware section (<c>/proc/cpuinfo</c>, <c>/proc/meminfo</c>)
/// that was the old "Hardware &amp; System Info" tab.
/// </summary>
public partial class PerformanceTab
{
    private bool _perCore;

    /// <summary>Gets or sets the live measurements.</summary>
    [Parameter, EditorRequired]
    public SystemMetrics Metrics { get; set; } = default!;

    /// <summary>Gets or sets the processors of <c>/proc/cpuinfo</c>.</summary>
    [Parameter]
    public IReadOnlyList<ProcCpuInfoEntry> CpuInfo { get; set; } = [];

    /// <summary>Gets or sets how many processes are visible.</summary>
    [Parameter]
    public int ProcessCount { get; set; }

    private ProcMemInfo? Memory => Metrics.Latest?.Memory;

    /// <summary>Gets the Web Workers (ADR 0071, section 5), shown next to the cores; none on a host without them.</summary>
    private ProcWorkers? Workers => Metrics.Latest?.Workers;

    private static string RoleText(ProcWorker worker) => worker.Role switch
    {
        "pool" => worker.Core is { } core ? $"core {core}" : "pool",
        "overflow" => "overflow core",
        "dedicated" => "dedicated",
        "ready" => "ready",
        string other => other
    };

    private static string StateText(ProcWorker worker) => worker.State switch
    {
        "not-responding" => "Not responding",
        "running" => "Running",
        "idle" => "Idle",
        "starting" => "Starting…",
        string other => other
    };

    private int CoreCount => Metrics.Latest?.Stat?.Cores.Count is > 0 and int cores ? cores : Math.Max(1, CpuInfo.Count);

    private string LoadText => Metrics.Latest?.Load is { } load
        ? string.Create(CultureInfo.InvariantCulture, $"{load.OneMinute:0.00} {load.FiveMinutes:0.00} {load.FifteenMinutes:0.00}")
        : "—";
}
