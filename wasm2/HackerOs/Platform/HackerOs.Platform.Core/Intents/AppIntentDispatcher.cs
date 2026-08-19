using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Intents;

/// <summary>Identifies the stable outcome of dispatching one app intent.</summary>
public enum AppIntentDispatchStatus
{
    /// <summary>The intent was authorized and carried out (or acknowledged, for deferred UI intents).</summary>
    Dispatched,

    /// <summary>The calling app lacks the capability required to carry out this intent.</summary>
    CapabilityDenied,

    /// <summary>No catalog app matches the intent's target.</summary>
    NotFound,

    /// <summary>The resolved target app is currently disabled.</summary>
    Disabled,

    /// <summary>Multiple candidate apps can handle an <see cref="OpenFileIntent"/>; the caller must choose.</summary>
    ChooserRequired,

    /// <summary>An <see cref="OpenFileIntent"/>'s explicit preferred app is invalid.</summary>
    TargetInvalid,

    /// <summary>The resolved target's entry point faulted while starting or executing.</summary>
    EntryPointFault
}

/// <summary>Contains the result of dispatching one <see cref="IAppIntent"/>, per `P1-APP-007`.</summary>
/// <param name="Status">Stable dispatch outcome.</param>
/// <param name="Process">Process created for the resolved target, when one was launched.</param>
/// <param name="Context">Execution context for the resolved target, when one was launched.</param>
/// <param name="ExitCode">Exit code for a completed <see cref="ExecuteCommandIntent"/> or Terminal <see cref="OpenFileIntent"/>.</param>
/// <param name="StandardOutput">Captured standard output for a completed Terminal launch.</param>
/// <param name="StandardError">Captured standard error for a completed Terminal launch.</param>
/// <param name="CandidateAppIds">Candidate app IDs, populated for <see cref="AppIntentDispatchStatus.ChooserRequired"/>.</param>
/// <param name="ErrorCode">Stable machine-readable error code when dispatch did not succeed.</param>
public sealed record AppIntentDispatchResult(
    AppIntentDispatchStatus Status,
    ProcessRecord? Process = null,
    IAppExecutionContext? Context = null,
    int? ExitCode = null,
    string? StandardOutput = null,
    string? StandardError = null,
    IReadOnlyList<string>? CandidateAppIds = null,
    string? ErrorCode = null);

/// <summary>Identifies the stable outcome of one service-control request (start/stop/get/set start mode).</summary>
public enum ServiceControlDispatchStatus
{
    /// <summary>The request completed: the service was started/stopped, or its start mode was read/set.</summary>
    Succeeded,

    /// <summary>No catalog app matches the requested target ID.</summary>
    NotFound,

    /// <summary>The resolved target app is not a <see cref="AppKind.Service"/> app.</summary>
    NotAService,

    /// <summary>
    /// The caller is neither in the same assembly as the target service nor holds
    /// <see cref="AppCapabilities.ServicesManage"/>.
    /// </summary>
    CapabilityDenied,

    /// <summary>The target service's start mode is <see cref="ServiceStartMode.Disabled"/>; it cannot be started.</summary>
    ServiceDisabled,

    /// <summary>The target's entry point faulted while starting.</summary>
    Faulted
}

/// <summary>Contains the result of one service-control request.</summary>
/// <param name="Status">Stable request outcome.</param>
/// <param name="ErrorCode">Stable machine-readable error code when the outcome did not succeed.</param>
public sealed record ServiceControlDispatchResult(ServiceControlDispatchStatus Status, string? ErrorCode = null);

/// <summary>
/// Capability-gates and dispatches every typed <see cref="IAppIntent"/> to the lifecycle
/// orchestrator or file-association resolver, per `P1-APP-007`. This is the policy layer:
/// it decides whether a request is allowed before <see cref="AppLifecycleOrchestrator"/> (the
/// mechanism layer) carries it out.
/// </summary>
public sealed class AppIntentDispatcher
{
    private readonly AppLifecycleOrchestrator _orchestrator;
    private readonly AppCatalog _catalog;
    private readonly IAppEnablementRegistry _enablement;
    private readonly FileAssociationResolver _associations;
    private readonly ICapabilityGrantRepository _grants;
    private readonly IFileSystemService _fileSystem;

