using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Commands.Launch;

/// <summary>
/// Implements the <c>launch</c> terminal command (`P4-W5-CMD-005`).
/// Launches another application window or service by app ID.
/// </summary>
public sealed class LaunchCommand : TerminalAppBase
{
    public LaunchCommand(AppManifest manifest) : base(manifest) { }

    public override async ValueTask<int> ExecuteAsync(
        TerminalExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Arguments.Count == 0)
        {
            context.StandardError.WriteLine("launch: missing app-id argument");
            return 1;
        }

        string appId = context.Arguments[0];
        IReadOnlyList<string> launchArguments = context.Arguments.Skip(1).ToList();

        AppIntentLaunchResult result;
        try
        {
            result = await context.App.Intents.LaunchAsync(appId, launchArguments, cancellationToken);
        }
        catch (AppGatewayAccessDeniedException exception)
        {
            context.StandardError.WriteLine($"launch: {exception.Message}");
            return 1;
        }

        switch (result.Outcome)
        {
            case AppIntentLaunchOutcome.Launched:
                context.StandardOutput.WriteLine($"Launched '{appId}'.");
                return 0;
            case AppIntentLaunchOutcome.NotFound:
                context.StandardError.WriteLine($"launch: no such application '{appId}'");
                return 1;
            case AppIntentLaunchOutcome.Disabled:
                // ADR 0048: a disabled app is an uninstalled one.
                context.StandardError.WriteLine($"launch: '{appId}' is not installed");
                return 1;
            default:
                context.StandardError.WriteLine($"launch: '{appId}' failed to start ({result.ErrorCode})");
                return 1;
        }
    }
}
