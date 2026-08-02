using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Intents;

/// <summary>Identifies the stable outcome of dispatching one app intent.</summary>
public enum AppIntentDispatchStatus
{
    /// <summary>The intent was authorized and carried out (or acknowledged, for deferred UI intents).</summary>
    Dispatched,

    /// <summary>The calling app lacks the capability required to carry out this intent.</summary>
    CapabilityDenied,

    /// <summary>No catalog app matches the intent's target.</summary>
    NotFound,

    /// <summary>The resolved target app is currently disabled.</summary>
    Disabled,

    /// <summary>Multiple candidate apps can handle an <see cref="OpenFileIntent"/>; the caller must choose.</summary>
    ChooserRequired,

    /// <summary>An <see cref="OpenFileIntent"/>'s explicit preferred app is invalid.</summary>
    TargetInvalid,

    /// <summary>The resolved target's entry point faulted while starting or executing.</summary>
    EntryPointFault
}

/// <summary>Contains the result of dispatching one <see cref="IAppIntent"/>, per `P1-APP-007`.</summary>
/// <param name="Status">Stable dispatch outcome.</param>
/// <param name="Process">Process created for the resolved target, when one was launched.</param>
/// <param name="Context">Execution context for the resolved target, when one was launched.</param>
/// <param name="ExitCode">Exit code for a completed <see cref="ExecuteCommandIntent"/> or Terminal <see cref="OpenFileIntent"/>.</param>
/// <param name="StandardOutput">Captured standard output for a completed Terminal launch.</param>
/// <param name="StandardError">Captured standard error for a completed Terminal launch.</param>
/// <param name="CandidateAppIds">Candidate app IDs, populated for <see cref="AppIntentDispatchStatus.ChooserRequired"/>.</param>
/// <param name="ErrorCode">Stable machine-readable error code when dispatch did not succeed.</param>
public sealed record AppIntentDispatchResult(
    AppIntentDispatchStatus Status,
    ProcessRecord? Process = null,
    IAppExecutionContext? Context = null,
    int? ExitCode = null,
    string? StandardOutput = null,
    string? StandardError = null,
    IReadOnlyList<string>? CandidateAppIds = null,
    string? ErrorCode = null);

/// <summary>
/// Capability-gates and dispatches every typed <see cref="IAppIntent"/> to the lifecycle
/// orchestrator or file-association resolver, per `P1-APP-007`. This is the policy layer:
/// it decides whether a request is allowed before <see cref="AppLifecycleOrchestrator"/> (the
/// mechanism layer) carries it out.
/// </summary>
public sealed class AppIntentDispatcher
{
    private readonly AppLifecycleOrchestrator _orchestrator;
    private readonly AppCatalog _catalog;
    private readonly IAppEnablementRegistry _enablement;
    private readonly FileAssociationResolver _associations;
    private readonly ICapabilityGrantRepository _grants;

