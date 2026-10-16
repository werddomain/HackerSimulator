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
    Faulted
}

/// <summary>
/// Base class for non-visual work that lasts only for the active OS session.
/// </summary>
public abstract class ServiceAppBase : AppBase
{
    private IAppExecutionContext? _context;

    /// <summary>Initializes a service application with its validated manifest.</summary>
    /// <param name="manifest">Service application manifest.</param>
    protected ServiceAppBase(AppManifest manifest)
        : base(manifest, AppKind.Service)
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
        return RunInScopeAsync(context, sessionCancellationToken);
    }

    /// <summary>
    /// Gives the service a bounded opportunity to release in-memory resources.
    /// </summary>
    /// <param name="reason">Reason the ecosystem requested the stop.</param>
    /// <param name="cancellationToken">Enforces the ecosystem shutdown deadline.</param>
    /// <returns>A task representing bounded cleanup.</returns>
    /// <remarks>Runs inside the same ambient context scope as <see cref="RunAsync"/> when the service was started.</remarks>
    public async ValueTask StopAsync(ServiceStopReason reason, CancellationToken cancellationToken)
    {
        using IDisposable? scope = _context is null ? null : HackExecutionContext.Enter(_context);
        await OnStoppingAsync(reason, cancellationToken);
    }

    private async Task RunInScopeAsync(IAppExecutionContext context, CancellationToken sessionCancellationToken)
    {
        using IDisposable scope = HackExecutionContext.Enter(context);
        await RunCoreAsync(context, sessionCancellationToken);
    }

    /// <summary>Implements the active service loop.</summary>
    /// <param name="context">Scoped application execution context.</param>
    /// <param name="sessionCancellationToken">Signals the end of the OS session.</param>
    /// <returns>A task representing the service loop.</returns>
    protected abstract Task RunCoreAsync(
        IAppExecutionContext context,
        CancellationToken sessionCancellationToken);

    /// <summary>Handles a bounded stop request after session cancellation is raised.</summary>
    /// <param name="reason">Reason the ecosystem requested the stop.</param>
    /// <param name="cancellationToken">Enforces the cleanup deadline.</param>
    /// <returns>A task representing cleanup.</returns>
    protected virtual ValueTask OnStoppingAsync(
        ServiceStopReason reason,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}