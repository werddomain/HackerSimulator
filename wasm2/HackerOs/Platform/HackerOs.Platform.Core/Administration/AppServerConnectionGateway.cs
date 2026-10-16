using HackerOs.App.Abstractions;
using HackerOs.AppSdk.Administration;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Simulation.Abstractions.Gateways;
using ServerState = HackerOs.Simulation.Abstractions.ServerConnection.ServerConnectionState;

namespace HackerOs.Platform.Core.Administration;

/// <summary>
/// Platform services behind <see cref="AppServerConnectionGateway"/>, resolved lazily: none of them
/// exists on a host built without the optional server plumbing.
/// </summary>
/// <param name="Connection">Server account link (ADR 0028).</param>
/// <param name="FileSystemSync">Home directory sync (ADR 0030).</param>
/// <param name="GrantsSync">Grant pull (ADR 0031).</param>
/// <param name="FileAssociationsSync">File association sync (ADR 0033).</param>
public sealed record ServerConnectionGatewayServices(
    Func<IServerConnectionService> Connection,
    Func<IFileSystemSyncService>? FileSystemSync = null,
    Func<IGrantsSyncService>? GrantsSync = null,
    Func<IFileAssociationsSyncService>? FileAssociationsSync = null);

/// <summary>
/// Trusted <see cref="IAppServerConnectionGateway"/> (ADR 0056, section 10) over the server
/// connection and sync services of Platform Core.
/// </summary>
/// <remarks>
/// <para>
/// Every call requires <c>server.connection.manage</c>. The tokens stay here: the app only sees the
/// server URL and the account and device identifiers.
/// </para>
/// <para>
/// The server is optional (<c>AGENTS.md</c>): with no services the state is "not connected" and a
/// sync does nothing. Transport and server errors of a request the user made are rethrown as
/// <see cref="HackServerConnectionException"/>.
/// </para>
/// </remarks>
internal sealed class AppServerConnectionGateway : IAppServerConnectionGateway
{
    private readonly ServerConnectionGatewayServices? _services;
    private readonly ICapabilityChecker _capabilities;

    /// <summary>Creates the gateway for one app instance.</summary>
    /// <param name="services">Server services; <see langword="null"/> on a host without them.</param>
    /// <param name="capabilities">The app's capability checker.</param>
    public AppServerConnectionGateway(ServerConnectionGatewayServices? services, ICapabilityChecker capabilities)
    {
        _services = services;
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    }

    /// <inheritdoc />
    public async ValueTask<HackServerConnectionState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        _capabilities.Require(AppCapabilities.ServerConnectionManage);
        if (_services is null)
        {
            return HackServerConnectionState.NotConnected;
        }

        return ToPublic(await _services.Connection().GetStateAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public ValueTask<HackServerConnectionState> ConnectAsync(
        Uri serverUrl, string login, string password, CancellationToken cancellationToken = default) =>
        ConnectCoreAsync(serverUrl, login, password, createAccount: false, cancellationToken);

    /// <inheritdoc />
    public ValueTask<HackServerConnectionState> CreateAccountAsync(
        Uri serverUrl, string login, string password, CancellationToken cancellationToken = default) =>
        ConnectCoreAsync(serverUrl, login, password, createAccount: true, cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _capabilities.Require(AppCapabilities.ServerConnectionManage);
        if (_services is not null)
        {
            await _services.Connection().DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask SyncAsync(HackSyncDomain domain, HackSyncDirection direction, CancellationToken cancellationToken = default)
    {
        _capabilities.Require(AppCapabilities.ServerConnectionManage);
        if (domain == HackSyncDomain.Grants && direction == HackSyncDirection.Push)
        {
            throw new ArgumentException("Grants are issued by the server: this domain can only be pulled (ADR 0031).", nameof(direction));
        }

        if (_services is null)
        {
            return;
        }

        try
        {
            Task pass = (domain, direction) switch
            {
                (HackSyncDomain.FileSystem, HackSyncDirection.Push) => _services.FileSystemSync?.Invoke().PushAsync(cancellationToken),
                (HackSyncDomain.FileSystem, HackSyncDirection.Pull) => _services.FileSystemSync?.Invoke().PullAsync(cancellationToken),
                (HackSyncDomain.Grants, _) => _services.GrantsSync?.Invoke().PullAsync(cancellationToken),
                (HackSyncDomain.FileAssociations, HackSyncDirection.Push) => _services.FileAssociationsSync?.Invoke().PushAsync(cancellationToken),
                (HackSyncDomain.FileAssociations, HackSyncDirection.Pull) => _services.FileAssociationsSync?.Invoke().PullAsync(cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unknown sync domain.")
            } ?? Task.CompletedTask;
            await pass.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ServerConnectionException or HttpRequestException)
        {
            throw new HackServerConnectionException($"Sync failed: {exception.Message}", exception);
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<HackSyncConflict>> GetConflictsAsync(CancellationToken cancellationToken = default)
    {
        _capabilities.Require(AppCapabilities.ServerConnectionManage);
        IReadOnlyList<HackSyncConflict> conflicts = _services?.FileSystemSync is { } fileSystemSync
            ? [.. fileSystemSync().UnresolvedConflicts.Select(path => new HackSyncConflict(HackSyncDomain.FileSystem, path))]
            : [];
        return ValueTask.FromResult(conflicts);
    }

    private async ValueTask<HackServerConnectionState> ConnectCoreAsync(
        Uri serverUrl, string login, string password, bool createAccount, CancellationToken cancellationToken)
    {
        _capabilities.Require(AppCapabilities.ServerConnectionManage);
        if (_services is null)
        {
            throw new HackServerConnectionException("This HackerOS host has no optional-server support.");
        }

        try
        {
            IServerConnectionService connection = _services.Connection();
            ServerState state = createAccount
                ? await connection.ConnectWithNewAccountAsync(serverUrl, login, password, cancellationToken).ConfigureAwait(false)
                : await connection.ConnectWithExistingAccountAsync(serverUrl, login, password, cancellationToken).ConfigureAwait(false);
            return ToPublic(state);
        }
        catch (Exception exception) when (exception is ServerConnectionException or HttpRequestException)
        {
            throw new HackServerConnectionException($"Could not connect: {exception.Message}", exception);
        }
    }

    private static HackServerConnectionState ToPublic(ServerState? state) =>
        state is null
            ? HackServerConnectionState.NotConnected
            : new HackServerConnectionState(
                true,
                Uri.TryCreate(state.ServerBaseUrl, UriKind.Absolute, out Uri? url) ? url : null,
                state.AccountId,
                state.DeviceId);
}
