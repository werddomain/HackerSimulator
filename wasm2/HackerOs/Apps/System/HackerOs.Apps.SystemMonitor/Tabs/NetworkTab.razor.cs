using HackerOs.Apps.SystemMonitor.Metrics;
using Microsoft.AspNetCore.Components;

namespace HackerOs.Apps.SystemMonitor.Tabs;

/// <summary>
/// The Network tab (ADR 0058, section 2): received and sent throughput of each interface of
/// <c>/proc/net/dev</c>, by difference between two readings, with the packet, error and drop
/// counters. The bytes are real: the OS counts every exchange of the apps' network gateway (ADR 0057).
/// </summary>
public partial class NetworkTab
{
    /// <summary>Gets or sets the live measurements.</summary>
    [Parameter, EditorRequired]
    public SystemMetrics Metrics { get; set; } = default!;
}
