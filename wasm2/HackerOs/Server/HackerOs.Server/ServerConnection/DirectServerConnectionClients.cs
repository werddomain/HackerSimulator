using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Server.Contracts.Identity;
using HackerOs.Server.Contracts.Proxy;
using HackerOs.Server.Contracts.Sync;
using HackerOs.Server.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HackerOs.Server.ServerConnection;

// =============================================================================
// Direct-injection IAccountClient/IProxyClient/ISyncClient (ADR 0036 / Pass N+5)
//
// Registered only by Server/HackerOs.Server/Program.cs, before AddHackerOsEcosystem
// runs (see EcosystemServiceCollectionExtensions.cs's TryAddSingleton calls) — the
// two WASM hosts never see these types and keep using Http*Client unchanged.
//
// Each class is Singleton (matching the interfaces' existing lifetime) but never
// construct-injects IAccountService/ISyncService/IProxyService directly — those are
// Scoped, and (per ProxyService's own dependency on HackerOsServerDbContext) hold an
// EF Core DbContext that must not be captured and reused forever by a singleton.
// Each method instead creates a short-lived IServiceScope, resolves the scoped
// service from it, and disposes the scope when the call completes.
// =============================================================================

/// <summary>Direct in-process <see cref="IAccountClient"/> — calls <see cref="IAccountService"/> without HTTP.</summary>
public sealed class DirectAccountClient(IServiceScopeFactory scopeFactory) : IAccountClient
{
    public async Task<CreateAccountResponse> CreateAccountAsync(
        Uri serverBaseUrl, CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IAccountService service = scope.ServiceProvider.GetRequiredService<IAccountService>();
        try
        {
            return await service.CreateAccountAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            throw new ServerConnectionException(ex.Message);
        }
    }

    public async Task<LoginResponse> LoginAsync(
        Uri serverBaseUrl, LoginRequest request, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IAccountService service = scope.ServiceProvider.GetRequiredService<IAccountService>();
        try
        {
            return await service.LoginAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ServerConnectionException(ex.Message);
        }
    }

    public async Task<RefreshTokenResponse> RefreshAsync(
        Uri serverBaseUrl, RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IAccountService service = scope.ServiceProvider.GetRequiredService<IAccountService>();
        try
        {
            return await service.RefreshAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ServerConnectionException(ex.Message);
        }
    }
}

