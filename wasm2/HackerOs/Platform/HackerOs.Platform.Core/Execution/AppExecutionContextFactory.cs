using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Platform.Core.Diagnostics;
using HackerOs.Platform.Core.Intents;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Notifications;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Simulation.Abstractions.Time;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// The sole trusted constructor for <see cref="IAppExecutionContext"/> instances (`P1-EXEC-007`).
/// App constructors and components never construct or elevate their own context; only platform
/// code that already holds a validated manifest, authenticated principal, and hosting process
/// record may call this factory.
/// </summary>
public sealed class AppExecutionContextFactory
{
    private readonly ICapabilityGrantRepository _grantRepository;
    private readonly IFileSystemService _fileSystem;
    private readonly IEventBus _eventBus;
    private readonly ITopicMessageBus _topicBus;
    private readonly INotificationQueue _notifications;
    private readonly IDiagnosticSink _diagnostics;
    private readonly ISimulationClock _clock;
    private readonly IProcessManager _processManager;
    private readonly Func<AppIntentDispatcher>? _intentDispatcherProvider;
    private readonly LogFileWriter? _logWriter;

    /// <summary>Initializes the factory with every platform singleton gateways delegate to.</summary>
    /// <param name="intentDispatcherProvider">
    /// Lazily resolves the app-intent dispatcher for the <see cref="IAppExecutionContext.Intents"/>
    /// gateway. Resolved lazily (not injected eagerly) because <see cref="AppIntentDispatcher"/>
    /// depends on the app lifecycle orchestrator, which itself depends on this factory to create
    /// execution contexts for newly launched processes -- an eager dependency here would be
    /// circular. Pass <see langword="null"/> (e.g. in tests) to leave app-launch unsupported.
    /// </param>
    /// <param name="logWriter">
    /// Platform log writer the <see cref="IAppExecutionContext.Logging"/> gateway appends to
    /// (<c>/var/log/apps/&lt;appId&gt;.log</c>, ADR 0045); <see langword="null"/> keeps app logs in
    /// the in-memory buffer only.
    /// </param>
    public AppExecutionContextFactory(
        ICapabilityGrantRepository grantRepository,
        IFileSystemService fileSystem,
        IEventBus eventBus,
        ITopicMessageBus topicBus,
        INotificationQueue notifications,
        IDiagnosticSink diagnostics,
        ISimulationClock clock,
        IProcessManager processManager,
        Func<AppIntentDispatcher>? intentDispatcherProvider = null,
        LogFileWriter? logWriter = null)
    {
        _grantRepository = grantRepository ?? throw new ArgumentNullException(nameof(grantRepository));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _topicBus = topicBus ?? throw new ArgumentNullException(nameof(topicBus));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _intentDispatcherProvider = intentDispatcherProvider;
        _logWriter = logWriter;
    }

