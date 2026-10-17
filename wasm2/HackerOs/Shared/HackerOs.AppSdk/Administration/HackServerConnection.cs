using HackerOs.AppSdk.IO;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.AppSdk.Administration;

/// <summary>
/// Connects this device to the optional HackerOS server and synchronizes it (ADR 0056,
/// section 10), as Settings &gt; Server Connection does. Replaces <c>IServerConnectionService</c>
/// and the sync services, which apps may no longer inject (ADR 0055).
/// </summary>
/// <remarks>
/// <para>
/// Every member requires the dangerous capability <c>server.connection.manage</c>; a missing one
/// raises <see cref="HackUnauthorizedAccessException"/>.
/// </para>
/// <para>
/// The server is optional (<c>AGENTS.md</c>): without one, <see cref="GetStateAsync(CancellationToken)"/> reports
/// <see cref="HackServerConnectionState.NotConnected"/> and <see cref="SyncAsync(HackSyncDomain, HackSyncDirection, CancellationToken)"/>
/// does nothing. Only a request the user made (connect, create an account, synchronize) raises
/// <see cref="HackServerConnectionException"/> when the server refuses it or cannot be reached.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await HackServerConnection.ConnectAsync(new Uri("https://hackeros.example"), "benoit", password);
/// await HackServerConnection.SyncAsync(HackSyncDomain.FileSystem, HackSyncDirection.Push);
/// </code>
/// </example>
public static class HackServerConnection
{
    /// <summary>Gets the current connection.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="HackUnauthorizedAccessException">This app lacks <c>server.connection.manage</c>.</exception>
    /// <exception cref="HackExecutionContextMissingException">No app context is active.</exception>
    public static Task<HackServerConnectionState> GetStateAsync(CancellationToken cancellationToken = default) =>
        GetStateAsync(HackExecutionContext.Current, cancellationToken);

    /// <summary>Gets the current connection through an explicit context.</summary>
    /// <param name="context">The calling app's execution context.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static Task<HackServerConnectionState> GetStateAsync(IAppExecutionContext context, CancellationToken cancellationToken = default) =>
        Call(context, gateway => gateway.GetStateAsync(cancellationToken));

    /// <summary>Links this device to an existing server account.</summary>
    /// <param name="serverUrl">Absolute server base URL.</param>
    /// <param name="login">Server account name.</param>
    /// <param name="password">Server account password.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="HackServerConnectionException">The server refused the credentials or could not be reached.</exception>
    /// <exception cref="HackUnauthorizedAccessException">This app lacks <c>server.connection.manage</c>.</exception>
    public static Task<HackServerConnectionState> ConnectAsync(
        Uri serverUrl, string login, string password, CancellationToken cancellationToken = default) =>
        ConnectAsync(HackExecutionContext.Current, serverUrl, login, password, cancellationToken);

