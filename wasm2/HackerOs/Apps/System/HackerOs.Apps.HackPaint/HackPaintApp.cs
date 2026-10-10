using HackerOs.App.Abstractions;

namespace HackerOs.Apps.HackPaint;

/// <summary>
/// Manifest of "Hack Paint" (ADR 0047). The build generates <c>manifest.json</c> from this
/// class; it is never instantiated at runtime.
/// </summary>
[AppManifest]
public sealed class HackPaintApp : AppDefinition
{
    /// <inheritdoc />
    public override string Id => "org.hackeros.hack-paint";

    /// <inheritdoc />
    public override string Name => "Hack Paint";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(HackPaintWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.hackeros";

    /// <inheritdoc />
    public override string Description =>
        "Virtual image editor with drawing canvas, filters, undo/redo, and file dialog export";

    /// <inheritdoc />
    public override string Category => "graphics";

    /// <inheritdoc />
    public override IReadOnlyList<string> Capabilities =>
        [AppCapabilities.FileSystemUserHomeRead, AppCapabilities.FileSystemUserHomeWrite, AppCapabilities.DialogFileOpen, AppCapabilities.DialogFileSave];

    /// <inheritdoc />
    public override AppResourceProfileManifest Resources => new(
        BaselineCpuWeight: 0.05,
        BurstCpuWeight: 0.25,
        BaselineMemoryWeight: 0.08,
        BurstMemoryWeight: 0.3,
        BaselineStorageIoWeight: 0.05,
        BurstStorageIoWeight: 0.2,
        BaselineNetworkIoWeight: 0.0,
        BurstNetworkIoWeight: 0.0);
}
