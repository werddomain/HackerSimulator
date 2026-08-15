using System.Net.Http.Headers;
using System.Net.Http.Json;
using HackerOs.Server.Contracts.Sync;

namespace HackerOs.Platform.Core.ServerConnection;

/// <summary>
/// Thin HTTP wrapper over the optional server's sync endpoints (ADR 0025/0029). Same
/// browser-independent, direct-injection-friendly shape as <see cref="IAccountClient"/>/
/// <see cref="IProxyClient"/>.
/// </summary>
public interface ISyncClient
{
    Task<PullResponse> PullAsync(
        Uri serverBaseUrl, string accessToken, PullRequest request, CancellationToken cancellationToken = default);

    Task<PushResponse> PushAsync(
        Uri serverBaseUrl, string accessToken, PushRequest request, CancellationToken cancellationToken = default);

    Task<ResolveSyncConflictResponse> ResolveConflictAsync(
        Uri serverBaseUrl, string accessToken, ResolveSyncConflictRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="ISyncClient"/> implementation backed by <see cref="HttpClient"/>.</summary>
public sealed class HttpSyncClient(HttpClient httpClient) : ISyncClient
{
    public async Task<PullResponse> PullAsync(
        Uri serverBaseUrl, string accessToken, PullRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(
            serverBaseUrl, "api/sync/pull", accessToken, JsonContent.Create(request, SyncContractsJsonContext.Default.PullRequest),
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync(
            SyncContractsJsonContext.Default.PullResponse, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<PushResponse> PushAsync(
        Uri serverBaseUrl, string accessToken, PushRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(
            serverBaseUrl, "api/sync/push", accessToken, JsonContent.Create(request, SyncContractsJsonContext.Default.PushRequest),
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync(
            SyncContractsJsonContext.Default.PushResponse, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<ResolveSyncConflictResponse> ResolveConflictAsync(
        Uri serverBaseUrl, string accessToken, ResolveSyncConflictRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(
            serverBaseUrl, "api/sync/conflicts/resolve", accessToken,
            JsonContent.Create(request, SyncContractsJsonContext.Default.ResolveSyncConflictRequest),
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync(
            SyncContractsJsonContext.Default.ResolveSyncConflictResponse, cancellationToken).ConfigureAwait(false))!;
    }

    private async Task<HttpResponseMessage> SendAsync(
        Uri serverBaseUrl, string relativePath, string accessToken, HttpContent content, CancellationToken cancellationToken)
    {
        using HttpRequestMessage message = new(HttpMethod.Post, new Uri(serverBaseUrl, relativePath))
        {
            Content = content
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new ServerConnectionException(
            $"The sync request failed ({(int)response.StatusCode} {response.ReasonPhrase}).", body);
    }
}
