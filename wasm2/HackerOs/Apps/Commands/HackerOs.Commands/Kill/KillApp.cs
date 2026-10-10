using HackerOs.App.Abstractions;

namespace HackerOs.Commands.Kill;

/// <summary>
/// Manifest of "kill" (ADR 0047). The build generates <c>manifests/&lt;appId&gt;.json</c> from this
/// class; it is never instantiated at runtime.
/// </summary>
[AppManifest]
public sealed class KillApp : AppDefinition
{
    /// <inheritdoc />
    public override string Id => "org.hackeros.commands.kill";

    /// <inheritdoc />
    public override string Name => "kill";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Terminal;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(KillCommand);

    /// <inheritdoc />
    public override string PublisherId => "pub.hackeros";

    /// <inheritdoc />
    public override string Description => "Send a signal to a process or terminate it";

    /// <inheritdoc />
    public override AppLaunchVisibility LaunchVisibility => AppLaunchVisibility.Hidden;

    /// <inheritdoc />
    public override IReadOnlyList<string> Capabilities => [AppCapabilities.ProcessManage];

    /// <inheritdoc />
    public override TerminalCommand? Terminal => new("kill", "kill [-9] <pid...>");

    /// <inheritdoc />
    public override AppResourceProfileManifest Resources => new(
        BaselineCpuWeight: 0.01,
        BurstCpuWeight: 0.05,
        BaselineMemoryWeight: 0.01,
        BurstMemoryWeight: 0.05,
        BaselineStorageIoWeight: 0.0,
        BurstStorageIoWeight: 0.0,
        BaselineNetworkIoWeight: 0.0,
        BurstNetworkIoWeight: 0.0);
}