    /// <summary>Initializes the dispatcher over the orchestrator, catalog, resolver, and grant repository.</summary>
    public AppIntentDispatcher(
        AppLifecycleOrchestrator orchestrator,
        AppCatalog catalog,
        IAppEnablementRegistry enablement,
        FileAssociationResolver associations,
        ICapabilityGrantRepository grants)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _enablement = enablement ?? throw new ArgumentNullException(nameof(enablement));
        _associations = associations ?? throw new ArgumentNullException(nameof(associations));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
    }

    /// <summary>Dispatches one intent request on behalf of an authenticated principal.</summary>
    /// <param name="request">Caller app, acting user, and typed intent.</param>
    /// <param name="principal">Authenticated principal the request runs as. Must match <paramref name="request"/>'s user.</param>
    public async ValueTask<AppIntentDispatchResult> DispatchAsync(AppIntentRequest request, AuthenticatedPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(principal);
        if (!string.Equals(request.UserId, principal.UserId.ToString(), StringComparison.Ordinal))
        {
            throw new ArgumentException("The intent request's user must match the acting principal.", nameof(request));
        }

        return request.Intent switch
        {
            LaunchAppIntent launch => await DispatchLaunchAsync(request, launch, principal),
            OpenFileIntent openFile => await DispatchOpenFileAsync(request, openFile, principal),
            ExecuteCommandIntent execute => await DispatchExecuteCommandAsync(request, execute, principal),
            RevealFileIntent => new AppIntentDispatchResult(AppIntentDispatchStatus.Dispatched),
            ShowAppSettingsIntent showSettings => DispatchShowSettings(showSettings),
            _ => new AppIntentDispatchResult(AppIntentDispatchStatus.NotFound, ErrorCode: "intent.unknown")
        };
    }

    private bool HasAppsLaunch(string callerAppId, string userId, AppAuthority actingAuthority) =>
        _grants.Evaluate(callerAppId, userId, AppCapabilities.AppsLaunch, actingAuthority, AppAuthority.User).Granted;

    private async ValueTask<AppIntentDispatchResult> DispatchLaunchAsync(
        AppIntentRequest request, LaunchAppIntent intent, AuthenticatedPrincipal principal)
    {
        if (!string.Equals(request.CallerAppId, intent.TargetAppId, StringComparison.Ordinal)
            && !HasAppsLaunch(request.CallerAppId, request.UserId, principal.Authority))
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.CapabilityDenied, ErrorCode: "intent.capability-denied");
        }

        AppLaunchResult launch = await _orchestrator.LaunchAsync(new AppLaunchRequest(intent.TargetAppId, principal, intent.Arguments));
        return ToDispatchResult(launch);
    }

    private async ValueTask<AppIntentDispatchResult> DispatchOpenFileAsync(
        AppIntentRequest request, OpenFileIntent intent, AuthenticatedPrincipal principal)
    {
        if (!HasAppsLaunch(request.CallerAppId, request.UserId, principal.Authority))
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.CapabilityDenied, ErrorCode: "intent.capability-denied");
        }

        AppOperationContext readContext = new()
        {
            AppId = request.CallerAppId,
            UserId = request.UserId,
            UserAuthority = principal.Authority,
            GrantedCapabilities = new HashSet<string>(StringComparer.Ordinal) { AppCapabilities.FileAssociationsRead },
            IsSystemOperation = false
        };

        FileHandlerResolution resolution = await _associations.ResolveAsync(intent, readContext);
        switch (resolution.Status)
        {
            case FileHandlerResolutionStatus.ChooserRequired:
                return new AppIntentDispatchResult(AppIntentDispatchStatus.ChooserRequired, CandidateAppIds: resolution.CandidateAppIds, ErrorCode: "intent.open-file.chooser-required");
            case FileHandlerResolutionStatus.NoHandler:
                return new AppIntentDispatchResult(AppIntentDispatchStatus.NotFound, ErrorCode: "intent.open-file.no-handler");
            case FileHandlerResolutionStatus.TargetInvalid:
                return new AppIntentDispatchResult(AppIntentDispatchStatus.TargetInvalid, ErrorCode: "intent.open-file.target-invalid");
        }

        AppLaunchResult launch = await _orchestrator.LaunchAsync(
            new AppLaunchRequest(resolution.AppId!, principal, [intent.Path.Value]));
        return ToDispatchResult(launch);
    }

    private async ValueTask<AppIntentDispatchResult> DispatchExecuteCommandAsync(
        AppIntentRequest request, ExecuteCommandIntent intent, AuthenticatedPrincipal principal)
    {
        if (!HasAppsLaunch(request.CallerAppId, request.UserId, principal.Authority))
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.CapabilityDenied, ErrorCode: "intent.capability-denied");
        }

        string[] tokens = intent.CommandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.NotFound, ErrorCode: "intent.execute-command.empty");
        }

        string commandName = tokens[0];
        string[] arguments = tokens[1..];

        AppManifest? target = _catalog.Manifests.Values.FirstOrDefault(manifest =>
            manifest.Kind == AppKind.Terminal
            && manifest.Terminal is not null
            && (string.Equals(manifest.Terminal.Name, commandName, StringComparison.Ordinal)
                || manifest.Terminal.Aliases.Contains(commandName, StringComparer.Ordinal)));

        if (target is null)
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.NotFound, ErrorCode: "intent.execute-command.not-found");
        }

        if (!_enablement.IsEnabled(target.Id))
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.Disabled, ErrorCode: "intent.execute-command.disabled");
        }

        AppLaunchResult launch = await _orchestrator.LaunchAsync(new AppLaunchRequest(target.Id, principal, arguments));
        return ToDispatchResult(launch);
    }

    private AppIntentDispatchResult DispatchShowSettings(ShowAppSettingsIntent intent)
    {
        if (!_catalog.Manifests.ContainsKey(intent.TargetAppId))
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.NotFound, ErrorCode: "intent.show-settings.not-found");
        }

        if (!_enablement.IsEnabled(intent.TargetAppId))
        {
            return new AppIntentDispatchResult(AppIntentDispatchStatus.Disabled, ErrorCode: "intent.show-settings.disabled");
        }

        // Actually focusing/rendering a settings surface belongs to the future window runtime
        // (Phase 2A); Section 8 only validates the target and acknowledges the request.
        return new AppIntentDispatchResult(AppIntentDispatchStatus.Dispatched);
    }

    private static AppIntentDispatchResult ToDispatchResult(AppLaunchResult launch) => launch.Status switch
    {
        AppLaunchStatus.Launched or AppLaunchStatus.FocusedExisting => new AppIntentDispatchResult(
            AppIntentDispatchStatus.Dispatched, launch.Process, launch.Context, launch.ExitCode, launch.StandardOutput, launch.StandardError),
        AppLaunchStatus.NotFound => new AppIntentDispatchResult(AppIntentDispatchStatus.NotFound, ErrorCode: launch.ErrorCode),
        AppLaunchStatus.Disabled => new AppIntentDispatchResult(AppIntentDispatchStatus.Disabled, ErrorCode: launch.ErrorCode),
        _ => new AppIntentDispatchResult(AppIntentDispatchStatus.EntryPointFault, launch.Process, launch.Context, ErrorCode: launch.ErrorCode)
    };
}
