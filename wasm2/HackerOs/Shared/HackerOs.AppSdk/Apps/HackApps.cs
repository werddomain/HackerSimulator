using HackerOs.App.Abstractions;
using HackerOs.AppSdk.IO;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.AppSdk.Apps;

/// <summary>
/// The catalog of installed apps, and launching them (ADR 0056, section 2). Replaces
/// <c>AppCatalog</c> and <c>IAppEnablementRegistry</c>, which apps may no longer inject (ADR 0055).
/// </summary>
/// <remarks>
/// Reading needs no capability; <see cref="LaunchAsync"/> and <see cref="OpenFileAsync"/> need
/// <c>apps.launch</c>. Every member targets the app running the current code (ambient context of
/// ADR 0043).
/// </remarks>
public static class HackApps
{
    /// <summary>
    /// Raised after an app was installed, uninstalled or reinstalled. Subscribing and unsubscribing
    /// both need the app's ambient scope; a component that unsubscribes in <c>Dispose</c> should use
    /// <c>AppContext.Catalog.Changed</c> instead.
    /// </summary>
    /// <exception cref="HackExecutionContextMissingException">No app context is active.</exception>
    public static event EventHandler? Changed
    {
        add => Gateway.Changed += value;
        remove => Gateway.Changed -= value;
    }

    /// <summary>Gets every app installed for the current user, sorted by name.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="HackExecutionContextMissingException">No app context is active.</exception>
    public static async Task<IReadOnlyList<HackAppInfo>> GetInstalledAsync(CancellationToken cancellationToken = default) =>
        await Gateway.GetInstalledAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Gets the installed apps that can open <paramref name="path"/>, the configured default first.</summary>
    /// <param name="path">Absolute, relative or <c>~</c>-prefixed path; the file need not exist.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">The path is empty or malformed.</exception>
    /// <exception cref="HackExecutionContextMissingException">No app context is active.</exception>
    public static async Task<IReadOnlyList<HackAppInfo>> GetHandlersAsync(string path, CancellationToken cancellationToken = default)
    {
        VirtualPath target = HackPath.Resolve(path, nameof(path));
        return await Gateway.GetHandlersAsync(target, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Starts an installed app with arguments, like running it from the launcher.</summary>
    /// <param name="appId">Exact app ID.</param>
    /// <param name="arguments">Arguments passed to the new process (file paths for a window app).</param>
    /// <param name="cancellationToken">Cancels the launch.</param>
    /// <exception cref="HackAppLaunchException">The app is unknown, uninstalled, or failed to start.</exception>
    /// <exception cref="HackUnauthorizedAccessException">This app lacks <c>apps.launch</c>.</exception>
    /// <exception cref="HackExecutionContextMissingException">No app context is active.</exception>
    public static async Task LaunchAsync(
        string appId, IReadOnlyList<string>? arguments = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        IAppIntentGateway intents = HackExecutionContext.Current.Intents;
        AppIntentLaunchResult result;
        try
        {
            result = await intents.LaunchAsync(appId, arguments ?? [], cancellationToken).ConfigureAwait(false);
        }
        catch (AppGatewayAccessDeniedException exception)
        {
            throw HackGatewayErrors.ToUnauthorized(exception);
        }

        switch (result.Outcome)
        {
            case AppIntentLaunchOutcome.Launched:
                return;
            case AppIntentLaunchOutcome.NotFound:
                throw new HackAppLaunchException($"No app '{appId}' is installed.", appId, result.ErrorCode);
            case AppIntentLaunchOutcome.Disabled:
                throw new HackAppLaunchException($"The app '{appId}' is uninstalled.", appId, result.ErrorCode);
            default:
                throw new HackAppLaunchException($"The app '{appId}' could not be started ({result.ErrorCode}).", appId, result.ErrorCode);
        }
    }

    /// <summary>
    /// Opens a file with its handler, like double-clicking it: the configured default, or the only
    /// app that can open it. When several can and none is the default, nothing starts and the
    /// result lists them so the caller can ask the user.
    /// </summary>
    /// <param name="path">Absolute, relative or <c>~</c>-prefixed path of the file.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">The path is empty or malformed.</exception>
    /// <exception cref="HackAppLaunchException">The handler failed to start.</exception>
    /// <exception cref="HackUnauthorizedAccessException">This app lacks <c>apps.launch</c>.</exception>
    /// <exception cref="HackExecutionContextMissingException">No app context is active.</exception>
    public static async Task<HackOpenFileResult> OpenFileAsync(string path, CancellationToken cancellationToken = default)
    {
        VirtualPath target = HackPath.Resolve(path, nameof(path));
        IAppIntentGateway intents = HackExecutionContext.Current.Intents;
        AppIntentOpenFileResult result;
        try
        {
            result = await intents.OpenFileAsync(target, null, cancellationToken).ConfigureAwait(false);
        }
        catch (AppGatewayAccessDeniedException exception)
        {
            throw HackGatewayErrors.ToUnauthorized(exception);
        }

        return result.Outcome switch
        {
            AppIntentOpenFileOutcome.Opened => new HackOpenFileResult(HackOpenFileStatus.Opened, []),
            AppIntentOpenFileOutcome.ChooserRequired => new HackOpenFileResult(HackOpenFileStatus.ChooserRequired, result.CandidateAppIds ?? []),
            AppIntentOpenFileOutcome.NoHandler => new HackOpenFileResult(HackOpenFileStatus.NoHandler, []),
            _ => throw new HackAppLaunchException($"'{target.Value}' could not be opened ({result.ErrorCode}).", errorCode: result.ErrorCode)
        };
    }

    private static IAppCatalogGateway Gateway => HackExecutionContext.Current.Catalog;
}
