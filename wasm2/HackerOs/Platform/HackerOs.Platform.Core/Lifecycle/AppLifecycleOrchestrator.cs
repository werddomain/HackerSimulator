using System.Diagnostics.CodeAnalysis;
using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Platform.Core.Execution;
using HackerOs.Platform.Core.Intents;
using HackerOs.Platform.Core.Policy;
using HackerOs.Platform.Core.Processes;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using Microsoft.Extensions.DependencyInjection;

using HackerOs.Platform.Core.Settings;
using HackerOs.Platform.Core.Shell;

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
    private readonly IEventBus? _eventBus;
    private readonly IAppDescriptorLoader? _descriptorLoader;
    private readonly IServiceProvider? _services;
    private readonly IFileSystemService? _fileSystem;
    private readonly IElevationTicketStore? _elevationTickets;
    private readonly IDangerousCapabilityGate? _dangerousCapabilityGate;
    private readonly LoginEnvironmentProvider? _loginEnvironment;
    private readonly object _sync = new();
    private readonly Dictionary<ProcessId, RunningInstance> _running = [];

    /// <summary>Initializes the orchestrator over a fully resolved discovery result.</summary>
    /// <param name="catalog">Validated, dependency-ordered app catalog.</param>
    /// <param name="descriptors">Successfully resolved entry-point descriptors for the catalog.</param>
    /// <param name="enablement">Runtime enable/disable state for catalog apps.</param>
    /// <param name="processManager">Process lifecycle manager backing every launch.</param>
    /// <param name="grants">Capability grant repository used to compute each launch's granted set.</param>
    /// <param name="contextFactory">Trusted execution-context factory.</param>
    /// <param name="eventBus">Optional typed event bus for cross-subsystem lifecycle cleanup.</param>
    /// <param name="descriptorLoader">Optional lazy descriptor loader for assemblies not eagerly discovered.</param>
    /// <param name="services">
    /// Optional DI container used to construct terminal/service app instances (ADR 0034). When
    /// supplied, <see cref="RunTerminalAsync"/>/<see cref="StartService"/> use
    /// <see cref="ActivatorUtilities.CreateInstance(IServiceProvider, Type, object[])"/> so a
    /// command's constructor can request additional injected services beyond the manifest (e.g.
    /// <c>PingCommand</c>'s <c>IServerConnectionService</c>/<c>IProxyClient</c>). When omitted, only
    /// manifest-only constructors work — the previous, narrower behavior tests still rely on.
    /// </param>
    /// <param name="fileSystem">
    /// Optional trusted filesystem access used to resolve each <see cref="AppKind.Service"/> app's
    /// live <see cref="ServiceStartMode"/> from its per-user config file (see
    /// <see cref="ServiceStartModeStore"/>) before <see cref="StartAllServicesAsync"/> auto-starts
    /// it. When omitted (e.g. in tests that don't exercise start-mode gating), every enabled
    /// service is treated as <see cref="ServiceStartMode.Automatic"/> — the previous, narrower
    /// behavior tests still rely on.
    /// </param>
    /// <param name="elevationTickets">
    /// Optional single-use elevation ticket store (`ADR 0042`). When a <see cref="AppLaunchRequest.ElevationToken"/>
    /// is supplied, <see cref="LaunchAsync"/> consumes it here before starting the app. When omitted
    /// (e.g. in tests that don't exercise elevation), a supplied token is always denied — the
    /// previous, narrower behavior tests still rely on.
    /// </param>
    /// <param name="dangerousCapabilityGate">
    /// Optional launch gate of ADR 0049 section 5. When supplied, every launch of an app declaring
    /// a dangerous capability goes through it before the process is created (approval overlay,
    /// <c>auth.log</c>, grants aligned with <c>app-permissions.config</c>), and policy <c>block</c>
    /// refuses the one-shot approval of ADR 0042 for those capabilities. When omitted, a non-system
    /// app simply never holds a dangerous capability (the login seeder withholds them).
    /// </param>
    /// <param name="loginEnvironment">
    /// Optional source of the login environment (<c>/etc/environment</c>, ADR 0050 section 5) given
    /// to processes with no parent. When omitted, they get the fixed login variables only.
    /// </param>
    public AppLifecycleOrchestrator(
        AppCatalog catalog,
        IReadOnlyDictionary<string, AppDescriptor> descriptors,
        AppEnablementRegistry enablement,
        IProcessManager processManager,
        ICapabilityGrantRepository grants,
        AppExecutionContextFactory contextFactory,
        IEventBus? eventBus = null,
        IAppDescriptorLoader? descriptorLoader = null,
        IServiceProvider? services = null,
        IFileSystemService? fileSystem = null,
        IElevationTicketStore? elevationTickets = null,
        IDangerousCapabilityGate? dangerousCapabilityGate = null,
        LoginEnvironmentProvider? loginEnvironment = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _descriptors = descriptors ?? throw new ArgumentNullException(nameof(descriptors));
        _enablement = enablement ?? throw new ArgumentNullException(nameof(enablement));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _eventBus = eventBus;
        _descriptorLoader = descriptorLoader;
        _services = services;
        _fileSystem = fileSystem;
        _elevationTickets = elevationTickets;
        _dangerousCapabilityGate = dangerousCapabilityGate;
        _loginEnvironment = loginEnvironment;
        _eventBus?.Subscribe<ProcessStateChangedEvent>(OnProcessStateChanged);
    }

    /// <summary>
    /// Constructs a terminal/service app instance, resolving any constructor parameters beyond the
    /// manifest from DI when a service provider was supplied (ADR 0034) — falls back to the
    /// manifest-only shape when it wasn't, matching the pre-ADR-0034 behavior for tests that don't
    /// need extra dependencies.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming", "IL2072",
        Justification = "descriptor.EntryPointType was verified by AppEntryPointDiscovery against an " +
            "explicit host assembly allowlist, never scanned; see P1-GATE-004 for the tracked follow-up " +
            "to add matching trim root descriptors once a host publish step exists.")]
    private object CreateAppInstance(AppDescriptor descriptor) =>
        _services is not null
            ? ActivatorUtilities.CreateInstance(_services, descriptor.EntryPointType, descriptor.Manifest)
            : Activator.CreateInstance(descriptor.EntryPointType, descriptor.Manifest)!;

    private void OnProcessStateChanged(ProcessStateChangedEvent e)
    {
        if (e.NewState is ProcessState.Stopped or ProcessState.Faulted)
        {
            lock (_sync)
            {
                _running.Remove(e.Pid);
            }
        }
    }

    /// <summary>Gets the enablement registry so callers can check status without a separate wire-up.</summary>
    public IAppEnablementRegistry Enablement => _enablement;

    /// <summary>
    /// Attempts to resolve a catalog app's discovered descriptor, exposing its resolved entry-point
    /// assembly for same-assembly authorization checks (e.g. a companion Window app controlling its
    /// own bundled Service app's start/stop/start-mode via the app-facing service-control gateway).
    /// </summary>
    public bool TryGetDescriptor(string appId, [NotNullWhen(true)] out AppDescriptor? descriptor) =>
        _descriptors.TryGetValue(appId, out descriptor);

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
    /// Attempts to resolve the active descriptor and context for a running instance, for callers
    /// (e.g. the window runtime) that only carry the instance ID rather than the process ID.
    /// </summary>
    public bool TryGetRunningInstance(AppInstanceId instanceId, [NotNullWhen(true)] out AppDescriptor? descriptor, [NotNullWhen(true)] out IAppExecutionContext? context)
    {
        lock (_sync)
        {
            foreach (RunningInstance instance in _running.Values)
            {
                if (instance.Context.InstanceId == instanceId.Value)
                {
                    descriptor = instance.Descriptor;
                    context = instance.Context;
                    return true;
                }
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

        if (!_descriptors.TryGetValue(request.AppId, out AppDescriptor? descriptor)
            && _descriptorLoader is not null)
        {
            AppDescriptorLoadResult loaded = await _descriptorLoader
                .EnsureAvailableAsync(request.AppId, cancellationToken)
                .ConfigureAwait(false);
            if (loaded.Status != AppDescriptorLoadStatus.Available
                || !_descriptors.TryGetValue(request.AppId, out descriptor))
            {
                return new AppLaunchResult(
                    AppLaunchStatus.NotFound,
                    ErrorCode: loaded.Status == AppDescriptorLoadStatus.Unavailable
                        ? "lifecycle.app.asset-unavailable"
                        : "lifecycle.app.not-found",
                    ErrorMessage: loaded.Detail);
            }
        }

        if (descriptor is null)
        {
            return new AppLaunchResult(AppLaunchStatus.NotFound, ErrorCode: "lifecycle.app.not-found");
        }

        if (!_enablement.IsEnabled(request.AppId))
        {
            return new AppLaunchResult(AppLaunchStatus.Disabled, ErrorCode: "lifecycle.app.disabled");
        }

        bool isImplicitlySingleton =
            (descriptor.Manifest.Kind == AppKind.Window && descriptor.Manifest.SingleInstancePerUser)
            || descriptor.Manifest.Kind == AppKind.Service;
        if (isImplicitlySingleton && _processManager.TryGetSingleton(request.AppId, out ProcessRecord existing))
        {
            return new AppLaunchResult(AppLaunchStatus.FocusedExisting, existing);
        }

        bool isRootElevated = false;
        ElevationTicket? capabilityTicket = null;
        if (request.ElevationToken is Guid elevationToken && request.ElevationScope is ElevationScope elevationScope)
        {
            if (_elevationTickets is null
                || !_elevationTickets.TryConsume(elevationToken, elevationScope, out ElevationTicket? ticket)
                || ticket is null)
            {
                return new AppLaunchResult(AppLaunchStatus.ElevationDenied, ErrorCode: "lifecycle.elevation.denied");
            }

            if (elevationScope == ElevationScope.SudoRoot)
            {
                isRootElevated = true;
            }
            else if (ticket.Capability is not null)
            {
                capabilityTicket = ticket;
            }
        }

        // ADR 0049 section 5: decide pending dangerous capabilities before the process exists, so
        // the instance's capabilities are frozen at launch with the administrator's answer.
        DangerousCapabilityGateResult? dangerous = null;
        if (_dangerousCapabilityGate is not null
            && DangerousCapabilityEvaluator.DeclaresDangerousCapabilities(descriptor.Manifest))
        {
            dangerous = await _dangerousCapabilityGate
                .EnsureDecidedAsync(descriptor.Manifest, request.Principal, request.ApprovalPrompt, request.Interactive, cancellationToken)
                .ConfigureAwait(false);
        }

        OneShotElevationGrantSet? oneShotElevation = null;
        if (dangerous is { BlocksOneShotApproval: true })
        {
            // ADR 0049 section 3: under policy block the one-shot approval of ADR 0042 is refused
            // for every dangerous capability of a non-system app (sudo is not concerned).
            if (capabilityTicket?.Capability is { } ticketCapability && AppCapabilities.IsDangerous(ticketCapability))
            {
                return new AppLaunchResult(AppLaunchStatus.ElevationDenied, ErrorCode: "lifecycle.elevation.blocked-by-policy");
            }

            oneShotElevation = new OneShotElevationGrantSet();
            foreach (string capability in AppCapabilities.All.Where(AppCapabilities.IsDangerous))
            {
                oneShotElevation.Block(capability);
            }
        }

        if (capabilityTicket is not null)
        {
            oneShotElevation ??= new OneShotElevationGrantSet();
            oneShotElevation.Grant(capabilityTicket.Capability!, capabilityTicket.Path);
        }

        HashSet<string> grantedCapabilities = ComputeGrantedCapabilities(descriptor.Manifest, request.Principal);
        (VirtualPath? workingDirectory, IReadOnlyDictionary<string, string>? environment) =
            await ResolveStartEnvironmentAsync(request, cancellationToken).ConfigureAwait(false);
        ProcessRecord process = _processManager.Start(new ProcessStartRequest(
            request.ParentPid,
            request.AppId,
            AppInstanceId.FromGuid(Guid.NewGuid()),
            descriptor.Manifest.Kind,
            request.Principal.UserId,
            request.Principal.SessionId,
            ResourceProfile.None,
            // argv[0] is the name the command was invoked as (an alias such as `unalias` included),
            // otherwise the manifest's command name (ADR 0050).
            [request.CommandName ?? descriptor.Manifest.Terminal?.Name ?? request.AppId, .. request.Arguments],
            workingDirectory,
            environment));

        IAppExecutionContext context = _contextFactory.Create(
            descriptor.Manifest, request.Principal, process, grantedCapabilities, process.AppInstanceId.Value,
            isRootElevated, oneShotElevation);

        try
        {
            return descriptor.Manifest.Kind switch
            {
                AppKind.Terminal => await RunTerminalAsync(
                    descriptor, process, context, request, fullScreen,
                    // ADR 0051 section 3: the shell reaches only a command granted shell.control.
                    grantedCapabilities.Contains(AppCapabilities.ShellControl) ? request.Shell : null,
                    cancellationToken),
                AppKind.Service => StartService(descriptor, process, context),
                _ => StartWindow(descriptor, process, context)
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

            ServiceStartMode effectiveMode = _fileSystem is null
                ? ServiceStartMode.Automatic
                : await ServiceStartModeStore.ReadAsync(
                    _fileSystem,
                    principal.LoginName.Value,
                    appId,
                    descriptor.Manifest.AutoStart ? ServiceStartMode.Automatic : ServiceStartMode.Manual,
                    CancellationToken.None);

            if (effectiveMode != ServiceStartMode.Automatic)
            {
                continue;
            }

            // Nobody is watching an auto-start: pending dangerous capabilities are deferred (ADR 0049 section 5.4).
            results.Add(await LaunchAsync(new AppLaunchRequest(appId, principal, [], Interactive: false)));
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
    /// Stops one running app instance identified by its instance rather than process identity, for
    /// callers (e.g. the window runtime) that only carry the instance ID. An instance ID is unique
    /// to one process for the whole life of that process, so this is equivalent to
    /// <see cref="StopAsync(ProcessId, ProcessExitReason)"/> once the owning process is resolved.
    /// </summary>
    /// <param name="instanceId">Instance identity of the process to stop.</param>
    /// <param name="reason">Terminal reason recorded for the process.</param>
    /// <returns><see langword="true"/> when an active instance was found and stopped.</returns>
    public async Task<bool> StopAsync(AppInstanceId instanceId, ProcessExitReason reason = ProcessExitReason.CloseRequested)
    {
        ProcessId pid;
        RunningInstance? instance;
        lock (_sync)
        {
            KeyValuePair<ProcessId, RunningInstance> found = _running
                .FirstOrDefault(entry => entry.Value.Context.InstanceId == instanceId.Value);
            instance = found.Value;
            pid = found.Key;
        }

        if (instance is null)
        {
            return false;
        }

        await StopInstanceAsync(pid, instance, reason);
        return true;
    }

    /// <summary>
    /// Stops the running instance of one <see cref="AppKind.Service"/> app, identified by app ID
    /// rather than process ID — services are implicitly singleton per user (see
    /// <see cref="LaunchAsync"/>), so at most one running instance can exist.
    /// </summary>
    /// <param name="appId">Service app to stop.</param>
    /// <param name="reason">Terminal reason recorded for the process.</param>
    /// <returns><see langword="true"/> when a running instance was found and stopped.</returns>
    public async Task<bool> StopServiceAsync(string appId, ProcessExitReason reason = ProcessExitReason.CloseRequested)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        return _processManager.TryGetSingleton(appId, out ProcessRecord existing)
            && await StopAsync(existing.Pid, reason);
    }

    /// <summary>
    /// Disables one app and every enabled app that transitively depends on it, stopping their
    /// active processes in dependents-first order (`P1-APP-011`). Internal since ADR 0048: only
    /// <c>AppInstallationService</c> calls it, to enforce an uninstall written in <c>apps.config</c>.
    /// File-association preferences are deliberately left untouched, so a reinstall finds them again;
    /// <see cref="FileAssociationResolver"/> already ignores a disabled app.
    /// </summary>
    /// <param name="appId">App to disable.</param>
    /// <param name="reason">Reason recorded against stopped processes.</param>
    internal async Task<AppDisableResult> DisableAsync(string appId, ProcessExitReason reason = ProcessExitReason.Disabled)
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

        return new AppDisableResult(true, closure, []);
    }

    /// <summary>
    /// Re-enables one app, failing with an explanatory error when a non-optional dependency is
    /// missing or still disabled (`P1-APP-011`). Internal since ADR 0048, like <see cref="DisableAsync"/>.
    /// </summary>
    /// <param name="appId">App to enable.</param>
    internal Task<AppEnableResult> EnableAsync(string appId)
    {
        if (!_catalog.Manifests.ContainsKey(appId))
        {
            return Task.FromResult(new AppEnableResult(false, [$"lifecycle.app.not-found:{appId}"]));
        }

        if (_enablement.IsEnabled(appId))
        {
            return Task.FromResult(new AppEnableResult(true, []));
        }

        IReadOnlyList<string> blocking = _enablement.ComputeBlockingDependencies(appId);
        if (blocking.Count > 0)
        {
            return Task.FromResult(new AppEnableResult(
                false,
                [.. blocking.Select(dependency => $"lifecycle.enable.dependency-disabled:{dependency}")]));
        }

        _enablement.MarkEnabled(appId);
        return Task.FromResult(new AppEnableResult(true, []));
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

    private async Task<AppLaunchResult> RunTerminalAsync(
        AppDescriptor descriptor,
        ProcessRecord process,
        IAppExecutionContext context,
        AppLaunchRequest request,
        IFullScreenTerminalSession? fullScreen,
        IShellSession? shell,
        CancellationToken cancellationToken)
    {
        _processManager.MarkRunning(process.Pid);

        // ADR 0050 section 6: the context keeps the process's state at command start, for
        // compatibility; the live values are read through HackEnvironment.
        IAppEnvironmentGateway processEnvironment = context.Environment;

        TerminalAppBase app = (TerminalAppBase)CreateAppInstance(descriptor);
        // ADR 0050 section 4: streams supplied by the shell (terminal, pipe, redirected file) are
        // written as the command runs; without them the output is buffered for the caller.
        using StringWriter? bufferedOutput = request.StandardOutput is null ? new StringWriter() : null;
        using StringWriter? bufferedError = request.StandardError is null ? new StringWriter() : null;
        using StringReader emptyInput = new(string.Empty);
        using CancellationTokenSource executionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        TerminalExecutionContext terminalContext = new(
            context,
            request.Arguments,
            request.StandardInput ?? emptyInput,
            request.StandardOutput ?? bufferedOutput!,
            request.StandardError ?? bufferedError!,
            processEnvironment.CurrentDirectory,
            processEnvironment.GetVariables(),
            fullScreen,
            shell);

        List<FileSystemError> permissionDenials = [];
        void OnPermissionDenied(object? sender, AppPermissionErrorEventArgs e) => permissionDenials.Add(e.Error);
        context.PermissionErrors.PermissionDenied += OnPermissionDenied;

        int exitCode;
        try
        {
            // ADR 0043/0050: the command runs as this app, in its process's working directory (the
            // shell's, copied at creation), so the Hack* file API resolves relative paths exactly
            // like the shell does, and follows the command's own HackEnvironment changes.
            using IDisposable ambient = HackExecutionContext.Enter(context);
            exitCode = await app.ExecuteAsync(terminalContext, executionCancellation.Token);
        }
        catch (OperationCanceledException) when (executionCancellation.IsCancellationRequested)
        {
            exitCode = 130;
        }
        catch (ShellBrokenPipeException)
        {
            // ADR 0050 section 4: the reader of its output exited and the command let the "Broken
            // pipe" error escape, as a Linux process killed by SIGPIPE (128 + 13).
            exitCode = 141;
        }
        finally
        {
            context.PermissionErrors.PermissionDenied -= OnPermissionDenied;
        }

        ProcessRecord stopped = CompleteUnlessEnded(process, exitCode);
        return new AppLaunchResult(
            AppLaunchStatus.Launched, stopped, context, exitCode,
            bufferedOutput?.ToString() ?? string.Empty, bufferedError?.ToString() ?? string.Empty,
            PermissionDenials: permissionDenials);
    }

    /// <summary>
    /// Records a command's exit, unless its process already ended: killed with <c>kill</c>, or with
    /// the shell that ran it when its terminal closed (ADR 0056, section 7). The record of that
    /// earlier end is kept, as the exit of a process killed by a signal is the signal's.
    /// </summary>
    private ProcessRecord CompleteUnlessEnded(ProcessRecord process, int exitCode)
    {
        try
        {
            return _processManager.Complete(process.Pid, exitCode);
        }
        catch (KeyNotFoundException)
        {
            return _processManager.GetHistory().LastOrDefault(record => record.Pid == process.Pid) ?? process;
        }
    }

    /// <summary>
    /// Computes what the new process starts with (ADR 0050, section 5). A process with no parent
    /// (desktop launch, service) gets the session's login environment and the home directory; a
    /// child inherits its parent's, which the process manager copies. In both cases an explicit
    /// directory also updates <c>PWD</c>, as the shell does when it starts a command after <c>cd</c>,
    /// then the request's own variables apply on top.
    /// </summary>
    private async ValueTask<(VirtualPath? WorkingDirectory, IReadOnlyDictionary<string, string>? Environment)> ResolveStartEnvironmentAsync(
        AppLaunchRequest request,
        CancellationToken cancellationToken)
    {
        VirtualPath? workingDirectory = request.WorkingDirectory;
        Dictionary<string, string> environment = new(StringComparer.Ordinal);
        if (request.ParentPid is null)
        {
            AuthenticatedPrincipal principal = request.Principal;
            IReadOnlyDictionary<string, string> login = _loginEnvironment is null
                ? LoginEnvironment.Build(principal.LoginName.Value, principal.HomePath, environmentFile: null)
                : await _loginEnvironment.GetAsync(principal, cancellationToken).ConfigureAwait(false);
            foreach ((string name, string value) in login)
            {
                environment[name] = value;
            }

            workingDirectory ??= VirtualPath.Parse(principal.HomePath);
            environment["PWD"] = workingDirectory.Value.Value;
        }
        else if (workingDirectory is { } explicitDirectory)
        {
            environment["PWD"] = explicitDirectory.Value;
        }

        if (request.Environment is { } overrides)
        {
            foreach ((string name, string value) in overrides)
            {
                environment[name] = value;
            }
        }

        return (workingDirectory, environment.Count == 0 ? null : environment);
    }

    private AppLaunchResult StartService(AppDescriptor descriptor, ProcessRecord process, IAppExecutionContext context)
    {
        ServiceAppBase service = (ServiceAppBase)CreateAppInstance(descriptor);
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

    private AppLaunchResult StartWindow(AppDescriptor descriptor, ProcessRecord process, IAppExecutionContext context)
    {
        // Register the running instance before marking the process Running: MarkRunning
        // publishes ProcessStateChangedEvent synchronously, and subscribers (e.g. the Blazor
        // window runtime presenting a new window) call TryGetRunningInstance in reaction to
        // that same event, so the entry must already be visible by the time it fires.
        lock (_sync)
        {
            _running[process.Pid] = new RunningInstance(
                descriptor, context, ServiceInstance: null, RunTask: null);
        }

        ProcessRecord running = _processManager.MarkRunning(process.Pid);

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

    /// <summary>Tracks one currently running app instance so it can be stopped later.</summary>
    private sealed record RunningInstance(
        AppDescriptor Descriptor,
        IAppExecutionContext Context,
        ServiceAppBase? ServiceInstance,
        Task? RunTask);
}
