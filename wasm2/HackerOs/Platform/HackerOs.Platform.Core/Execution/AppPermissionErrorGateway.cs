using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Concrete permission-error notification hub for one app instance. Constructed once per
/// <see cref="AppExecutionContext"/> and shared, unchanged, by every
/// <see cref="AppFileSystemGateway"/> scoped clone (including ones returned from
/// <see cref="IAppFileSystemGateway.WithSelectedHandle"/>), so a subscription registered through
/// <see cref="IAppExecutionContext.PermissionErrors"/> observes denials raised by any of them.
/// </summary>
public sealed class AppPermissionErrorGateway : IAppPermissionErrorGateway
{
    /// <inheritdoc />
    public event EventHandler<AppPermissionErrorEventArgs>? PermissionDenied;

    /// <summary>Raises <see cref="PermissionDenied"/> for one permission-class failure.</summary>
    internal void Raise(FileSystemError error) =>
        PermissionDenied?.Invoke(this, new AppPermissionErrorEventArgs(error));
}
