using System.Net.Http.Headers;
using System.Net.Http.Json;
using HackerOs.Server.Contracts.Sync;

namespace HackerOs.Platform.Core.ServerConnection;

/// <summary>
/// Thin HTTP wrapper over the optional server's chunked, content-addressed file
/// transfer endpoints (ADR 0030). Same browser-independent, direct-injection-friendly
/// shape as <see cref="ISyncClient"/> — carries only opaque bytes/hashes, never
/// interprets filesystem semantics itself.
/// </summary>
public interface IContentTransferClient
{
    Task<InitiateContentUploadResponse> InitiateUploadAsync(
        Uri serverBaseUrl, string accessToken, InitiateContentUploadRequest request, CancellationToken cancellationToken = default);

    Task<QueryUploadProgressResponse> QueryUploadProgressAsync(
        Uri serverBaseUrl, string accessToken, string uploadSessionId, CancellationToken cancellationToken = default);

    Task UploadChunkAsync(
        Uri serverBaseUrl, string accessToken, string uploadSessionId, int chunkIndex, byte[] chunkData, CancellationToken cancellationToken = default);

    Task<InitiateContentDownloadResponse> InitiateDownloadAsync(
        Uri serverBaseUrl, string accessToken, InitiateContentDownloadRequest request, CancellationToken cancellationToken = default);

    Task<byte[]> DownloadChunkAsync(
        Uri serverBaseUrl, string accessToken, string contentHash, int chunkIndex, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IContentTransferClient"/> implementation backed by <see cref="HttpClient"/>.</summary>
public sealed class HttpContentTransferClient(HttpClient httpClient) : IContentTransferClient
{
    public async Task<InitiateContentUploadResponse> InitiateUploadAsync(
        Uri serverBaseUrl, string accessToken, InitiateContentUploadRequest request, CancellationToken cancellationToken = default)
    {
        using HttpContent content = JsonContent.Create(request, ContentTransferContractsJsonContext.Default.InitiateContentUploadRequest);
        using HttpResponseMessage response = await SendAsync(
            serverBaseUrl, "api/sync/content/upload", HttpMethod.Post, accessToken, content, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync(
            ContentTransferContractsJsonContext.Default.InitiateContentUploadResponse, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<QueryUploadProgressResponse> QueryUploadProgressAsync(
        Uri serverBaseUrl, string accessToken, string uploadSessionId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(
            serverBaseUrl, $"api/sync/content/upload/{Uri.EscapeDataString(uploadSessionId)}/progress",
            HttpMethod.Get, accessToken, content: null, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync(
            ContentTransferContractsJsonContext.Default.QueryUploadProgressResponse, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task UploadChunkAsync(
        Uri serverBaseUrl, string accessToken, string uploadSessionId, int chunkIndex, byte[] chunkData, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var chunkContent = new ByteArrayContent(chunkData);
        chunkContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(chunkContent, "chunk", "chunk.bin");

        using HttpResponseMessage response = await SendAsync(
            serverBaseUrl, $"api/sync/content/upload/{Uri.EscapeDataString(uploadSessionId)}/chunks/{chunkIndex}",
            HttpMethod.Put, accessToken, form, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<InitiateContentDownloadResponse> InitiateDownloadAsync(
        Uri serverBaseUrl, string accessToken, InitiateContentDownloadRequest request, CancellationToken cancellationToken = default)
    {
        using HttpContent content = JsonContent.Create(request, ContentTransferContractsJsonContext.Default.InitiateContentDownloadRequest);
        using HttpResponseMessage response = await SendAsync(
            serverBaseUrl, "api/sync/content/download", HttpMethod.Post, accessToken, content, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync(
            ContentTransferContractsJsonContext.Default.InitiateContentDownloadResponse, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<byte[]> DownloadChunkAsync(
        Uri serverBaseUrl, string accessToken, string contentHash, int chunkIndex, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(
            serverBaseUrl, $"api/sync/content/download/{Uri.EscapeDataString(contentHash)}/chunks/{chunkIndex}",
            HttpMethod.Get, accessToken, content: null, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Uri serverBaseUrl, string relativePath, HttpMethod method, string accessToken, HttpContent? content, CancellationToken cancellationToken)
    {
        using HttpRequestMessage message = new(method, new Uri(serverBaseUrl, relativePath))
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
            $"The content transfer request failed ({(int)response.StatusCode} {response.ReasonPhrase}).", body);
    }
}
