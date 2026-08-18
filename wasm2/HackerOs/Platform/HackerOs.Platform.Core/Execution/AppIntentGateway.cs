using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Platform.Core.Intents;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Trusted <see cref="IAppIntentGateway"/> implementation that routes launch requests through
/// the real <see cref="AppIntentDispatcher"/>, so any app can request another app be started the
/// same way the desktop shell and terminal already do, instead of only components with direct
/// Blazor DI access to the dispatcher.
/// </summary>
internal sealed class AppIntentGateway(
    Func<AppIntentDispatcher> dispatcherProvider,
    string callerAppId,
    string userId,
    AuthenticatedPrincipal principal) : IAppIntentGateway
{
    public async ValueTask<AppIntentLaunchResult> LaunchAsync(
        string appId, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentNullException.ThrowIfNull(arguments);

        AppIntentDispatcher dispatcher = dispatcherProvider();
        AppIntentDispatchResult result = await dispatcher.DispatchAsync(
            new AppIntentRequest(Guid.NewGuid(), callerAppId, userId, new LaunchAppIntent(appId, arguments)),
            principal,
            fullScreen: null,
            cancellationToken);

        return result.Status switch
        {
            AppIntentDispatchStatus.Dispatched =>
                new AppIntentLaunchResult(AppIntentLaunchOutcome.Launched),
            AppIntentDispatchStatus.NotFound =>
                new AppIntentLaunchResult(AppIntentLaunchOutcome.NotFound, result.ErrorCode),
            AppIntentDispatchStatus.Disabled =>
                new AppIntentLaunchResult(AppIntentLaunchOutcome.Disabled, result.ErrorCode),
            AppIntentDispatchStatus.CapabilityDenied =>
                throw new AppGatewayAccessDeniedException(
                    AppCapabilities.AppsLaunch, CapabilityPolicyEvaluation.DenyMissing(1)),
            _ => new AppIntentLaunchResult(AppIntentLaunchOutcome.Faulted, result.ErrorCode)
        };
    }

    public async ValueTask<AppIntentOpenFileResult> OpenFileAsync(
        VirtualPath path, string? mediaType = null, CancellationToken cancellationToken = default)
    {
        AppIntentDispatcher dispatcher = dispatcherProvider();
        AppIntentDispatchResult result = await dispatcher.DispatchAsync(
            new AppIntentRequest(
                Guid.NewGuid(), callerAppId, userId, new OpenFileIntent(path, FileIntentAction.Open, mediaType)),
            principal,
            fullScreen: null,
            cancellationToken);

        return result.Status switch
        {
            AppIntentDispatchStatus.Dispatched =>
                new AppIntentOpenFileResult(AppIntentOpenFileOutcome.Opened),
            AppIntentDispatchStatus.ChooserRequired =>
                new AppIntentOpenFileResult(AppIntentOpenFileOutcome.ChooserRequired, result.CandidateAppIds),
            AppIntentDispatchStatus.NotFound =>
                new AppIntentOpenFileResult(AppIntentOpenFileOutcome.NoHandler, ErrorCode: result.ErrorCode),
            AppIntentDispatchStatus.TargetInvalid =>
                new AppIntentOpenFileResult(AppIntentOpenFileOutcome.NoHandler, ErrorCode: result.ErrorCode),
            AppIntentDispatchStatus.CapabilityDenied =>
                throw new AppGatewayAccessDeniedException(
                    AppCapabilities.AppsLaunch, CapabilityPolicyEvaluation.DenyMissing(1)),
            _ => new AppIntentOpenFileResult(AppIntentOpenFileOutcome.Faulted, ErrorCode: result.ErrorCode)
        };
    }
}
