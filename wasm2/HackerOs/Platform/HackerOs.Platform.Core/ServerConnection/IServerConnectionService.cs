using System.Security.Cryptography;
using HackerOs.Platform.Core.Sessions;
using HackerOs.Server.Contracts.Identity;
using HackerOs.Simulation.Abstractions.ServerConnection;

namespace HackerOs.Platform.Core.ServerConnection;

/// <summary>
/// Orchestrates the per-device optional-server connection (ADR 0028): connect (create-or-login),
/// disconnect, and on-demand access-token refresh. Access tokens are never persisted — only the
/// longer-lived refresh token is — so every caller needing one goes through
/// <see cref="EnsureAccessTokenAsync"/>, which caches the current token in memory for this process
/// only and refreshes it once it is close to expiry.
/// </summary>
public interface IServerConnectionService
{
    /// <summary>Gets the current connection state, or <see langword="null"/> if this device is not connected.</summary>
    ValueTask<ServerConnectionState?> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new server account on <paramref name="serverBaseUrl"/> and connects this device to it.</summary>
    Task<ServerConnectionState> ConnectWithNewAccountAsync(
        Uri serverBaseUrl, string username, string password, CancellationToken cancellationToken = default);

    /// <summary>Logs into an existing server account on <paramref name="serverBaseUrl"/> and connects this device to it.</summary>
    Task<ServerConnectionState> ConnectWithExistingAccountAsync(
        Uri serverBaseUrl, string username, string password, CancellationToken cancellationToken = default);

    /// <summary>Clears the stored connection; this device is disconnected until reconnected.</summary>
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a valid access token, refreshing it from the stored refresh token if the cached one
    /// is missing or close to expiry. Returns <see langword="null"/> if this device is not connected.
    /// </summary>
    Task<string?> EnsureAccessTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IServerConnectionService"/> implementation. Browser-independent by
/// construction: password hashing goes through the same optional <see cref="KeyDerivationAsyncDelegate"/>
/// seam <see cref="LocalPasswordHasher"/> already uses (falling back to managed PBKDF2 when absent)
/// instead of depending on a concrete browser Web Crypto type directly.
/// </summary>
public sealed class ServerConnectionService(
    IServerConnectionRepository repository,
    IAccountClient accountClient,
    KeyDerivationAsyncDelegate? asyncPasswordHasher = null) : IServerConnectionService
{
    private const int SaltLengthBytes = 16;
    private const int PasswordHashLengthBytes = 32;
    private const int PasswordHashIterations = 210_000;

    // In-memory only, per ADR 0028 Decision 4 — never persisted alongside the refresh token.
    private string? _cachedAccessToken;
    private DateTimeOffset _cachedAccessTokenExpiresUtc = DateTimeOffset.MinValue;

    public ValueTask<ServerConnectionState?> GetStateAsync(CancellationToken cancellationToken = default) =>
        repository.GetAsync(cancellationToken);

    public Task<ServerConnectionState> ConnectWithNewAccountAsync(
        Uri serverBaseUrl, string username, string password, CancellationToken cancellationToken = default) =>
        ConnectAsync(serverBaseUrl, username, password, createNewAccount: true, cancellationToken);

    public Task<ServerConnectionState> ConnectWithExistingAccountAsync(
        Uri serverBaseUrl, string username, string password, CancellationToken cancellationToken = default) =>
        ConnectAsync(serverBaseUrl, username, password, createNewAccount: false, cancellationToken);

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await repository.ClearAsync(cancellationToken).ConfigureAwait(false);
        _cachedAccessToken = null;
        _cachedAccessTokenExpiresUtc = DateTimeOffset.MinValue;
    }

    public async Task<string?> EnsureAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        ServerConnectionState? state = await repository.GetAsync(cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return null;
        }

        if (_cachedAccessToken is not null && DateTimeOffset.UtcNow < _cachedAccessTokenExpiresUtc - TimeSpan.FromSeconds(30))
        {
            return _cachedAccessToken;
        }

        RefreshTokenResponse refreshed = await accountClient.RefreshAsync(
            new Uri(state.ServerBaseUrl),
            new RefreshTokenRequest(state.RefreshTokenOpaque, state.DeviceFingerprint),
            cancellationToken).ConfigureAwait(false);

        _cachedAccessToken = refreshed.AccessToken;
        _cachedAccessTokenExpiresUtc = refreshed.AccessTokenExpiresUtc;

        // Refresh tokens rotate on every use (ADR 0024) — persist the new one immediately so a
        // later refresh doesn't retry an already-invalidated token.
        await repository.SetAsync(
            state with { RefreshTokenOpaque = refreshed.RefreshToken, RefreshTokenExpiresUtc = refreshed.RefreshTokenExpiresUtc },
            cancellationToken).ConfigureAwait(false);

        return _cachedAccessToken;
    }

    private async Task<ServerConnectionState> ConnectAsync(
        Uri serverBaseUrl, string username, string password, bool createNewAccount, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        string deviceFingerprint = Guid.NewGuid().ToString("N");
        byte[] salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
        byte[] hash;
        if (asyncPasswordHasher is not null)
        {
            try
            {
                hash = await asyncPasswordHasher(
                    password, salt, PasswordHashIterations, PasswordHashLengthBytes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Graceful fallback to managed PBKDF2, matching LocalPasswordHasher's own pattern.
                hash = Rfc2898DeriveBytes.Pbkdf2(
                    password, salt, PasswordHashIterations, HashAlgorithmName.SHA256, PasswordHashLengthBytes);
            }
        }
        else
        {
            hash = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, PasswordHashIterations, HashAlgorithmName.SHA256, PasswordHashLengthBytes);
        }

        string passwordHashBase64 = Convert.ToBase64String(hash);
        string saltBase64 = Convert.ToBase64String(salt);
        Guid accountId;
        Guid deviceId;
        string refreshToken;
        DateTimeOffset refreshTokenExpiresUtc;

        if (createNewAccount)
        {
            CreateAccountResponse created = await accountClient.CreateAccountAsync(
                serverBaseUrl,
                new CreateAccountRequest(username, passwordHashBase64, saltBase64, DeviceName: "HackerOS", deviceFingerprint),
                cancellationToken).ConfigureAwait(false);
            LoginResponse login = await accountClient.LoginAsync(
                serverBaseUrl,
                new LoginRequest(username, passwordHashBase64, deviceFingerprint),
                cancellationToken).ConfigureAwait(false);
            accountId = created.AccountId;
            deviceId = login.DeviceId;
            refreshToken = login.RefreshToken;
            refreshTokenExpiresUtc = login.RefreshTokenExpiresUtc;
            _cachedAccessToken = login.AccessToken;
            _cachedAccessTokenExpiresUtc = login.AccessTokenExpiresUtc;
        }
        else
        {
            LoginResponse login = await accountClient.LoginAsync(
                serverBaseUrl,
                new LoginRequest(username, passwordHashBase64, deviceFingerprint),
                cancellationToken).ConfigureAwait(false);
            accountId = login.AccountId;
            deviceId = login.DeviceId;
            refreshToken = login.RefreshToken;
            refreshTokenExpiresUtc = login.RefreshTokenExpiresUtc;
            _cachedAccessToken = login.AccessToken;
            _cachedAccessTokenExpiresUtc = login.AccessTokenExpiresUtc;
        }

        ServerConnectionState state = new(
            accountId, deviceId, serverBaseUrl.ToString(), deviceFingerprint, refreshToken, refreshTokenExpiresUtc);
        await repository.SetAsync(state, cancellationToken).ConfigureAwait(false);
        return state;
    }
}