    /// <summary>Initializes the dispatcher over the orchestrator, catalog, resolver, grant repository, and filesystem.</summary>
    public AppIntentDispatcher(
        AppLifecycleOrchestrator orchestrator,
        AppCatalog catalog,
        IAppEnablementRegistry enablement,
        FileAssociationResolver associations,
        ICapabilityGrantRepository grants,
        IFileSystemService fileSystem)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _enablement = enablement ?? throw new ArgumentNullException(nameof(enablement));
        _associations = associations ?? throw new ArgumentNullException(nameof(associations));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary>Dispatches one intent request on behalf of an authenticated principal.</summary>
    /// <param name="request">Caller app, acting user, and typed intent.</param>
    /// <param name="principal">Authenticated principal the request runs as. Must match <paramref name="request"/>'s user.</param>
    /// <param name="fullScreen">Optional alternate-screen renderer supplied to an executed Terminal command.</param>
    /// <param name="cancellationToken">Cancels command execution without affecting unrelated intents.</param>
    public async ValueTask<AppIntentDispatchResult> DispatchAsync(
        AppIntentRequest request,
        AuthenticatedPrincipal principal,
        IFullScreenTerminalSession? fullScreen = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(principal);
        if (!string.Equals(request.UserId, principal.UserId.ToString(), StringComparison.Ordinal))
        {
            throw new ArgumentException("The intent request's user must match the acting principal.", nameof(request));
        }

        return request.Intent switch
        {
            LaunchAppIntent launch => await DispatchLaunchAsync(request, launch, principal),
            OpenFileIntent openFile => await DispatchOpenFileAsync(request, openFile, principal),
            ExecuteCommandIntent execute => await DispatchExecuteCommandAsync(
                request, execute, principal, fullScreen, cancellationToken),
            RevealFileIntent => new AppIntentDispatchResult(AppIntentDispatchStatus.Dispatched),
            ShowAppSettingsIntent showSettings => DispatchShowSettings(showSettings),
            _ => new AppIntentDispatchResult(AppIntentDispatchStatus.NotFound, ErrorCode: "intent.unknown")
        };
    }

    private bool HasAppsLaunch(string callerAppId, string userId, AppAuthority actingAuthority) =>
        _grants.Evaluate(callerAppId, userId, AppCapabilities.AppsLaunch, actingAuthority, AppAuthority.User).Granted;

