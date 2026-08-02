using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Trusted, immutable implementation of <see cref="IAppExecutionContext"/> constructed only by
/// <see cref="AppExecutionContextFactory"/> (`P1-EXEC-007`). App code never constructs this type.
/// </summary>
internal sealed class AppExecutionContext : IAppExecutionContext
{
    /// <summary>Initializes a fully wired app execution context.</summary>
    internal AppExecutionContext(
        AppManifest manifest,
        Guid instanceId,
        string userId,
        AppAuthority userAuthority,
        IReadOnlySet<string> grantedCapabilities,
        SessionId sessionId,
        ProcessId processId,
        CancellationToken cancellationToken,
        ICapabilityChecker capabilities,
        IAppFileSystemGateway fileSystem,
        IAppSettingsGateway settings,
        IAppEventGateway events,
        IAppNotificationGateway notifications,
        IAppLoggingGateway logging,
        IAppClockGateway clock,
        IAppProcessGateway processes)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        InstanceId = instanceId;
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        UserId = userId;
        UserAuthority = userAuthority;
        GrantedCapabilities = grantedCapabilities ?? throw new ArgumentNullException(nameof(grantedCapabilities));
        SessionId = sessionId;
        ProcessId = processId;
        CancellationToken = cancellationToken;
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Processes = processes ?? throw new ArgumentNullException(nameof(processes));
    }

    /// <inheritdoc />
    public AppManifest Manifest { get; }

    /// <inheritdoc />
    public Guid InstanceId { get; }

    /// <inheritdoc />
    public string UserId { get; }

    /// <inheritdoc />
    public AppAuthority UserAuthority { get; }

    /// <inheritdoc />
    public IReadOnlySet<string> GrantedCapabilities { get; }

    /// <inheritdoc />
    public SessionId SessionId { get; }

    /// <inheritdoc />
    public ProcessId ProcessId { get; }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc />
    public ICapabilityChecker Capabilities { get; }

    /// <inheritdoc />
    public IAppFileSystemGateway FileSystem { get; }

    /// <inheritdoc />
    public IAppSettingsGateway Settings { get; }

    /// <inheritdoc />
    public IAppEventGateway Events { get; }

    /// <inheritdoc />
    public IAppNotificationGateway Notifications { get; }

    /// <inheritdoc />
    public IAppLoggingGateway Logging { get; }

    /// <inheritdoc />
    public IAppClockGateway Clock { get; }

    /// <inheritdoc />
    public IAppProcessGateway Processes { get; }
}
