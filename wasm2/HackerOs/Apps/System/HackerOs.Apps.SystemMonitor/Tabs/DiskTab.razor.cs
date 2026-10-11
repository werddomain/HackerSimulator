using System.Globalization;
using HackerOs.AppSdk.IO;
using HackerOs.Apps.SystemMonitor.Metrics;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Apps.SystemMonitor.Tabs;

/// <summary>
/// The Disk tab (ADR 0058, section 2): used and free space of each <see cref="HackDriveInfo"/>, and
/// the read and write throughput of each disk of <c>/proc/diskstats</c>, by difference between two
/// readings. The bytes are real: IndexedDB transactions counted by the OS (ADR 0057).
/// </summary>
public partial class DiskTab
{
    /// <summary>Gets or sets the live measurements.</summary>
    [Parameter, EditorRequired]
    public SystemMetrics Metrics { get; set; } = default!;

    /// <summary>Gets or sets the drives of the last refresh.</summary>
    [Parameter]
    public IReadOnlyList<HackDriveInfo> Drives { get; set; } = [];

    /// <summary>Width of the used part of the bar, in the bar's 0–100 coordinate space.</summary>
    private static string BarWidth(double percent) =>
        Math.Clamp(percent, 0, 100).ToString("0.##", CultureInfo.InvariantCulture);
}
