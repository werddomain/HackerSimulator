using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Apps;
using HackerOs.AppSdk.Clipboard;
using HackerOs.AppSdk.Diagnostics;
using HackerOs.AppSdk.IO;
using HackerOs.AppSdk.Net;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.WorkerHost.Worker;

/// <summary>
/// The <see cref="IAppExecutionContext"/> of a process running in a worker (ADR 0070, section 4).
/// Its identity is the snapshot the OS took when it created the process; its gateways are remote:
/// each SDK call becomes a message served on the main thread by the process's trusted context, with
/// the same capability checks. The SDK does not change: a command cannot tell where it runs.
/// </summary>
/// <remarks>
/// The administration gateways (system settings, permissions, server connection, power, storage
/// diagnostics, app management), the interactive shell host, typed events and directory watches are
/// not offered: they keep the SDK's "unsupported" default, and an app that needs them declares
/// <see cref="AppExecutionHost.MainThread"/>.
/// </remarks>
internal sealed class RemoteAppExecutionContext : IAppExecutionContext
{
    private readonly RemotePermissionErrorGateway _permissionErrors = new();
    private readonly CancellationToken _cancellationToken;
    private EventHandler<HackStopRequestedEventArgs>? _stopRequested;

    public RemoteAppExecutionContext(
        RemoteProcessSnapshot snapshot,
        RemoteProcessChannel channel,
        WorkerClock clock,
        IAppConsoleGateway console,
        CancellationToken cancellationToken)
    {
        Manifest = snapshot.Manifest;
        InstanceId = snapshot.InstanceId;
        UserId = snapshot.UserId;
        HomeDirectory = snapshot.HomeDirectory;
        IsRootElevated = snapshot.IsRootElevated;
        UserLoginName = snapshot.UserLoginName;
        UserDisplayName = snapshot.UserDisplayName;
        UserAuthority = snapshot.UserAuthority;
        GrantedCapabilities = snapshot.GrantedCapabilities;
        SessionId = snapshot.SessionId;
        ProcessId = snapshot.Process.Pid;
        _cancellationToken = cancellationToken;
        Capabilities = new RemoteCapabilityChecker(snapshot.Manifest.Id, snapshot.UserId, snapshot.GrantedCapabilities);
        FileSystem = new RemoteFileSystemGateway(channel);
        Logging = new RemoteLoggingGateway(channel);
        Clock = new RemoteClockGateway(channel, clock);
        Processes = new RemoteProcessGateway(channel, snapshot.Process);
        Notifications = new RemoteNotificationGateway(channel);
        Intents = new RemoteIntentGateway(channel);
        Services = new RemoteServiceControlGateway(channel);
        Environment = new RemoteEnvironmentGateway(
            channel, snapshot.Arguments, snapshot.CurrentDirectory,
            new Dictionary<string, string>(snapshot.Variables, StringComparer.Ordinal),
            snapshot.EnvironmentUserName, snapshot.MachineName);
        Network = new RemoteNetworkGateway(channel);
        Catalog = new RemoteCatalogGateway(channel);
        Drives = new RemoteDriveGateway(channel);
        Clipboard = new RemoteClipboardGateway(channel);
        Console = console;
    }

    public AppManifest Manifest { get; }

    public Guid InstanceId { get; }

    public string UserId { get; }

    public string HomeDirectory { get; }

    public bool IsRootElevated { get; }

    public string UserLoginName { get; }

    public string UserDisplayName { get; }

    public AppAuthority UserAuthority { get; }

    public IReadOnlySet<string> GrantedCapabilities { get; }

    public SessionId SessionId { get; }

    public ProcessId ProcessId { get; }

    public CancellationToken CancellationToken => _cancellationToken;

    public event EventHandler<HackStopRequestedEventArgs>? StopRequested
    {
        add => _stopRequested += value;
        remove => _stopRequested -= value;
    }

    public HackStopRequestedEventArgs? StopRequest { get; private set; }

    public ICapabilityChecker Capabilities { get; }

    public IAppFileSystemGateway FileSystem { get; }

    public IAppEventGateway Events { get; } = new RemoteEventGateway();

    public IAppNotificationGateway Notifications { get; }

    public IAppLoggingGateway Logging { get; }

    public IAppClockGateway Clock { get; }

    public IAppProcessGateway Processes { get; }

    public IAppIntentGateway Intents { get; }

    public IAppFileSystemWatchGateway Watch { get; } = new RemoteWatchGateway();

    public IAppServiceControlGateway Services { get; }

    public IAppPermissionErrorGateway PermissionErrors => _permissionErrors;

    public IAppEnvironmentGateway Environment { get; }

    public IAppNetworkGateway Network { get; }

    public IAppCatalogGateway Catalog { get; }

    public IAppDriveGateway Drives { get; }

    public IAppClipboardGateway Clipboard { get; }

    public IAppConsoleGateway Console { get; }

    /// <summary>
    /// Delivers the OS's <c>SIGTERM</c> (ADR 0069, section 1) once: records it, then raises
    /// <see cref="StopRequested"/>. The caller cancels the process token afterwards.
    /// </summary>
    /// <returns>The event data, whose deferred work the worker reports back to the OS.</returns>
    public HackStopRequestedEventArgs? DeliverStopRequest(ProcessExitReason reason, DateTimeOffset requestedAt, DateTimeOffset deadline)
    {
        if (StopRequest is not null)
        {
            return null;
        }

        HackStopRequestedEventArgs request = new(reason, requestedAt, deadline);
        StopRequest = request;
        if (_stopRequested is { } handlers)
        {
            foreach (EventHandler<HackStopRequestedEventArgs> handler in handlers.GetInvocationList().Cast<EventHandler<HackStopRequestedEventArgs>>())
            {
                try
                {
                    handler(this, request);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // A faulty handler must not keep the OS from stopping the process.
                }
            }
        }

        return request;
    }

    /// <summary>Raises <see cref="IAppPermissionErrorGateway.PermissionDenied"/> for a denial the OS reported.</summary>
    public void RaisePermissionDenied(HackerOs.Simulation.Abstractions.FileSystem.FileSystemError error) => _permissionErrors.Raise(error);
}

/// <summary>
/// What the OS tells a worker about a process it starts there: identity, policy, environment and the
/// process record, as of the moment the OS created it.
/// </summary>
internal sealed record RemoteProcessSnapshot(
    AppManifest Manifest,
    Guid InstanceId,
    string UserId,
    string HomeDirectory,
    bool IsRootElevated,
    string UserLoginName,
    string UserDisplayName,
    AppAuthority UserAuthority,
    IReadOnlySet<string> GrantedCapabilities,
    SessionId SessionId,
    ProcessRecord Process,
    IReadOnlyList<string> Arguments,
    string CurrentDirectory,
    IReadOnlyDictionary<string, string> Variables,
    string EnvironmentUserName,
    string MachineName);