    /// <summary>Links this device to an existing server account through an explicit context.</summary>
    /// <param name="context">The calling app's execution context.</param>
    /// <param name="serverUrl">Absolute server base URL.</param>
    /// <param name="login">Server account name.</param>
    /// <param name="password">Server account password.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public static Task<HackServerConnectionState> ConnectAsync(
        IAppExecutionContext context, Uri serverUrl, string login, string password, CancellationToken cancellationToken = default)
    {
        ValidateCredentials(serverUrl, login, password);
        return Call(context, gateway => gateway.ConnectAsync(serverUrl, login, password, cancellationToken));
    }

    /// <summary>Creates a server account and links this device to it.</summary>
    /// <param name="serverUrl">Absolute server base URL.</param>
    /// <param name="login">New account name.</param>
    /// <param name="password">New account password.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="HackServerConnectionException">The server refused the account or could not be reached.</exception>
    /// <exception cref="HackUnauthorizedAccessException">This app lacks <c>server.connection.manage</c>.</exception>
    public static Task<HackServerConnectionState> CreateAccountAsync(
        Uri serverUrl, string login, string password, CancellationToken cancellationToken = default) =>
        CreateAccountAsync(HackExecutionContext.Current, serverUrl, login, password, cancellationToken);

    /// <summary>Creates a server account through an explicit context.</summary>
    /// <param name="context">The calling app's execution context.</param>
    /// <param name="serverUrl">Absolute server base URL.</param>
    /// <param name="login">New account name.</param>
    /// <param name="password">New account password.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public static Task<HackServerConnectionState> CreateAccountAsync(
        IAppExecutionContext context, Uri serverUrl, string login, string password, CancellationToken cancellationToken = default)
    {
        ValidateCredentials(serverUrl, login, password);
        return Call(context, gateway => gateway.CreateAccountAsync(serverUrl, login, password, cancellationToken));
    }

    /// <summary>Unlinks this device from its server account.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="HackUnauthorizedAccessException">This app lacks <c>server.connection.manage</c>.</exception>
    public static Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        DisconnectAsync(HackExecutionContext.Current, cancellationToken);

    /// <summary>Unlinks this device through an explicit context.</summary>
    /// <param name="context">The calling app's execution context.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public static Task DisconnectAsync(IAppExecutionContext context, CancellationToken cancellationToken = default) =>
        Call(context, async gateway =>
        {
            await gateway.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            return true;
        });

    /// <summary>Runs one synchronization pass; does nothing when the device is not connected.</summary>
    /// <param name="domain">What to synchronize.</param>
    /// <param name="direction">Push or pull. <see cref="HackSyncDomain.Grants"/> only pulls.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <exception cref="ArgumentException"><see cref="HackSyncDirection.Push"/> was asked for <see cref="HackSyncDomain.Grants"/>.</exception>
    /// <exception cref="HackServerConnectionException">The server refused the pass or could not be reached.</exception>
    /// <exception cref="HackUnauthorizedAccessException">This app lacks <c>server.connection.manage</c>.</exception>
    public static Task SyncAsync(HackSyncDomain domain, HackSyncDirection direction, CancellationToken cancellationToken = default) =>
        SyncAsync(HackExecutionContext.Current, domain, direction, cancellationToken);

    /// <summary>Runs one synchronization pass through an explicit context.</summary>
    /// <param name="context">The calling app's execution context.</param>
    /// <param name="domain">What to synchronize.</param>
    /// <param name="direction">Push or pull.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    public static Task SyncAsync(
        IAppExecutionContext context, HackSyncDomain domain, HackSyncDirection direction, CancellationToken cancellationToken = default)
    {
        if (domain == HackSyncDomain.Grants && direction == HackSyncDirection.Push)
        {
            throw new ArgumentException("Grants are issued by the server: this domain can only be pulled (ADR 0031).", nameof(direction));
        }

        return Call(context, async gateway =>
        {
            await gateway.SyncAsync(domain, direction, cancellationToken).ConfigureAwait(false);
            return true;
        });
    }

    /// <summary>Gets the records the last synchronization could not merge.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="HackUnauthorizedAccessException">This app lacks <c>server.connection.manage</c>.</exception>
    public static Task<IReadOnlyList<HackSyncConflict>> GetConflictsAsync(CancellationToken cancellationToken = default) =>
        GetConflictsAsync(HackExecutionContext.Current, cancellationToken);

    /// <summary>Gets the unmerged records through an explicit context.</summary>
    /// <param name="context">The calling app's execution context.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static Task<IReadOnlyList<HackSyncConflict>> GetConflictsAsync(IAppExecutionContext context, CancellationToken cancellationToken = default) =>
        Call(context, gateway => gateway.GetConflictsAsync(cancellationToken));

    private static void ValidateCredentials(Uri serverUrl, string login, string password)
    {
        ArgumentNullException.ThrowIfNull(serverUrl);
        if (!serverUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("The server URL must be absolute.", nameof(serverUrl));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        ArgumentException.ThrowIfNullOrEmpty(password);
    }

    private static async Task<T> Call<T>(IAppExecutionContext context, Func<IAppServerConnectionGateway, ValueTask<T>> call)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return await call(context.ServerConnection).ConfigureAwait(false);
        }
        catch (AppGatewayAccessDeniedException exception)
        {
            throw HackGatewayErrors.ToUnauthorized(exception);
        }
    }
}
