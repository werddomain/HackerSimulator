using System.Net.Http.Json;
using HackerOs.Server.Contracts.Identity;

namespace HackerOs.Platform.Core.ServerConnection;

/// <summary>
/// Thin HTTP wrapper over the optional server's account/auth endpoints (ADR 0028). Every member
/// takes only <see cref="HackerOs.Server.Contracts"/> DTOs and primitives — no ASP.NET-specific
/// types — so a future direct-injection implementation for the server-hosted host is possible
/// without redesigning this interface. Browser-independent (plain <see cref="HttpClient"/>), unlike
/// the IndexedDB-backed <c>IServerConnectionRepository</c> implementation.
/// </summary>
public interface IAccountClient
{
    Task<CreateAccountResponse> CreateAccountAsync(Uri serverBaseUrl, CreateAccountRequest request, CancellationToken cancellationToken = default);

    Task<LoginResponse> LoginAsync(Uri serverBaseUrl, LoginRequest request, CancellationToken cancellationToken = default);

    Task<RefreshTokenResponse> RefreshAsync(Uri serverBaseUrl, RefreshTokenRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IAccountClient"/> implementation backed by <see cref="HttpClient"/>.</summary>
public sealed class HttpAccountClient(HttpClient httpClient) : IAccountClient
{
    public async Task<CreateAccountResponse> CreateAccountAsync(
        Uri serverBaseUrl, CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            new Uri(serverBaseUrl, "api/account"),
            request,
            IdentityContractsJsonContext.Default.CreateAccountRequest,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync(
            IdentityContractsJsonContext.Default.CreateAccountResponse, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<LoginResponse> LoginAsync(
        Uri serverBaseUrl, LoginRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            new Uri(serverBaseUrl, "api/auth/login"),
            request,
            IdentityContractsJsonContext.Default.LoginRequest,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync(
            IdentityContractsJsonContext.Default.LoginResponse, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<RefreshTokenResponse> RefreshAsync(
        Uri serverBaseUrl, RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            new Uri(serverBaseUrl, "api/auth/refresh"),
            request,
            IdentityContractsJsonContext.Default.RefreshTokenRequest,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync(
            IdentityContractsJsonContext.Default.RefreshTokenResponse, cancellationToken).ConfigureAwait(false))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new ServerConnectionException(
            $"The optional server rejected the request ({(int)response.StatusCode} {response.ReasonPhrase}).", body);
    }
}

/// <summary>Thrown when the optional server rejects an account/auth/proxy request.</summary>
public sealed class ServerConnectionException(string message, string? serverBody = null)
    : Exception(message)
{
    /// <summary>Gets the raw response body the server returned, if any, for diagnostics.</summary>
    public string? ServerBody { get; } = serverBody;
}
