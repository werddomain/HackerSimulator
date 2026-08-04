using System.Diagnostics.CodeAnalysis;
using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Platform.Core.Execution;
using HackerOs.Platform.Core.Intents;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Lifecycle;

/// <summary>
/// Starts, tracks, and stops app instances using catalog activation/deactivation order, per
/// `P1-APP-004` through `P1-APP-006` and `P1-APP-011`. This is the mechanism layer: it performs
/// every launch/stop/enable/disable it is asked to without itself deciding whether a caller is
/// allowed to ask. <see cref="AppIntentDispatcher"/> is the policy layer that capability-gates
/// requests before calling here.
/// </summary>
public sealed class AppLifecycleOrchestrator
{
    private readonly AppCatalog _catalog;
    private readonly IReadOnlyDictionary<string, AppDescriptor> _descriptors;
    private readonly AppEnablementRegistry _enablement;
    private readonly IProcessManager _processManager;
    private readonly ICapabilityGrantRepository _grants;
    private readonly AppExecutionContextFactory _contextFactory;
    private readonly ISettingsDocumentService _settings;
    private readonly IEventBus? _eventBus;
    private readonly object _sync = new();
    private readonly Dictionary<ProcessId, RunningInstance> _running = [];

    /// <summary>Initializes the orchestrator over a fully resolved discovery result.</summary>
    /// <param name="catalog">Validated, dependency-ordered app catalog.</param>
    /// <param name="descriptors">Successfully resolved entry-point descriptors for the catalog.</param>
    /// <param name="enablement">Runtime enable/disable state for catalog apps.</param>
    /// <param name="processManager">Process lifecycle manager backing every launch.</param>
    /// <param name="grants">Capability grant repository used to compute each launch's granted set.</param>
    /// <param name="contextFactory">Trusted execution-context factory.</param>
    /// <param name="settings">Canonical settings service, used to invalidate stale file-association defaults on disable.</param>
    /// <param name="eventBus">Optional typed event bus for cross-subsystem lifecycle cleanup.</param>
    public AppLifecycleOrchestrator(
        AppCatalog catalog,
        IReadOnlyDictionary<string, AppDescriptor> descriptors,
        AppEnablementRegistry enablement,
        IProcessManager processManager,
        ICapabilityGrantRepository grants,
        AppExecutionContextFactory contextFactory,
        ISettingsDocumentService settings,
        IEventBus? eventBus = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _descriptors = descriptors ?? throw new ArgumentNullException(nameof(descriptors));
        _enablement = enablement ?? throw new ArgumentNullException(nameof(enablement));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _eventBus = eventBus;
    }

    /// <summary>Gets the enablement registry so callers can check status without a separate wire-up.</summary>
    public IAppEnablementRegistry Enablement => _enablement;

    /// <summary>Attempts to resolve the active descriptor and context for a running process.</summary>
    public bool TryGetRunningInstance(ProcessId pid, [NotNullWhen(true)] out AppDescriptor? descriptor, [NotNullWhen(true)] out IAppExecutionContext? context)
    {
        lock (_sync)
        {
            if (_running.TryGetValue(pid, out RunningInstance? instance))
            {
                descriptor = instance.Descriptor;
                context = instance.Context;
                return true;
            }
        }

        descriptor = null;
        context = null;
        return false;
    }

    /// <summary>
    /// Launches one catalog app. For a singleton <see cref="AppKind.Window"/> app that already has
    /// an active process, focuses the existing instance instead of starting a new one
    /// (`P1-APP-006`). <see cref="AppKind.Terminal"/> apps run to completion synchronously, since
    /// they represent one command execution rather than a persistent instance.
    /// </summary>
    /// <param name="request">Target app, acting principal, and launch arguments.</param>
    /// <param name="fullScreen">Optional alternate-screen session available only to Terminal apps.</param>
    /// <param name="cancellationToken">Cancels this launch or synchronous command execution.</param>
    public async Task<AppLaunchResult> LaunchAsync(
        AppLaunchRequest request,
        IFullScreenTerminalSession? fullScreen = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_descriptors.TryGetValue(request.AppId, out AppDescriptor? descriptor))
        {
            return new AppLaunchResult(AppLaunchStatus.NotFound, ErrorCode: "lifecycle.app.not-found");
        }

        if (!_enablement.IsEnabled(request.AppId))
        {
            return new AppLaunchResult(AppLaunchStatus.Disabled, ErrorCode: "lifecycle.app.disabled");
        }

        if (descriptor.Manifest.Kind == AppKind.Window
            && descriptor.Manifest.SingleInstancePerUser
            && _processManager.TryGetSingleton(request.AppId, out ProcessRecord existing))
        {
            return new AppLaunchResult(AppLaunchStatus.FocusedExisting, existing);
        }

