using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Apps;
using HackerOs.AppSdk.Shell;
using HackerOs.AppSdk.Net;
using HackerOs.AppSdk.Clipboard;
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
        IAppEnvironmentGateway? environment = null,
        bool isRootElevated = false,
        string? userLoginName = null,
        string? userDisplayName = null,
        IAppCatalogGateway? catalog = null,
        IAppManagementGateway? appManagement = null,
        IAppNetworkGateway? network = null,
        IAppClipboardGateway? clipboard = null)
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
        Environment = environment
            ?? new DetachedAppEnvironmentGateway(manifest.Id, HomeDirectory, userId, static () => "hackeros");
        IsRootElevated = isRootElevated;
        UserLoginName = userLoginName ?? Environment.UserName;
        UserDisplayName = userDisplayName ?? UserLoginName;
        Catalog = catalog ?? UnsupportedAppGateways.Catalog;
        AppManagement = appManagement ?? UnsupportedAppGateways.Management;
        Network = network ?? UnsupportedNetworkGateway.Instance;
        Clipboard = clipboard ?? new AppClipboardGateway(new Clipboard.SystemClipboard(), capabilities, sessionId);
    }

    /// <inheritdoc />
    public string UserLoginName { get; }

    /// <inheritdoc />
    public string UserDisplayName { get; }

    /// <inheritdoc />
    public IAppCatalogGateway Catalog { get; }

    /// <inheritdoc />
    public IAppManagementGateway AppManagement { get; }

    /// <inheritdoc />
    public IAppNetworkGateway Network { get; }

    /// <inheritdoc />
    public IAppClipboardGateway Clipboard { get; }

    /// <inheritdoc />
    public IAppEnvironmentGateway Environment { get; }

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

    /// <inheritdoc />
    /// <remarks>
    /// Bound by <see cref="AppExecutionContextFactory"/> right after construction, because the
    /// gateway needs this context (the shell's parent process); unsupported until then.
    /// </remarks>
    public IAppShellHostGateway ShellHost { get; internal set; } = UnsupportedShellHostGateway.Instance;

    /// <summary>Shell host of a context whose factory has no shell service (tests, legacy hosts).</summary>
    internal sealed class UnsupportedShellHostGateway : IAppShellHostGateway
    {
        public static readonly UnsupportedShellHostGateway Instance = new();

        public Task<IHackShell> StartAsync(HackShellOptions options, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This execution context factory was not configured with a shell host service.");
    }

    /// <summary>Network gateway of a context built without a simulated network (tests).</summary>
    private sealed class UnsupportedNetworkGateway : IAppNetworkGateway
    {
        public static readonly UnsupportedNetworkGateway Instance = new();

        private const string Reason = "This execution context factory was not configured with a network.";

        public ValueTask<HackNetworkResult<HackHostEntry>> ResolveAsync(string hostName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Reason);

        public ValueTask<HackNetworkResult<HackHostEntry>> ReverseLookupAsync(
            System.Net.IPAddress address, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Reason);

        public ValueTask<HackNetworkResult<HackPingReply>> PingAsync(
            string hostNameOrAddress, TimeSpan timeout, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Reason);

        public ValueTask<HackNetworkResult<HackHttpResponse>> SendAsync(
            HackHttpRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Reason);

        public ValueTask<HackNetworkResult<IReadOnlyList<HackPortProbeResult>>> ProbeTcpAsync(
            string hostNameOrAddress, int firstPort, int lastPort, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Reason);

        public ValueTask<HackNetworkResult<HackOsFingerprint?>> DetectOperatingSystemAsync(
            string hostNameOrAddress, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Reason);
    }
}

/// <summary>Gateways used when the factory was built without the app catalog services (tests, minimal hosts).</summary>
internal static class UnsupportedAppGateways
{
    private const string Message = "This execution context factory was not configured with the app catalog services.";

    /// <summary>Catalog gateway whose reads throw <see cref="NotSupportedException"/>.</summary>
    public static readonly IAppCatalogGateway Catalog = new UnsupportedCatalog();

    /// <summary>Management gateway whose calls throw <see cref="NotSupportedException"/>.</summary>
    public static readonly IAppManagementGateway Management = new UnsupportedManagement();

    private sealed class UnsupportedCatalog : IAppCatalogGateway
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public ValueTask<IReadOnlyList<HackAppInfo>> GetInstalledAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);

        public ValueTask<IReadOnlyList<HackAppInfo>> GetHandlersAsync(
            VirtualPath path, string? mediaType = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);
    }

    private sealed class UnsupportedManagement : IAppManagementGateway
    {
        public ValueTask<IReadOnlyList<HackAppInstallInfo>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);

        public ValueTask<IReadOnlyList<string>> UninstallAsync(
            string appId, HackInstallScope scope, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);

        public ValueTask<IReadOnlyList<string>> ReinstallAsync(
            string appId, HackInstallScope scope, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(Message);
    }
}
