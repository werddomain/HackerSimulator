using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.AppSdk;

/// <summary>
/// Provides identity, granted policy, and scoped gateway access for one app instance.
/// </summary>
/// <remarks>
/// This context deliberately does not expose the root service provider, raw repositories,
/// unrestricted <c>IJSRuntime</c>, or any other concrete platform implementation. Every gateway
/// property is a narrow, capability-checked surface bound to this instance only. App code never
/// constructs this context itself; only the trusted platform-owned factory does
/// (`P1-EXEC-007`).
/// </remarks>
public interface IAppExecutionContext
{
    /// <summary>Gets the validated manifest for the running application.</summary>
    AppManifest Manifest { get; }

    /// <summary>Gets the unique identifier for this running instance.</summary>
    Guid InstanceId { get; }

    /// <summary>Gets the authenticated user identifier that initiated the operation.</summary>
    string UserId { get; }

    /// <summary>Gets the acting user's authority for this operation.</summary>
    AppAuthority UserAuthority { get; }

    /// <summary>Gets the capabilities granted to this app instance by OS policy.</summary>
    IReadOnlySet<string> GrantedCapabilities { get; }

    /// <summary>Gets the session this app instance belongs to.</summary>
    SessionId SessionId { get; }

    /// <summary>Gets the process record hosting this app instance.</summary>
    ProcessId ProcessId { get; }

    /// <summary>
    /// Gets the cancellation token owned by this app instance's process, cancelled on close, kill,
    /// logout, or shutdown.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>Gets a capability checker scoped to this app/user, exposing no other app's grants.</summary>
    ICapabilityChecker Capabilities { get; }

    /// <summary>Gets this instance's authorized, capability-checked filesystem gateway.</summary>
    IAppFileSystemGateway FileSystem { get; }

    /// <summary>Gets this instance's authorized canonical settings gateway.</summary>
    IAppSettingsGateway Settings { get; }

    /// <summary>Gets this instance's typed event bus gateway.</summary>
    IAppEventGateway Events { get; }

    /// <summary>Gets this instance's authorized notification-posting gateway.</summary>
    IAppNotificationGateway Notifications { get; }

    /// <summary>Gets this instance's structured diagnostic logging gateway.</summary>
    IAppLoggingGateway Logging { get; }

    /// <summary>Gets this instance's authorized diagnostic-log read gateway.</summary>
    IAppDiagnosticsGateway Diagnostics { get; }

    /// <summary>Gets this instance's read-only deterministic simulation clock gateway.</summary>
    IAppClockGateway Clock { get; }

    /// <summary>Gets this instance's authorized process/job gateway.</summary>
    IAppProcessGateway Processes { get; }

    /// <summary>
    /// Gets this instance's authorized ability to launch another installed application.
    /// Defaults to an unsupported gateway so existing <see cref="IAppExecutionContext"/>
    /// implementations that predate this member keep compiling; the trusted platform factory
    /// overrides it with a real implementation.
    /// </summary>
    IAppIntentGateway Intents => new UnsupportedAppIntentGateway();

    /// <summary>
    /// Gets this instance's authorized directory-change watch gateway. Defaults to an unsupported
    /// gateway so existing <see cref="IAppExecutionContext"/> implementations that predate this member
    /// keep compiling; the trusted platform factory overrides it with a real implementation.
    /// </summary>
    IAppFileSystemWatchGateway Watch => new UnsupportedAppFileSystemWatchGateway();

    /// <summary>
    /// Gets this instance's authorized ability to start, stop, or reconfigure other
    /// <see cref="AppKind.Service"/> apps. Defaults to an unsupported gateway so existing
    /// <see cref="IAppExecutionContext"/> implementations that predate this member keep compiling;
    /// the trusted platform factory overrides it with a real implementation.
    /// </summary>
    IAppServiceControlGateway Services => new UnsupportedAppServiceControlGateway();

    /// <summary>
    /// Gets this instance's permission-error notification gateway. Defaults to a no-op gateway so
    /// existing <see cref="IAppExecutionContext"/> implementations that predate this member keep
    /// compiling; the trusted platform factory overrides it with a real implementation.
    /// </summary>
    IAppPermissionErrorGateway PermissionErrors => new NoOpAppPermissionErrorGateway();
}

/// <summary>Default <see cref="IAppExecutionContext.PermissionErrors"/> for contexts that don't wire one up.</summary>
file sealed class NoOpAppPermissionErrorGateway : IAppPermissionErrorGateway
{
    public event EventHandler<AppPermissionErrorEventArgs>? PermissionDenied
    {
        add { }
        remove { }
    }
}

/// <summary>Default <see cref="IAppExecutionContext.Intents"/> for contexts that don't wire one up.</summary>
file sealed class UnsupportedAppIntentGateway : IAppIntentGateway
{
    public ValueTask<AppIntentLaunchResult> LaunchAsync(
        string appId, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide an Intents gateway.");

    public ValueTask<AppIntentOpenFileResult> OpenFileAsync(
        VirtualPath path, string? mediaType = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide an Intents gateway.");
}

/// <summary>Default <see cref="IAppExecutionContext.Watch"/> for contexts that don't wire one up.</summary>
file sealed class UnsupportedAppFileSystemWatchGateway : IAppFileSystemWatchGateway
{
    public ValueTask<ITopicChannelSubscription<FileSystemChangeEvent>> WatchAsync(
        VirtualPath path, FileSystemWatchScope scope, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide a Watch gateway.");
}

/// <summary>Default <see cref="IAppExecutionContext.Services"/> for contexts that don't wire one up.</summary>
file sealed class UnsupportedAppServiceControlGateway : IAppServiceControlGateway
{
    public ValueTask<ServiceControlResult> StartAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide a Services gateway.");

    public ValueTask<ServiceControlResult> StopAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide a Services gateway.");

    public ValueTask<ServiceStartMode> GetStartModeAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide a Services gateway.");

    public ValueTask<ServiceControlResult> SetStartModeAsync(
        string serviceAppId, ServiceStartMode mode, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This IAppExecutionContext implementation does not provide a Services gateway.");
}