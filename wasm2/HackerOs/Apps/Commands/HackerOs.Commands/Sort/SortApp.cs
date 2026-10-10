using HackerOs.App.Abstractions;

namespace HackerOs.Commands.Sort;

/// <summary>
/// Manifest of "sort" (ADR 0047). The build generates <c>manifests/&lt;appId&gt;.json</c> from this
/// class; it is never instantiated at runtime.
/// </summary>
[AppManifest]
public sealed class SortApp : AppDefinition
{
    /// <inheritdoc />
    public override string Id => "org.hackeros.commands.sort";

    /// <inheritdoc />
    public override string Name => "sort";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Terminal;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(SortCommand);

    /// <inheritdoc />
    public override string PublisherId => "pub.hackeros";

    /// <inheritdoc />
    public override string Description => "Sort lines of text files";

    /// <inheritdoc />
    public override AppLaunchVisibility LaunchVisibility => AppLaunchVisibility.Hidden;

    /// <inheritdoc />
    public override IReadOnlyList<string> Capabilities =>
        [AppCapabilities.FileSystemUserHomeRead, AppCapabilities.FileSystemSystemRead];

    /// <inheritdoc />
    public override TerminalCommand? Terminal => new("sort", "sort [-r] [-n] [-u] [file...]");

    /// <inheritdoc />
    public override AppResourceProfileManifest Resources => new(
        BaselineCpuWeight: 0.01,
        BurstCpuWeight: 0.08,
        BaselineMemoryWeight: 0.01,
        BurstMemoryWeight: 0.05,
        BaselineStorageIoWeight: 0.05,
        BurstStorageIoWeight: 0.2,
        BaselineNetworkIoWeight: 0.0,
        BurstNetworkIoWeight: 0.0);
}