    private async ValueTask<AppIntentDispatchResult> DispatchLaunchAsync(
        AppIntentRequest request, LaunchAppIntent intent, AuthenticatedPrincipal principal)
    {
        if (!string.Equals(request.CallerAppId, intent.TargetAppId, StringComparison.Ordinal)
            && !HasAppsLaunch(request.CallerAppId, request.UserId, principal.Authority))
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.CapabilityDenied, ErrorCode: "intent.capability-denied");
        }

        AppLaunchResult launch = await _orchestrator.LaunchAsync(new AppLaunchRequest(intent.TargetAppId, principal, intent.Arguments));
        return ToDispatchResult(launch);
    }

    private async ValueTask<AppIntentDispatchResult> DispatchOpenFileAsync(
        AppIntentRequest request, OpenFileIntent intent, AuthenticatedPrincipal principal)
    {
        if (!HasAppsLaunch(request.CallerAppId, request.UserId, principal.Authority))
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.CapabilityDenied, ErrorCode: "intent.capability-denied");
        }

        AppOperationContext readContext = new()
        {
            AppId = request.CallerAppId,
            UserId = request.UserId,
            UserAuthority = principal.Authority,
            GrantedCapabilities = new HashSet<string>(StringComparer.Ordinal) { AppCapabilities.FileAssociationsRead },
            IsSystemOperation = false
        };

        FileHandlerResolution resolution = await _associations.ResolveAsync(intent, readContext);
        switch (resolution.Status)
        {
            case FileHandlerResolutionStatus.ChooserRequired:
                return new AppIntentDispatchResult(AppIntentDispatchStatus.ChooserRequired, CandidateAppIds: resolution.CandidateAppIds, ErrorCode: "intent.open-file.chooser-required");
            case FileHandlerResolutionStatus.NoHandler:
                return new AppIntentDispatchResult(AppIntentDispatchStatus.NotFound, ErrorCode: "intent.open-file.no-handler");
            case FileHandlerResolutionStatus.TargetInvalid:
                return new AppIntentDispatchResult(AppIntentDispatchStatus.TargetInvalid, ErrorCode: "intent.open-file.target-invalid");
        }

        AppLaunchResult launch = await _orchestrator.LaunchAsync(
            new AppLaunchRequest(resolution.AppId!, principal, [intent.Path.Value]));
        return ToDispatchResult(launch);
    }

    private async ValueTask<AppIntentDispatchResult> DispatchExecuteCommandAsync(
        AppIntentRequest request,
        ExecuteCommandIntent intent,
        AuthenticatedPrincipal principal,
        IFullScreenTerminalSession? fullScreen,
        CancellationToken cancellationToken)
    {
        if (!HasAppsLaunch(request.CallerAppId, request.UserId, principal.Authority))
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.CapabilityDenied, ErrorCode: "intent.capability-denied");
        }

        string[] tokens = intent.CommandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.NotFound, ErrorCode: "intent.execute-command.empty");
        }

        string commandName = tokens[0];
        string[] arguments = tokens[1..];

        AppManifest? target = _catalog.Manifests.Values.FirstOrDefault(manifest =>
            manifest.Kind == AppKind.Terminal
            && manifest.Terminal is not null
            && (string.Equals(manifest.Terminal.Name, commandName, StringComparison.Ordinal)
                || manifest.Terminal.Aliases.Contains(commandName, StringComparer.Ordinal)));

        if (target is null)
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.NotFound, ErrorCode: "intent.execute-command.not-found");
        }

        if (!_enablement.IsEnabled(target.Id))
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.Disabled, ErrorCode: "intent.execute-command.disabled");
        }

        AppLaunchResult launch = await _orchestrator.LaunchAsync(
            new AppLaunchRequest(target.Id, principal, arguments, WorkingDirectory: intent.WorkingDirectory),
            fullScreen, cancellationToken);
        return ToDispatchResult(launch);
    }

    private AppIntentDispatchResult DispatchShowSettings(ShowAppSettingsIntent intent)
    {
        if (!_catalog.Manifests.ContainsKey(intent.TargetAppId))
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.NotFound, ErrorCode: "intent.show-settings.not-found");
        }

        if (!_enablement.IsEnabled(intent.TargetAppId))
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.Disabled, ErrorCode: "intent.show-settings.disabled");
        }

        // Actually focusing/rendering a settings surface belongs to the future window runtime
        // (Phase 2A); Section 8 only validates the target and acknowledges the request.
        return new AppIntentDispatchResult(AppIntentDispatchStatus.Dispatched);
    }

    /// <summary>
    /// Starts one <see cref="AppKind.Service"/> app on behalf of a caller, authorized either
    /// because the caller's entry point lives in the same assembly as the target service (a
    /// companion Window/Terminal app controlling its own bundled service) or because the caller
    /// holds <see cref="AppCapabilities.ServicesManage"/> (a "service manager" role, e.g. System
    /// Monitor). Refuses when the target's effective start mode is <see cref="ServiceStartMode.Disabled"/>.
    /// </summary>
    public async ValueTask<ServiceControlDispatchResult> StartServiceAsync(
        string callerAppId, string userId, string targetAppId, AuthenticatedPrincipal principal)
    {
        ServiceControlDispatchResult? denial = AuthorizeServiceControl(callerAppId, userId, targetAppId, principal, out AppManifest? target);
        if (denial is not null)
        {
            return denial;
        }

        ServiceStartMode effectiveMode = await ServiceStartModeStore.ReadAsync(
            _fileSystem,
            principal.LoginName.Value,
            targetAppId,
            target!.AutoStart ? ServiceStartMode.Automatic : ServiceStartMode.Manual,
            CancellationToken.None);
        if (effectiveMode == ServiceStartMode.Disabled)
        {
            return new ServiceControlDispatchResult(ServiceControlDispatchStatus.ServiceDisabled, ErrorCode: "service.start.disabled");
        }

        AppLaunchResult launch = await _orchestrator.LaunchAsync(new AppLaunchRequest(targetAppId, principal, []));
        return launch.Status switch
        {
            AppLaunchStatus.Launched or AppLaunchStatus.FocusedExisting =>
                new ServiceControlDispatchResult(ServiceControlDispatchStatus.Succeeded),
            _ => new ServiceControlDispatchResult(ServiceControlDispatchStatus.Faulted, ErrorCode: launch.ErrorCode)
        };
    }

    /// <summary>Stops one <see cref="AppKind.Service"/> app on behalf of a caller; see <see cref="StartServiceAsync"/> for the authorization rule.</summary>
    public async ValueTask<ServiceControlDispatchResult> StopServiceAsync(
        string callerAppId, string userId, string targetAppId, AuthenticatedPrincipal principal)
    {
        ServiceControlDispatchResult? denial = AuthorizeServiceControl(callerAppId, userId, targetAppId, principal, out _);
        if (denial is not null)
        {
            return denial;
        }

        await _orchestrator.StopServiceAsync(targetAppId);
        return new ServiceControlDispatchResult(ServiceControlDispatchStatus.Succeeded);
    }

    /// <summary>Reads one <see cref="AppKind.Service"/> app's effective start mode on behalf of a caller; see <see cref="StartServiceAsync"/> for the authorization rule.</summary>
    public async ValueTask<(ServiceControlDispatchResult Result, ServiceStartMode Mode)> GetServiceStartModeAsync(
        string callerAppId, string userId, string targetAppId, AuthenticatedPrincipal principal)
    {
        ServiceControlDispatchResult? denial = AuthorizeServiceControl(callerAppId, userId, targetAppId, principal, out AppManifest? target);
        if (denial is not null)
        {
            return (denial, ServiceStartMode.Manual);
        }

        ServiceStartMode mode = await ServiceStartModeStore.ReadAsync(
            _fileSystem,
            principal.LoginName.Value,
            targetAppId,
            target!.AutoStart ? ServiceStartMode.Automatic : ServiceStartMode.Manual,
            CancellationToken.None);
        return (new ServiceControlDispatchResult(ServiceControlDispatchStatus.Succeeded), mode);
    }

    /// <summary>Sets one <see cref="AppKind.Service"/> app's effective start mode on behalf of a caller; see <see cref="StartServiceAsync"/> for the authorization rule.</summary>
    public async ValueTask<ServiceControlDispatchResult> SetServiceStartModeAsync(
        string callerAppId, string userId, string targetAppId, AuthenticatedPrincipal principal, ServiceStartMode mode)
    {
        ServiceControlDispatchResult? denial = AuthorizeServiceControl(callerAppId, userId, targetAppId, principal, out _);
        if (denial is not null)
        {
            return denial;
        }

        await ServiceStartModeStore.WriteAsync(_fileSystem, principal.LoginName.Value, targetAppId, mode, CancellationToken.None);
        return new ServiceControlDispatchResult(ServiceControlDispatchStatus.Succeeded);
    }

    /// <summary>
    /// Resolves and authorizes a service-control target: it must be a catalog <see cref="AppKind.Service"/>
    /// app, and the caller must either share its resolved entry-point assembly or hold
    /// <see cref="AppCapabilities.ServicesManage"/>. Returns <see langword="null"/> and the resolved
    /// manifest when authorized; otherwise returns the denial result to return to the caller.
    /// </summary>
    private ServiceControlDispatchResult? AuthorizeServiceControl(
        string callerAppId, string userId, string targetAppId, AuthenticatedPrincipal principal, out AppManifest? target)
    {
        target = null;
        if (!_catalog.Manifests.TryGetValue(targetAppId, out AppManifest? manifest))
        {
            return new ServiceControlDispatchResult(ServiceControlDispatchStatus.NotFound, ErrorCode: "service.not-found");
        }

        if (manifest.Kind != AppKind.Service)
        {
            return new ServiceControlDispatchResult(ServiceControlDispatchStatus.NotAService, ErrorCode: "service.not-a-service");
        }

        bool sameAssembly =
            _orchestrator.TryGetDescriptor(callerAppId, out AppDescriptor? caller)
            && _orchestrator.TryGetDescriptor(targetAppId, out AppDescriptor? targetDescriptor)
            && ReferenceEquals(caller.Assembly, targetDescriptor.Assembly);

        bool hasServicesManage = _grants.Evaluate(
            callerAppId, userId, AppCapabilities.ServicesManage, principal.Authority, AppAuthority.User).Granted;

        if (!sameAssembly && !hasServicesManage)
        {
            return new ServiceControlDispatchResult(ServiceControlDispatchStatus.CapabilityDenied, ErrorCode: "service.capability-denied");
        }

        target = manifest;
        return null;
    }

    private static AppIntentDispatchResult ToDispatchResult(AppLaunchResult launch) => launch.Status switch
    {
        AppLaunchStatus.Launched or AppLaunchStatus.FocusedExisting => new AppIntentDispatchResult(
            AppIntentDispatchStatus.Dispatched, launch.Process, launch.Context, launch.ExitCode, launch.StandardOutput, launch.StandardError),
        AppLaunchStatus.NotFound => new AppIntentDispatchResult(AppIntentDispatchStatus.NotFound, ErrorCode: launch.ErrorCode),
        AppLaunchStatus.Disabled => new AppIntentDispatchResult(AppIntentDispatchStatus.Disabled, ErrorCode: launch.ErrorCode),
        _ => new AppIntentDispatchResult(AppIntentDispatchStatus.EntryPointFault, launch.Process, launch.Context, ErrorCode: launch.ErrorCode)
    };
}
