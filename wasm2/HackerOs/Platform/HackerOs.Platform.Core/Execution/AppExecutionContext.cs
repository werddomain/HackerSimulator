using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.FileSystem;
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
        IAppEventGateway events,
        IAppNotificationGateway notifications,
        IAppLoggingGateway logging,
        IAppClockGateway clock,
        IAppProcessGateway processes,
        IAppIntentGateway intents,
        IAppFileSystemWatchGateway watch,
        IAppPermissionErrorGateway permissionErrors,
        IAppServiceControlGateway services,
        OneShotElevationGrantSet oneShotElevation,
        string? homeDirectory = null,
        bool isRootElevated = false)
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
        Events = events ?? throw new ArgumentNullException(nameof(events));
        Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Processes = processes ?? throw new ArgumentNullException(nameof(processes));
        Intents = intents ?? throw new ArgumentNullException(nameof(intents));
        Watch = watch ?? throw new ArgumentNullException(nameof(watch));
        PermissionErrors = permissionErrors ?? throw new ArgumentNullException(nameof(permissionErrors));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        OneShotElevation = oneShotElevation ?? throw new ArgumentNullException(nameof(oneShotElevation));
        HomeDirectory = homeDirectory ?? $"/home/{userId}";
        IsRootElevated = isRootElevated;
    }

    /// <inheritdoc />
    public bool IsRootElevated { get; }

    /// <inheritdoc />
    public string HomeDirectory { get; }

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
    public IAppEventGateway Events { get; }

    /// <inheritdoc />
    public IAppNotificationGateway Notifications { get; }

    /// <inheritdoc />
    public IAppLoggingGateway Logging { get; }


    /// <inheritdoc />
    public IAppClockGateway Clock { get; }

    /// <inheritdoc />
    public IAppProcessGateway Processes { get; }

    /// <inheritdoc />
    public IAppIntentGateway Intents { get; }

    /// <inheritdoc />
    public IAppFileSystemWatchGateway Watch { get; }

    /// <inheritdoc />
    public IAppPermissionErrorGateway PermissionErrors { get; }

    /// <inheritdoc />
    public IAppServiceControlGateway Services { get; }

    /// <inheritdoc />
    public OneShotElevationGrantSet OneShotElevation { get; }
}
