namespace HackerOs.AppSdk.Administration;

/// <summary>Connection of this device to the optional HackerOS server (ADR 0028), as an app sees it.</summary>
/// <param name="IsConnected">Whether the device is linked to a server account.</param>
/// <param name="ServerUrl">Server base URL, or <see langword="null"/> when not connected.</param>
/// <param name="AccountId">Server account identifier, or <see langword="null"/> when not connected.</param>
/// <param name="DeviceId">Device identifier on that server, or <see langword="null"/> when not connected.</param>
/// <remarks>The refresh and access tokens stay inside the OS: no app ever sees them (ADR 0056, section 6).</remarks>
public sealed record HackServerConnectionState(bool IsConnected, Uri? ServerUrl, Guid? AccountId, Guid? DeviceId)
{
    /// <summary>The state of a device that is not connected, the normal case without a server.</summary>
    public static HackServerConnectionState NotConnected { get; } = new(false, null, null, null);
}

/// <summary>What a synchronization moves (ADR 0029 to 0033).</summary>
public enum HackSyncDomain
{
    /// <summary>The user's home directory, user settings included (ADR 0030, ADR 0044 section 10).</summary>
    FileSystem = 0,

    /// <summary>Capability grants issued by the server; pull only (ADR 0031).</summary>
    Grants = 1,

    /// <summary><c>/etc/hackeros/file-associations.json</c> (ADR 0033).</summary>
    FileAssociations = 2
}

/// <summary>Direction of one synchronization pass.</summary>
public enum HackSyncDirection
{
    /// <summary>Send local changes to the server.</summary>
    Push = 0,

    /// <summary>Apply the server's changes locally.</summary>
    Pull = 1
}

/// <summary>A record a synchronization could not merge: both sides changed it (ADR 0030).</summary>
/// <param name="Domain">Domain of the record.</param>
/// <param name="Path">Virtual path of the conflicting file.</param>
public sealed record HackSyncConflict(HackSyncDomain Domain, string Path);

/// <summary>
/// The server refused a request or could not be reached while the user asked for it (connect,
/// create an account, synchronize). Never raised merely because no server is configured.
/// </summary>
public sealed class HackServerConnectionException : IOException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Human-readable reason.</param>
    /// <param name="innerException">The transport or server error.</param>
    public HackServerConnectionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Optional server connection and synchronization for one app instance (ADR 0056, section 10),
/// behind <see cref="HackServerConnection"/>. Every member requires the dangerous capability
/// <c>server.connection.manage</c>.
/// </summary>
public interface IAppServerConnectionGateway
{
    /// <summary>Gets the current connection; <see cref="HackServerConnectionState.NotConnected"/> without a server.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    ValueTask<HackServerConnectionState> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Links this device to an existing server account.</summary>
    /// <param name="serverUrl">Absolute server base URL.</param>
    /// <param name="login">Server account name.</param>
    /// <param name="password">Server account password (a credential of the remote service, not a local password).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    ValueTask<HackServerConnectionState> ConnectAsync(
        Uri serverUrl, string login, string password, CancellationToken cancellationToken = default);

    /// <summary>Creates a server account and links this device to it.</summary>
    /// <param name="serverUrl">Absolute server base URL.</param>
    /// <param name="login">New account name.</param>
    /// <param name="password">New account password.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    ValueTask<HackServerConnectionState> CreateAccountAsync(
        Uri serverUrl, string login, string password, CancellationToken cancellationToken = default);

    /// <summary>Unlinks this device; local data stays the source of truth.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs one synchronization pass; does nothing when not connected.</summary>
    /// <param name="domain">What to synchronize.</param>
    /// <param name="direction">Push or pull.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    ValueTask SyncAsync(HackSyncDomain domain, HackSyncDirection direction, CancellationToken cancellationToken = default);

    /// <summary>Gets the records the last synchronization could not merge.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    ValueTask<IReadOnlyList<HackSyncConflict>> GetConflictsAsync(CancellationToken cancellationToken = default);
}
