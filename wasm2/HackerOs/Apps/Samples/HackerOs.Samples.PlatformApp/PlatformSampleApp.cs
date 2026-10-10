using HackerOs.App.Abstractions;

namespace HackerOs.Samples.PlatformApp;

/// <summary>
/// Manifest of "Platform Demo" (ADR 0047). The build generates <c>manifest.json</c> from this
/// class; it is never instantiated at runtime.
/// </summary>
[AppManifest]
public sealed class PlatformSampleApp : AppDefinition
{
    /// <inheritdoc />
    public override string Id => "org.hackeros.samples.platform-app";

    /// <inheritdoc />
    public override string Name => "Platform Demo";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(PlatformDemoWindow);

    /// <inheritdoc />
    public override Type? MobileEntryPoint => typeof(PlatformDemoWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.hackeros";

    /// <inheritdoc />
    public override string Description =>
        "Demonstrates a manifest with a shared entry point covering multiple platforms.";
}
