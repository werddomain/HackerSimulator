using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Platform.Core.Intents;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Trusted <see cref="IAppServiceControlGateway"/> implementation that routes every request
/// through the real <see cref="AppIntentDispatcher"/>, so any app can start, stop, or reconfigure
/// a companion service (same assembly) or, holding <see cref="AppCapabilities.ServicesManage"/>,
/// any other installed service — instead of only components with direct Blazor DI access to the
/// orchestrator (as System Monitor's process-kill flow still uses).
/// </summary>
internal sealed class AppServiceControlGateway(
    Func<AppIntentDispatcher> dispatcherProvider,
    string callerAppId,
    string userId,
    AuthenticatedPrincipal principal) : IAppServiceControlGateway
{
    public async ValueTask<ServiceControlResult> StartAsync(string serviceAppId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceAppId);
        ServiceControlDispatchResult result = await dispatcherProvider().StartServiceAsync(callerAppId, userId, serviceAppId, principal);
        return ToResult(result);
    }

    public async ValueTask<ServiceControlResult> StopAsync(string serviceAppId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceAppId);
        ServiceControlDispatchResult result = await dispatcherProvider().StopServiceAsync(callerAppId, userId, serviceAppId, principal);
        return ToResult(result);
    }

    public async ValueTask<ServiceStartMode> GetStartModeAsync(string serviceAppId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceAppId);
        (ServiceControlDispatchResult result, ServiceStartMode mode) =
            await dispatcherProvider().GetServiceStartModeAsync(callerAppId, userId, serviceAppId, principal);
        ThrowIfDenied(result);
        return mode;
    }

    public async ValueTask<ServiceControlResult> SetStartModeAsync(
        string serviceAppId, ServiceStartMode mode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceAppId);
        ServiceControlDispatchResult result =
            await dispatcherProvider().SetServiceStartModeAsync(callerAppId, userId, serviceAppId, principal, mode);
        return ToResult(result);
    }

    private static ServiceControlResult ToResult(ServiceControlDispatchResult result)
    {
        ThrowIfDenied(result);
        return result.Status switch
        {
            ServiceControlDispatchStatus.Succeeded => new ServiceControlResult(ServiceControlOutcome.Succeeded),
            ServiceControlDispatchStatus.NotFound => new ServiceControlResult(ServiceControlOutcome.NotFound, result.ErrorCode),
            ServiceControlDispatchStatus.NotAService => new ServiceControlResult(ServiceControlOutcome.NotAService, result.ErrorCode),
            ServiceControlDispatchStatus.ServiceDisabled => new ServiceControlResult(ServiceControlOutcome.ServiceDisabled, result.ErrorCode),
            _ => new ServiceControlResult(ServiceControlOutcome.Faulted, result.ErrorCode)
        };
    }

    private static void ThrowIfDenied(ServiceControlDispatchResult result)
    {
        if (result.Status == ServiceControlDispatchStatus.CapabilityDenied)
        {
            throw new AppGatewayAccessDeniedException(
                AppCapabilities.ServicesManage, CapabilityPolicyEvaluation.DenyMissing(1));
        }
    }
}
