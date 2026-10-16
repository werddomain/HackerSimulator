using HackerOs.App.Abstractions;

namespace HackerOs.Samples.ServiceApp;

/// <summary>
/// Manifest of "Sample Ticker Service" (ADR 0047). The build generates <c>manifest.json</c> from this
/// class; it is never instantiated at runtime.
/// </summary>
// ADR 0070: stays on the main thread: it publishes typed topic messages (SampleTickerEvent), .NET
// objects of the OS's heap that a worker cannot hand to their subscribers.
[AppManifest(Execution = AppExecutionHost.MainThread)]
public sealed class ServiceSampleApp : AppDefinition
{
    /// <inheritdoc />
    public override string Id => SampleTickerService.AppId;

    /// <inheritdoc />
    public override string Name => "Sample Ticker Service";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Service;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(SampleTickerService);

    /// <inheritdoc />
    public override string PublisherId => "pub.hackeros";

    /// <inheritdoc />
    public override string Description =>
        "Deterministic status and ticker background service for HackerOS sessions";

    /// <inheritdoc />
    public override AppLaunchVisibility LaunchVisibility => AppLaunchVisibility.Hidden;

    /// <inheritdoc />
    public override IReadOnlyList<string> Capabilities => [AppCapabilities.NotificationsPost];

    /// <inheritdoc />
    public override bool AutoStart => true;

    /// <inheritdoc />
    public override AppResourceProfileManifest Resources => new(
        BaselineCpuWeight: 0.01,
        BurstCpuWeight: 0.05,
        BaselineMemoryWeight: 0.02,
        BurstMemoryWeight: 0.05,
        BaselineStorageIoWeight: 0.0,
        BurstStorageIoWeight: 0.0,
        BaselineNetworkIoWeight: 0.0,
        BurstNetworkIoWeight: 0.0);
}
