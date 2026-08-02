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
    public Task RunAsync(IAppExecutionContext context, CancellationToken sessionCancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return RunCoreAsync(context, sessionCancellationToken);
    }

    /// <summary>
    /// Gives the service a bounded opportunity to release in-memory resources.
    /// </summary>
    /// <param name="reason">Reason the ecosystem requested the stop.</param>
    /// <param name="cancellationToken">Enforces the ecosystem shutdown deadline.</param>
    /// <returns>A task representing bounded cleanup.</returns>
    public ValueTask StopAsync(ServiceStopReason reason, CancellationToken cancellationToken) =>
        OnStoppingAsync(reason, cancellationToken);

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