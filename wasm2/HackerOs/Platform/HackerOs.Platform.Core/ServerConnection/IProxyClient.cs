using System.Net.Http.Headers;
using System.Net.Http.Json;
using HackerOs.Server.Contracts.Proxy;

namespace HackerOs.Platform.Core.ServerConnection;

/// <summary>
/// Thin HTTP wrapper over the optional server's proxy endpoints (ADR 0028). Takes only
/// <see cref="HackerOs.Server.Contracts"/> DTOs and primitives, matching <see cref="IAccountClient"/>'s
/// direct-injection-friendly, browser-independent shape.
/// </summary>
/// <remarks>
/// <see cref="ProxyHttpResponse"/>'s <c>BodyBase64</c> is populated only when the request sets
/// <see cref="ProxyHttpRequest.IncludeBody"/> — false by default, so callers that only need
/// reachability/status/headers (e.g. <c>curl -I</c>, <c>ping</c>) get the original metadata-only
/// shape unchanged. Callers needing the fetched body (a normal <c>curl</c> GET) set it explicitly;
/// the response is capped at the same <c>MaxResponseBytes</c> limit as every other proxy response.
/// </remarks>
public interface IProxyClient
{
    /// <summary>
    /// Has the server perform one real HTTP request on the app's behalf (<c>POST api/proxy/http</c>),
    /// subject to the server's proxy policy and the app's stored capability grant.
    /// </summary>
    /// <param name="serverBaseUrl">The server's base URL; endpoint paths are resolved relative to it.</param>
    /// <param name="accessToken">The bearer token of the connected account.</param>
    /// <param name="request">The method, URL, headers, timeout and requesting app; <c>IncludeBody</c> asks for the body too.</param>
    /// <param name="cancellationToken">Cancels the HTTP request.</param>
    /// <returns>The status, headers and, when requested, the base64 body of the real response.</returns>
    /// <exception cref="ServerConnectionException">The server refused or failed the request; the message carries the proxy error code when there is one.</exception>
    Task<ProxyHttpResponse> ExecuteHttpRequestAsync(
        Uri serverBaseUrl, string accessToken, ProxyHttpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a single-port TCP reachability probe (ADR 0035) — one connect attempt against one
    /// host:port, no data exchanged. Used by <c>nmap</c>'s real-network fallback.
    /// </summary>
    Task<ProxyTcpProbeResponse> ExecuteTcpProbeAsync(
        Uri serverBaseUrl, string accessToken, ProxyTcpProbeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads the proxy limits the server applies to this device (<c>GET api/proxy/policy</c>).</summary>
    /// <param name="serverBaseUrl">The server's base URL; endpoint paths are resolved relative to it.</param>
    /// <param name="accessToken">The bearer token of the connected account.</param>
    /// <param name="cancellationToken">Cancels the HTTP request.</param>
    /// <returns>Concurrency, payload, duration and redirect limits, allowed protocols and operator warnings.</returns>
    /// <exception cref="ServerConnectionException">The server answered with a non-success status.</exception>
    Task<ProxyPolicyResponse> GetPolicyAsync(
        Uri serverBaseUrl, string accessToken, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IProxyClient"/> implementation backed by <see cref="HttpClient"/>.</summary>
public sealed class HttpProxyClient(HttpClient httpClient) : IProxyClient
{
    /// <inheritdoc />
    public async Task<ProxyHttpResponse> ExecuteHttpRequestAsync(
        Uri serverBaseUrl, string accessToken, ProxyHttpRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = new(HttpMethod.Post, new Uri(serverBaseUrl, "api/proxy/http"))
        {
            Content = JsonContent.Create(request, ProxyContractsJsonContext.Default.ProxyHttpRequest)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            ProxyErrorResponse? error = await TryReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
            throw new ServerConnectionException(
                error is null
                    ? $"The proxy request failed ({(int)response.StatusCode} {response.ReasonPhrase})."
                    : $"The proxy request failed: {error.ErrorCode} — {error.Message}");
        }

        return (await response.Content.ReadFromJsonAsync(
            ProxyContractsJsonContext.Default.ProxyHttpResponse, cancellationToken).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<ProxyTcpProbeResponse> ExecuteTcpProbeAsync(
        Uri serverBaseUrl, string accessToken, ProxyTcpProbeRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = new(HttpMethod.Post, new Uri(serverBaseUrl, "api/proxy/tcp-probe"))
        {
            Content = JsonContent.Create(request, ProxyContractsJsonContext.Default.ProxyTcpProbeRequest)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            ProxyErrorResponse? error = await TryReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
            throw new ServerConnectionException(
                error is null
                    ? $"The TCP probe request failed ({(int)response.StatusCode} {response.ReasonPhrase})."
                    : $"The TCP probe request failed: {error.ErrorCode} — {error.Message}");
        }

        return (await response.Content.ReadFromJsonAsync(
            ProxyContractsJsonContext.Default.ProxyTcpProbeResponse, cancellationToken).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<ProxyPolicyResponse> GetPolicyAsync(
        Uri serverBaseUrl, string accessToken, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = new(HttpMethod.Get, new Uri(serverBaseUrl, "api/proxy/policy"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ServerConnectionException(
                $"Reading the proxy policy failed ({(int)response.StatusCode} {response.ReasonPhrase}).");
        }

        return (await response.Content.ReadFromJsonAsync(
            ProxyContractsJsonContext.Default.ProxyPolicyResponse, cancellationToken).ConfigureAwait(false))!;
    }

    private static async Task<ProxyErrorResponse?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync(
                ProxyContractsJsonContext.Default.ProxyErrorResponse, cancellationToken).ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
