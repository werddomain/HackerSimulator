using System.Security.Claims;
using HackerOs.Server.Contracts.Proxy;
using HackerOs.Server.Services;

namespace HackerOs.Server.Endpoints;

/// <summary>
/// Proxy endpoints — P5-PROXY-001 through P5-PROXY-007.
/// All endpoints require authentication.
///
/// POST /api/proxy/http      → execute an HTTP proxy request
/// POST /api/proxy/tcp-probe → execute a single-port TCP reachability probe (ADR 0035)
/// GET  /api/proxy/policy    → read current device proxy policy
/// </summary>
public static class ProxyEndpoints
{
    /// <summary>Registers proxy route groups on the application.</summary>
    public static IEndpointRouteBuilder MapProxyEndpoints(this IEndpointRouteBuilder app)
    {
        var proxyGroup = app.MapGroup("/api/proxy").WithTags("Proxy").RequireAuthorization();

        proxyGroup.MapPost("/http", ExecuteHttpAsync).WithName("ProxyHttp")
            .WithSummary("Executes a server-validated HTTP proxy request.");
        proxyGroup.MapPost("/tcp-probe", ExecuteTcpProbeAsync).WithName("ProxyTcpProbe")
            .WithSummary("Executes a server-validated single-port TCP reachability probe.");
        proxyGroup.MapGet("/policy", GetPolicyAsync).WithName("GetProxyPolicy")
            .WithSummary("Returns current proxy quotas and policy for the authenticated device.");

        return app;
    }

    private static async Task<IResult> ExecuteHttpAsync(
        ProxyHttpRequest request, IProxyService proxy, ClaimsPrincipal user, CancellationToken ct)
    {
        var accountId = GetAccountId(user);
        var deviceId = GetDeviceId(user);

        try
        {
            var response = await proxy.ExecuteHttpRequestAsync(accountId, deviceId, request, ct);
            return Results.Json(response, ProxyContractsJsonContext.Default.ProxyHttpResponse);
        }
        catch (ProxyRequestException ex)
        {
            var error = new ProxyErrorResponse(
                request.RequestId,
                ex.ErrorCode,
                ex.Message,
                Guid.NewGuid());

            // Map error codes to appropriate HTTP status codes.
            var statusCode = ex.ErrorCode switch
            {
                ProxyErrorCode.CapabilityDenied => StatusCodes.Status403Forbidden,
                ProxyErrorCode.QuotaExceeded => StatusCodes.Status429TooManyRequests,
                ProxyErrorCode.Timeout => StatusCodes.Status504GatewayTimeout,
                ProxyErrorCode.PayloadTooLarge => StatusCodes.Status502BadGateway,
                _ => StatusCodes.Status400BadRequest
            };

            return Results.Json(error, ProxyContractsJsonContext.Default.ProxyErrorResponse, statusCode: statusCode);
        }
        catch (OperationCanceledException)
        {
            var error = new ProxyErrorResponse(
                request.RequestId, ProxyErrorCode.Timeout,
                "The request was cancelled.", Guid.NewGuid());
            return Results.Json(error, ProxyContractsJsonContext.Default.ProxyErrorResponse,
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }

    private static async Task<IResult> ExecuteTcpProbeAsync(
        ProxyTcpProbeRequest request, IProxyService proxy, ClaimsPrincipal user, CancellationToken ct)
    {
        var accountId = GetAccountId(user);
        var deviceId = GetDeviceId(user);

        try
        {
            var response = await proxy.ExecuteTcpProbeAsync(accountId, deviceId, request, ct);
            return Results.Json(response, ProxyContractsJsonContext.Default.ProxyTcpProbeResponse);
        }
        catch (ProxyRequestException ex)
        {
            var error = new ProxyErrorResponse(
                request.RequestId,
                ex.ErrorCode,
                ex.Message,
                Guid.NewGuid());

            var statusCode = ex.ErrorCode switch
            {
                ProxyErrorCode.CapabilityDenied => StatusCodes.Status403Forbidden,
                ProxyErrorCode.QuotaExceeded => StatusCodes.Status429TooManyRequests,
                ProxyErrorCode.Timeout => StatusCodes.Status504GatewayTimeout,
                _ => StatusCodes.Status400BadRequest
            };

            return Results.Json(error, ProxyContractsJsonContext.Default.ProxyErrorResponse, statusCode: statusCode);
        }
        catch (OperationCanceledException)
        {
            var error = new ProxyErrorResponse(
                request.RequestId, ProxyErrorCode.Timeout,
                "The request was cancelled.", Guid.NewGuid());
            return Results.Json(error, ProxyContractsJsonContext.Default.ProxyErrorResponse,
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }

    private static async Task<IResult> GetPolicyAsync(
        IProxyService proxy, ClaimsPrincipal user, CancellationToken ct)
    {
        var deviceId = GetDeviceId(user);
        var policy = await proxy.GetPolicyAsync(deviceId, ct);
        return Results.Json(policy, ProxyContractsJsonContext.Default.ProxyPolicyResponse);
    }

    private static Guid GetAccountId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static Guid GetDeviceId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("device_id")!);
}
