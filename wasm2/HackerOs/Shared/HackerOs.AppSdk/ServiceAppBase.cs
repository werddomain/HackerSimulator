using HackerOs.App.Abstractions;

namespace HackerOs.AppSdk;

/// <summary>
/// Explains why the ecosystem requested a session service to stop.
/// </summary>
public enum ServiceStopReason
{
    /// <summary>The user or OS disabled the service.</summary>
    Disabled,

    /// <summary>The authenticated user logged out.</summary>
    Logout,

    /// <summary>The simulated operating system is shutting down.</summary>
    Shutdown,

    /// <summary>The service or one of its dependencies faulted.</summary>
    Faulted,

    /// <summary>
    /// Another process asked it to stop: <c>kill</c>, "End task", <c>systemctl stop</c> (ADR 0069).
    /// </summary>
    Terminated
}

/// <summary>
/// Base class for non-visual work that lasts only for the active OS session.
/// </summary>
public abstract class ServiceAppBase : AppBase
{
    private readonly Lock _stopGate = new();
    private IAppExecutionContext? _context;
    private Task? _stopping;

    /// <summary>Initializes a service application with its validated manifest.</summary>
    /// <param name="manifest">Service application manifest.</param>
    protected ServiceAppBase(AppManifest manifest)
        : base(manifest, AppKind.Service)
    {
    }

    /// <summary>
    /// Initializes a service-like app of another kind: a web site (<see cref="AppKind.Web"/>,
    /// <see cref="WebServerAppBase"/>) has the same lifecycle as a service (ADR 0073).
    /// </summary>
    /// <param name="manifest">Application manifest.</param>
    /// <param name="kind">The kind the manifest must declare.</param>
    private protected ServiceAppBase(AppManifest manifest, AppKind kind)
        : base(manifest, kind)
    {
    }

    /// <summary>
    /// Runs the service until it completes, faults, or receives session cancellation.
    /// </summary>
    /// <param name="context">Scoped application execution context.</param>
    /// <param name="sessionCancellationToken">Signals logout, disable, or OS shutdown.</param>
    /// <returns>A task representing the active service session.</returns>
    /// <remarks>
    /// The service runs inside a <see cref="HackExecutionContext"/> scope for <paramref name="context"/>
    /// (working directory: the user's home), so the <c>Hack*</c> file API works without passing the
    /// context around (ADR 0043).
    /// </remarks>
    public Task RunAsync(IAppExecutionContext context, CancellationToken sessionCancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        lock (_stopGate)
        {
            // A new run gets its own stop: OnStoppingAsync runs once per run, not once per instance.
            _stopping = null;
        }

        // ADR 0069: the OS's stop request (kill, End task, logout...) runs OnStoppingAsync right
        // away, before the token is cancelled, and the OS waits for it within the grace period.
        context.StopRequested += OnStopRequested;
        return RunInScopeAsync(context, sessionCancellationToken);
    }

    /// <summary>
    /// Gives the service a bounded opportunity to release in-memory resources.
    /// </summary>
    /// <param name="reason">Reason the ecosystem requested the stop.</param>
    /// <param name="cancellationToken">Enforces the ecosystem shutdown deadline.</param>
    /// <returns>A task representing bounded cleanup.</returns>
    /// <remarks>Runs inside the same ambient context scope as <see cref="RunAsync"/> when the service was started.</remarks>
    /// <remarks>
    /// Runs <see cref="OnStoppingAsync"/> once per run: when the OS already announced the stop
    /// (<see cref="IAppExecutionContext.StopRequested"/>), this call waits for that same cleanup.
    /// </remarks>
    public async ValueTask StopAsync(ServiceStopReason reason, CancellationToken cancellationToken)
    {
        await StopOnceAsync(reason, cancellationToken);
    }

    /// <summary>Maps the OS's stop reason to the one a service sees.</summary>
    /// <param name="reason">Reason carried by the stop request.</param>
    public static ServiceStopReason ToServiceStopReason(HackerOs.Simulation.Abstractions.Processes.ProcessExitReason reason) => reason switch
    {
        HackerOs.Simulation.Abstractions.Processes.ProcessExitReason.Disabled => ServiceStopReason.Disabled,
        HackerOs.Simulation.Abstractions.Processes.ProcessExitReason.Logout => ServiceStopReason.Logout,
        HackerOs.Simulation.Abstractions.Processes.ProcessExitReason.Shutdown => ServiceStopReason.Shutdown,
        HackerOs.Simulation.Abstractions.Processes.ProcessExitReason.Fault => ServiceStopReason.Faulted,
        _ => ServiceStopReason.Terminated
    };

    private void OnStopRequested(object? sender, Diagnostics.HackStopRequestedEventArgs e)
    {
        // The grace period is simulated time; the cleanup budget follows it in real time.
        CancellationTokenSource budget = new(e.GracePeriod > TimeSpan.Zero ? e.GracePeriod : TimeSpan.FromSeconds(1));
        Task cleanup = StopOnceAsync(ToServiceStopReason(e.Reason), budget.Token);
        _ = cleanup.ContinueWith(_ => budget.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        e.Defer(cleanup);
    }

    private Task StopOnceAsync(ServiceStopReason reason, CancellationToken cancellationToken)
    {
        lock (_stopGate)
        {
            return _stopping ??= RunStoppingAsync(reason, cancellationToken);
        }
    }

    private async Task RunStoppingAsync(ServiceStopReason reason, CancellationToken cancellationToken)
    {
        using IDisposable? scope = _context is null ? null : HackExecutionContext.Enter(_context);
        await OnStoppingAsync(reason, cancellationToken);
    }

    private async Task RunInScopeAsync(IAppExecutionContext context, CancellationToken sessionCancellationToken)
    {
        using IDisposable scope = HackExecutionContext.Enter(context);
        try
        {
            await RunCoreAsync(context, sessionCancellationToken);
        }
        finally
        {
            context.StopRequested -= OnStopRequested;
        }
    }

    /// <summary>Implements the active service loop.</summary>
    /// <param name="context">Scoped application execution context.</param>
    /// <param name="sessionCancellationToken">Signals the end of the OS session.</param>
    /// <returns>A task representing the service loop.</returns>
    protected abstract Task RunCoreAsync(
        IAppExecutionContext context,
        CancellationToken sessionCancellationToken);

    /// <summary>
    /// Handles a bounded stop request: called once per run, when the OS announces the stop
    /// (before the session token is cancelled, ADR 0069) or, at the latest, when the ecosystem stops
    /// the service. A service that holds state finishes or cancels its current operation here.
    /// </summary>
    /// <param name="reason">Reason the ecosystem requested the stop.</param>
    /// <param name="cancellationToken">Enforces the cleanup deadline.</param>
    /// <returns>A task representing cleanup.</returns>
    protected virtual ValueTask OnStoppingAsync(
        ServiceStopReason reason,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}