using HackerOs.App.Abstractions;

namespace HackerOs.Apps.SystemMonitor;

/// <summary>
/// Manifest of "System Monitor" (ADR 0047). The build generates <c>manifest.json</c> from this
/// class; it is never instantiated at runtime.
/// </summary>
[AppManifest]
public sealed class SystemMonitorApp : AppDefinition
{
    /// <inheritdoc />
    public override string Id => "org.hackeros.system-monitor";

    /// <inheritdoc />
    public override string Name => "System Monitor";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(SystemMonitorWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.hackeros";

    /// <inheritdoc />
    public override string Description =>
        "System resource performance monitoring, process manager, memory usage, and background service diagnostics";

    /// <inheritdoc />
    public override string Category => "system";

    /// <inheritdoc />
    public override IReadOnlyList<string> Capabilities =>
        [AppCapabilities.ProcessList, AppCapabilities.ProcessManage, AppCapabilities.ServicesManage];

    /// <inheritdoc />
    public override bool SingleInstancePerUser => true;

    /// <inheritdoc />
    public override AppResourceProfileManifest Resources => new(
        BaselineCpuWeight: 0.02,
        BurstCpuWeight: 0.1,
        BaselineMemoryWeight: 0.05,
        BurstMemoryWeight: 0.1,
        BaselineStorageIoWeight: 0.0,
        BurstStorageIoWeight: 0.0,
        BaselineNetworkIoWeight: 0.0,
        BurstNetworkIoWeight: 0.0);
}