        HashSet<string> grantedCapabilities = ComputeGrantedCapabilities(descriptor.Manifest, request.Principal);
        ProcessRecord process = _processManager.Start(new ProcessStartRequest(
            request.ParentPid,
            request.AppId,
            AppInstanceId.FromGuid(Guid.NewGuid()),
            descriptor.Manifest.Kind,
            request.Principal.UserId,
            request.Principal.SessionId,
            ResourceProfile.None));

        IAppExecutionContext context = _contextFactory.Create(
            descriptor.Manifest, request.Principal, process, grantedCapabilities);

        try
        {
            return descriptor.Manifest.Kind switch
            {
                AppKind.Terminal => await RunTerminalAsync(
                    descriptor, process, context, request.Arguments, fullScreen, cancellationToken),
                AppKind.Service => StartService(descriptor, process, context),
                _ => StartWindow(process, context)
            };
        }
        catch (Exception ex)
        {
            ProcessRecord faulted = _processManager.Fault(process.Pid);
            return new AppLaunchResult(
                AppLaunchStatus.EntryPointFault, faulted, context,
                ErrorCode: "lifecycle.entry-point.fault", ErrorMessage: ex.Message);
        }
    }

    /// <summary>
    /// Starts every enabled <see cref="AppKind.Service"/> app in catalog activation order, per
    /// `P1-APP-005`. Intended to run once per new session.
    /// </summary>
    /// <param name="principal">Authenticated principal the services run as.</param>
    public async Task<IReadOnlyList<AppLaunchResult>> StartAllServicesAsync(AuthenticatedPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        List<AppLaunchResult> results = [];
        foreach (string appId in _catalog.ActivationOrder)
        {
            if (!_descriptors.TryGetValue(appId, out AppDescriptor? descriptor) || descriptor.Manifest.Kind != AppKind.Service)
            {
                continue;
            }

            if (!_enablement.IsEnabled(appId))
            {
                continue;
            }

            results.Add(await LaunchAsync(new AppLaunchRequest(appId, principal, [])));
        }

        return results;
    }

    /// <summary>
    /// Stops every currently running app instance in catalog deactivation order, per `P1-APP-005`.
    /// Each <see cref="AppKind.Service"/> instance receives a bounded graceful stop callback
    /// before its process is force-stopped; other kinds are force-stopped directly.
    /// </summary>
    /// <param name="reason">Reason recorded against every stopped process.</param>
    public async Task StopAllAsync(ProcessExitReason reason)
    {
        List<KeyValuePair<ProcessId, RunningInstance>> snapshot;
        lock (_sync)
        {
            snapshot = [.. _running];
        }

        Dictionary<string, List<KeyValuePair<ProcessId, RunningInstance>>> byAppId = new(StringComparer.Ordinal);
        foreach (KeyValuePair<ProcessId, RunningInstance> entry in snapshot)
        {
            if (!byAppId.TryGetValue(entry.Value.Descriptor.Manifest.Id, out List<KeyValuePair<ProcessId, RunningInstance>>? list))
            {
                byAppId[entry.Value.Descriptor.Manifest.Id] = list = [];
            }

            list.Add(entry);
        }

        foreach (string appId in _catalog.DeactivationOrder)
        {
            if (!byAppId.TryGetValue(appId, out List<KeyValuePair<ProcessId, RunningInstance>>? instances))
            {
                continue;
            }

            foreach ((ProcessId pid, RunningInstance instance) in instances)
            {
                await StopInstanceAsync(pid, instance, reason);
            }
        }
    }

    /// <summary>Stops one running app instance through the same bounded lifecycle path.</summary>
    /// <param name="pid">Process identity of the instance to stop.</param>
    /// <param name="reason">Terminal reason recorded for the process.</param>
    /// <returns><see langword="true"/> when an active instance was found and stopped.</returns>
    public async Task<bool> StopAsync(ProcessId pid, ProcessExitReason reason = ProcessExitReason.CloseRequested)
    {
        RunningInstance? instance;
        lock (_sync)
        {
            _running.TryGetValue(pid, out instance);
        }

        if (instance is null)
        {
            return false;
        }

        await StopInstanceAsync(pid, instance, reason);
        return true;
    }

    /// <summary>
    /// Disables one app and every enabled app that transitively depends on it, stopping their
    /// active processes in dependents-first order and invalidating any stale file-association
    /// default that named the disabled app (`P1-APP-010`, `P1-APP-011`).
    /// </summary>
    /// <param name="appId">App to disable.</param>
    /// <param name="reason">Reason recorded against stopped processes.</param>
    public async Task<AppDisableResult> DisableAsync(string appId, ProcessExitReason reason = ProcessExitReason.Disabled)
    {
        if (!_catalog.Manifests.ContainsKey(appId))
        {
            return new AppDisableResult(false, [], [$"lifecycle.app.not-found:{appId}"]);
        }

        if (!_enablement.IsEnabled(appId))
        {
            return new AppDisableResult(true, [], []);
        }

        IReadOnlyList<string> closure = _enablement.ComputeDisableClosure(appId);
        foreach (string toDisable in closure)
        {
            List<KeyValuePair<ProcessId, RunningInstance>> instances;
            lock (_sync)
            {
                instances = _running.Where(e => e.Value.Descriptor.Manifest.Id == toDisable).ToList();
            }

            foreach ((ProcessId pid, RunningInstance instance) in instances)
            {
                await StopInstanceAsync(pid, instance, reason);
            }
        }

        _enablement.MarkDisabled(closure);
        foreach (string disabledAppId in closure)
        {
            _eventBus?.Publish(new AppDisabledEvent(disabledAppId));
        }

        await InvalidateAssociationDefaultsAsync(appId);
        return new AppDisableResult(true, closure, []);
    }

    /// <summary>
    /// Re-enables one app, failing with an explanatory error when a non-optional dependency is
    /// missing or still disabled (`P1-APP-011`).
    /// </summary>
    /// <param name="appId">App to enable.</param>
    public AppEnableResult Enable(string appId)
    {
        if (!_catalog.Manifests.ContainsKey(appId))
        {
            return new AppEnableResult(false, [$"lifecycle.app.not-found:{appId}"]);
        }

        if (_enablement.IsEnabled(appId))
        {
            return new AppEnableResult(true, []);
        }

        IReadOnlyList<string> blocking = _enablement.ComputeBlockingDependencies(appId);
        if (blocking.Count > 0)
        {
            return new AppEnableResult(
                false,
                [.. blocking.Select(dependency => $"lifecycle.enable.dependency-disabled:{dependency}")]);
        }

        _enablement.MarkEnabled(appId);
        return new AppEnableResult(true, []);
    }

    private HashSet<string> ComputeGrantedCapabilities(AppManifest manifest, AuthenticatedPrincipal principal)
    {
        string userId = principal.UserId.ToString();
        HashSet<string> granted = new(StringComparer.Ordinal);
        foreach (string capability in manifest.Capabilities)
        {
            CapabilityPolicyEvaluation evaluation = _grants.Evaluate(
                manifest.Id, userId, capability, principal.Authority, AppAuthority.User);
            if (evaluation.Granted)
            {
                granted.Add(capability);
            }
        }

        return granted;
    }

    [UnconditionalSuppressMessage(
        "Trimming", "IL2072",
        Justification = "descriptor.EntryPointType was verified by AppEntryPointDiscovery against an " +
            "explicit host assembly allowlist, never scanned; see P1-GATE-004 for the tracked follow-up " +
            "to add matching trim root descriptors once a host publish step exists.")]
    private async Task<AppLaunchResult> RunTerminalAsync(
        AppDescriptor descriptor,
        ProcessRecord process,
        IAppExecutionContext context,
        IReadOnlyList<string> arguments,
        IFullScreenTerminalSession? fullScreen,
        CancellationToken cancellationToken)
    {
        _processManager.MarkRunning(process.Pid);

        TerminalAppBase app = (TerminalAppBase)Activator.CreateInstance(descriptor.EntryPointType, descriptor.Manifest)!;
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new(string.Empty);
        using CancellationTokenSource executionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        TerminalExecutionContext terminalContext = new(
            context,
            arguments,
            stdin,
            stdout,
            stderr,
            "/",
            new Dictionary<string, string>(StringComparer.Ordinal),
            fullScreen);

        int exitCode;
        try
        {
            exitCode = await app.ExecuteAsync(terminalContext, executionCancellation.Token);
        }
        catch (OperationCanceledException) when (executionCancellation.IsCancellationRequested)
        {
            exitCode = 130;
        }
        ProcessRecord stopped = _processManager.Complete(process.Pid, exitCode);
        return new AppLaunchResult(
            AppLaunchStatus.Launched, stopped, context, exitCode, stdout.ToString(), stderr.ToString());
    }

    [UnconditionalSuppressMessage(
        "Trimming", "IL2072",
        Justification = "descriptor.EntryPointType was verified by AppEntryPointDiscovery against an " +
            "explicit host assembly allowlist, never scanned; see P1-GATE-004 for the tracked follow-up " +
            "to add matching trim root descriptors once a host publish step exists.")]
    private AppLaunchResult StartService(AppDescriptor descriptor, ProcessRecord process, IAppExecutionContext context)
    {
        ServiceAppBase service = (ServiceAppBase)Activator.CreateInstance(descriptor.EntryPointType, descriptor.Manifest)!;
        _processManager.MarkRunning(process.Pid);
        Task runTask = RunServiceAsync(service, process.Pid, context);

        lock (_sync)
        {
            _running[process.Pid] = new RunningInstance(descriptor, context, service, runTask);
        }

        return new AppLaunchResult(AppLaunchStatus.Launched, process, context);
    }

    private async Task RunServiceAsync(ServiceAppBase service, ProcessId pid, IAppExecutionContext context)
    {
        try
        {
            await service.RunAsync(context, context.CancellationToken);
            if (_processManager.TryGetActive(pid, out _))
            {
                _processManager.Complete(pid, 0);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected once the process token is cancelled by StopInstanceAsync/logout/shutdown.
        }
        catch
        {
            if (_processManager.TryGetActive(pid, out _))
            {
                _processManager.Fault(pid);
            }
        }
    }

    private AppLaunchResult StartWindow(ProcessRecord process, IAppExecutionContext context)
    {
        // Rendering a Window app's component belongs to the future Platform Blazor window
        // runtime (Phase 2A); Section 8 only establishes the process/context the runtime will
        // later attach to. Marking the process Running now reflects that it is alive, independent
        // of whether anything is currently rendering it.
        ProcessRecord running = _processManager.MarkRunning(process.Pid);
        lock (_sync)
        {
            _running[process.Pid] = new RunningInstance(
                _descriptors[process.AppId], context, ServiceInstance: null, RunTask: null);
        }

        return new AppLaunchResult(AppLaunchStatus.Launched, running, context);
    }

    private async Task StopInstanceAsync(ProcessId pid, RunningInstance instance, ProcessExitReason reason)
    {
        lock (_sync)
        {
            _running.Remove(pid);
        }

        if (instance.ServiceInstance is ServiceAppBase service && _processManager.TryGetActive(pid, out _))
        {
            using CancellationTokenSource stopBudget = new(TimeSpan.FromSeconds(5));
            try
            {
                await service.StopAsync(ToServiceStopReason(reason), stopBudget.Token);
            }
            catch
            {
                // Best-effort cleanup only; the process is force-stopped below regardless.
            }
        }

        if (_processManager.TryGetActive(pid, out _))
        {
            _processManager.Kill(pid, reason);
        }

        if (instance.RunTask is not null)
        {
            try
            {
                await instance.RunTask;
            }
            catch
            {
                // Faults from the run task are already reflected in the process's Faulted state.
            }
        }
    }

    private static ServiceStopReason ToServiceStopReason(ProcessExitReason reason) => reason switch
    {
        ProcessExitReason.Disabled => ServiceStopReason.Disabled,
        ProcessExitReason.Logout => ServiceStopReason.Logout,
        ProcessExitReason.Shutdown => ServiceStopReason.Shutdown,
        ProcessExitReason.Fault => ServiceStopReason.Faulted,
        _ => ServiceStopReason.Shutdown
    };

    private async Task InvalidateAssociationDefaultsAsync(string disabledAppId)
    {
        AppOperationContext systemContext = new()
        {
            AppId = "org.hackeros.settings",
            UserId = "system",
            UserAuthority = AppAuthority.System,
            GrantedCapabilities = new HashSet<string>(StringComparer.Ordinal)
            {
                AppCapabilities.FileAssociationsRead,
                AppCapabilities.FileAssociationsWrite
            },
            IsSystemOperation = true
        };

        SettingsReadResult read = await _settings.ReadAsync(FileAssociationSettingsDocuments.Path, systemContext);
        if (read.Status != SettingsReadStatus.Success || read.Document is null)
        {
            return;
        }

        FileAssociationIndex index = FileAssociationIndex.Build(read.Document.Content);
        if (!index.Entries.Any(entry => entry.AppId == disabledAppId))
        {
            return;
        }

        string updated = FileAssociationIndex.Serialize(
            index.Entries.Where(entry => entry.AppId != disabledAppId).ToArray());
        await _settings.WriteAsync(
            new SettingsWriteRequest(FileAssociationSettingsDocuments.Path, updated, read.Document.Revision),
            systemContext);
    }

    /// <summary>Tracks one currently running app instance so it can be stopped later.</summary>
    private sealed record RunningInstance(
        AppDescriptor Descriptor,
        IAppExecutionContext Context,
        ServiceAppBase? ServiceInstance,
        Task? RunTask);
}