    /// <summary>
    /// Creates a new trusted context for one running app instance, wiring every scoped gateway to
    /// the acting app/user/process.
    /// </summary>
    /// <param name="manifest">Validated manifest of the running application.</param>
    /// <param name="principal">Authenticated principal that owns the hosting session.</param>
    /// <param name="process">Process record hosting this app instance; must already be active.</param>
    /// <param name="grantedCapabilities">Exact capabilities trusted policy granted for this instance.</param>
    /// <param name="instanceId">Explicit instance ID, or <see langword="null"/> to generate one.</param>
    /// <param name="isRootElevated">
    /// Whether this one launch was elevated to full unrestricted root via <c>sudo</c> (`ADR 0042`),
    /// after a caller-supplied elevation ticket was already consumed.
    /// </param>
    /// <param name="oneShotElevation">
    /// A live one-shot elevation ticket set to bind into this instance, when the caller pre-filled
    /// one from an already-consumed capability-approval ticket; otherwise a fresh empty set is used
    /// so an app can still receive a same-instance admin-approval grant later.
    /// </param>
    public IAppExecutionContext Create(
        AppManifest manifest,
        AuthenticatedPrincipal principal,
        ProcessRecord process,
        IReadOnlySet<string> grantedCapabilities,
        Guid? instanceId = null,
        bool isRootElevated = false,
        OneShotElevationGrantSet? oneShotElevation = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(grantedCapabilities);

        string userId = principal.UserId.ToString();
        string userName = principal.LoginName.Value;
        string[] groupIds = [.. principal.GroupIds.Select(g => g.ToString())];

        AppOperationContext operationContext = new()
        {
            AppId = manifest.Id,
            UserId = userId,
            UserName = userName,
            ProcessId = process.Pid.Value,
            UserAuthority = principal.Authority,
            GrantedCapabilities = grantedCapabilities,
            IsSystemOperation = false,
            IsRootElevated = isRootElevated,
            OneShotElevation = oneShotElevation ?? new OneShotElevationGrantSet()
        };

        ICapabilityChecker capabilities = new AppCapabilityChecker(
            _grantRepository, manifest.Id, userId, principal.Authority);

        CancellationToken cancellationToken = _processManager.GetCancellationToken(process.Pid);

        IAppIntentGateway intents = _intentDispatcherProvider is not null
            ? new AppIntentGateway(_intentDispatcherProvider, manifest.Id, userId, principal)
            : UnsupportedIntentGateway.Instance;

        IAppServiceControlGateway services = _intentDispatcherProvider is not null
            ? new AppServiceControlGateway(_intentDispatcherProvider, manifest.Id, userId, principal)
            : UnsupportedServiceControlGateway.Instance;

        AppPermissionErrorGateway permissionErrors = new();
        AppEventGateway events = new(_eventBus, _topicBus, manifest.Id, userId, process.Pid.ToString());
        AppClockGateway clock = new(_clock);

        AppExecutionContext context = new(
            manifest,
            instanceId ?? process.AppInstanceId.Value,
            userId,
            principal.Authority,
            grantedCapabilities,
            process.SessionId,
            process.Pid,
            cancellationToken,
            capabilities,
            new AppFileSystemGateway(_fileSystem, _clock, operationContext, groupIds, notifyPermissionError: permissionErrors.Raise),
            events,
            new AppNotificationGateway(_notifications, _clock, capabilities, manifest.Id, principal.UserId),
            new AppLoggingGateway(_diagnostics, _clock, manifest.Id, _logWriter, process.Pid.Value),
            clock,
            new AppProcessGateway(_processManager, capabilities, process.Pid),
            intents,
            new AppFileSystemWatchGateway(_fileSystem, _topicBus, _clock, operationContext, groupIds, process.Pid.ToString()),
            permissionErrors,
            services,
            operationContext.OneShotElevation,
            principal.HomePath,
            isRootElevated);

        // ADR 0043: callbacks these gateways invoke on the app's behalf re-enter its ambient context.
        events.BindAmbientContext(context);
        clock.BindAmbientContext(context);
        return context;
    }

    private sealed class UnsupportedIntentGateway : IAppIntentGateway
    {
        public static readonly UnsupportedIntentGateway Instance = new();

        public ValueTask<AppIntentLaunchResult> LaunchAsync(
            string appId, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "This execution context factory was not configured with an app-intent dispatcher.");

        public ValueTask<AppIntentOpenFileResult> OpenFileAsync(
            VirtualPath path, string? mediaType = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "This execution context factory was not configured with an app-intent dispatcher.");
    }

    private sealed class UnsupportedServiceControlGateway : IAppServiceControlGateway
    {
        public static readonly UnsupportedServiceControlGateway Instance = new();

        public ValueTask<ServiceControlResult> StartAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "This execution context factory was not configured with an app-intent dispatcher.");

        public ValueTask<ServiceControlResult> StopAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "This execution context factory was not configured with an app-intent dispatcher.");

        public ValueTask<ServiceStartMode> GetStartModeAsync(string serviceAppId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "This execution context factory was not configured with an app-intent dispatcher.");

        public ValueTask<ServiceControlResult> SetStartModeAsync(
            string serviceAppId, ServiceStartMode mode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "This execution context factory was not configured with an app-intent dispatcher.");
    }
}
