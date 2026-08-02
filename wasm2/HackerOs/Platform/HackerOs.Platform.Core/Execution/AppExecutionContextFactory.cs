using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
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
    private readonly ISettingsDocumentService _settings;
    private readonly IEventBus _eventBus;
    private readonly INotificationQueue _notifications;
    private readonly IDiagnosticSink _diagnostics;
    private readonly ISimulationClock _clock;
    private readonly IProcessManager _processManager;

    /// <summary>Initializes the factory with every platform singleton gateways delegate to.</summary>
    public AppExecutionContextFactory(
        ICapabilityGrantRepository grantRepository,
        IFileSystemService fileSystem,
        ISettingsDocumentService settings,
        IEventBus eventBus,
        INotificationQueue notifications,
        IDiagnosticSink diagnostics,
        ISimulationClock clock,
        IProcessManager processManager)
    {
        _grantRepository = grantRepository ?? throw new ArgumentNullException(nameof(grantRepository));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
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
    public IAppExecutionContext Create(
        AppManifest manifest,
        AuthenticatedPrincipal principal,
        ProcessRecord process,
        IReadOnlySet<string> grantedCapabilities,
        Guid? instanceId = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(grantedCapabilities);

        string userId = principal.UserId.ToString();
        string[] groupIds = [.. principal.GroupIds.Select(g => g.ToString())];

        AppOperationContext operationContext = new()
        {
            AppId = manifest.Id,
            UserId = userId,
            UserAuthority = principal.Authority,
            GrantedCapabilities = grantedCapabilities,
            IsSystemOperation = false
        };

        ICapabilityChecker capabilities = new AppCapabilityChecker(
            _grantRepository, manifest.Id, userId, principal.Authority);

        CancellationToken cancellationToken = _processManager.GetCancellationToken(process.Pid);

        return new AppExecutionContext(
            manifest,
            instanceId ?? Guid.NewGuid(),
            userId,
            principal.Authority,
            grantedCapabilities,
            process.SessionId,
            process.Pid,
            cancellationToken,
            capabilities,
            new AppFileSystemGateway(_fileSystem, _clock, operationContext, groupIds),
            new AppSettingsGateway(_settings, operationContext),
            new AppEventGateway(_eventBus),
            new AppNotificationGateway(_notifications, _clock, capabilities, manifest.Id, principal.UserId),
            new AppLoggingGateway(_diagnostics, _clock, manifest.Id),
            new AppClockGateway(_clock),
            new AppProcessGateway(_processManager, capabilities, process.Pid));
    }
}
