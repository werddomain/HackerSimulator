using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Administration;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Platform.Core.Administration;

/// <summary>
/// Reads the raw storage of the machine for <c>HackStorageDiagnostics</c> (ADR 0056, section 11).
/// The browser host implements it over IndexedDB (<c>IndexedDbInspectorService</c> of
/// <c>Infrastructure.Browser</c>); Platform Core never touches the database itself.
/// </summary>
public interface IStorageInspector
{
    /// <summary>Reads every object store in one transaction and resolves each file's virtual path.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<HackStorageSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}

/// <summary>Trusted <see cref="IAppStorageDiagnosticsGateway"/>: checks <c>system.storage.inspect</c>, then reads the snapshot.</summary>
internal sealed class AppStorageDiagnosticsGateway : IAppStorageDiagnosticsGateway
{
    private readonly Func<IStorageInspector>? _inspector;
    private readonly ICapabilityChecker _capabilities;

    /// <summary>Creates the gateway for one app instance.</summary>
    /// <param name="inspector">Lazily resolved inspector; <see langword="null"/> on a host without persistent storage.</param>
    /// <param name="capabilities">The app's capability checker.</param>
    public AppStorageDiagnosticsGateway(Func<IStorageInspector>? inspector, ICapabilityChecker capabilities)
    {
        _inspector = inspector;
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    }

    /// <inheritdoc />
    public async ValueTask<HackStorageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        _capabilities.Require(AppCapabilities.SystemStorageInspect);
        IStorageInspector inspector = _inspector?.Invoke()
            ?? throw new NotSupportedException("This HackerOS host has no persistent storage to inspect.");
        return await inspector.CaptureAsync(cancellationToken).ConfigureAwait(false);
    }
}