/// <summary>Direct in-process <see cref="ISyncClient"/> — calls <see cref="ISyncService"/> without HTTP.</summary>
public sealed class DirectSyncClient(IServiceScopeFactory scopeFactory, ITokenService tokenService) : ISyncClient
{
    public async Task<PullResponse> PullAsync(
        Uri serverBaseUrl, string accessToken, PullRequest request, CancellationToken cancellationToken = default)
    {
        (Guid accountId, _) = await ValidateAsync(accessToken, cancellationToken).ConfigureAwait(false);
        using IServiceScope scope = scopeFactory.CreateScope();
        ISyncService service = scope.ServiceProvider.GetRequiredService<ISyncService>();
        try
        {
            return await service.PullAsync(accountId, request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException)
        {
            throw new ServerConnectionException(ex.Message);
        }
    }

    public async Task<PushResponse> PushAsync(
        Uri serverBaseUrl, string accessToken, PushRequest request, CancellationToken cancellationToken = default)
    {
        (Guid accountId, Guid deviceId) = await ValidateAsync(accessToken, cancellationToken).ConfigureAwait(false);
        using IServiceScope scope = scopeFactory.CreateScope();
        ISyncService service = scope.ServiceProvider.GetRequiredService<ISyncService>();
        try
        {
            return await service.PushAsync(accountId, deviceId, request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException)
        {
            throw new ServerConnectionException(ex.Message);
        }
    }

    public async Task<ResolveSyncConflictResponse> ResolveConflictAsync(
        Uri serverBaseUrl, string accessToken, ResolveSyncConflictRequest request, CancellationToken cancellationToken = default)
    {
        (Guid accountId, _) = await ValidateAsync(accessToken, cancellationToken).ConfigureAwait(false);
        using IServiceScope scope = scopeFactory.CreateScope();
        ISyncService service = scope.ServiceProvider.GetRequiredService<ISyncService>();
        try
        {
            return await service.ResolveConflictAsync(accountId, request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException)
        {
            throw new ServerConnectionException(ex.Message);
        }
    }

    private async Task<(Guid AccountId, Guid DeviceId)> ValidateAsync(string accessToken, CancellationToken cancellationToken)
    {
        TokenValidationResult result = await tokenService.ValidateAccessTokenAsync(accessToken, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid)
        {
            throw new ServerConnectionException(result.FailureReason ?? "The access token is invalid or has expired.");
        }
        return (result.AccountId, result.DeviceId);
    }
}

/// <summary>Direct in-process <see cref="IProxyClient"/> — calls <see cref="IProxyService"/> without HTTP.</summary>
public sealed class DirectProxyClient(IServiceScopeFactory scopeFactory, ITokenService tokenService) : IProxyClient
{
    public async Task<ProxyHttpResponse> ExecuteHttpRequestAsync(
        Uri serverBaseUrl, string accessToken, ProxyHttpRequest request, CancellationToken cancellationToken = default)
    {
        (Guid accountId, Guid deviceId) = await ValidateAsync(accessToken, cancellationToken).ConfigureAwait(false);
        using IServiceScope scope = scopeFactory.CreateScope();
        IProxyService service = scope.ServiceProvider.GetRequiredService<IProxyService>();
        try
        {
            return await service.ExecuteHttpRequestAsync(accountId, deviceId, request, cancellationToken).ConfigureAwait(false);
        }
        catch (ProxyRequestException ex)
        {
            throw new ServerConnectionException($"The proxy request failed: {ex.ErrorCode} — {ex.Message}");
        }
    }

    public async Task<ProxyTcpProbeResponse> ExecuteTcpProbeAsync(
        Uri serverBaseUrl, string accessToken, ProxyTcpProbeRequest request, CancellationToken cancellationToken = default)
    {
        (Guid accountId, Guid deviceId) = await ValidateAsync(accessToken, cancellationToken).ConfigureAwait(false);
        using IServiceScope scope = scopeFactory.CreateScope();
        IProxyService service = scope.ServiceProvider.GetRequiredService<IProxyService>();
        try
        {
            return await service.ExecuteTcpProbeAsync(accountId, deviceId, request, cancellationToken).ConfigureAwait(false);
        }
        catch (ProxyRequestException ex)
        {
            throw new ServerConnectionException($"The TCP probe request failed: {ex.ErrorCode} — {ex.Message}");
        }
    }

    public async Task<ProxyPolicyResponse> GetPolicyAsync(
        Uri serverBaseUrl, string accessToken, CancellationToken cancellationToken = default)
    {
        (_, Guid deviceId) = await ValidateAsync(accessToken, cancellationToken).ConfigureAwait(false);
        using IServiceScope scope = scopeFactory.CreateScope();
        IProxyService service = scope.ServiceProvider.GetRequiredService<IProxyService>();
        try
        {
            return await service.GetPolicyAsync(deviceId, cancellationToken).ConfigureAwait(false);
        }
        catch (ProxyRequestException ex)
        {
            throw new ServerConnectionException($"The proxy policy request failed: {ex.ErrorCode} — {ex.Message}");
        }
    }

    private async Task<(Guid AccountId, Guid DeviceId)> ValidateAsync(string accessToken, CancellationToken cancellationToken)
    {
        TokenValidationResult result = await tokenService.ValidateAccessTokenAsync(accessToken, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid)
        {
            throw new ServerConnectionException(result.FailureReason ?? "The access token is invalid or has expired.");
        }
        return (result.AccountId, result.DeviceId);
    }
}
