using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Tests.Support;

/// <summary>Scriptable <see cref="IAppIntentGateway"/> double for command/app tests.</summary>
public sealed class FakeAppIntentGateway : IAppIntentGateway
{
    private readonly Dictionary<string, AppIntentLaunchResult> _results = new(StringComparer.Ordinal);
    private readonly HashSet<string> _denied = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AppIntentOpenFileResult> _openResults = new(StringComparer.Ordinal);

    /// <summary>Every launch request received, in call order.</summary>
    public List<(string AppId, IReadOnlyList<string> Arguments)> Requests { get; } = [];

    /// <summary>Every open-file request received, in call order.</summary>
    public List<VirtualPath> OpenFileRequests { get; } = [];

    public FakeAppIntentGateway WithResult(string appId, AppIntentLaunchResult result)
    {
        _results[appId] = result;
        return this;
    }

    public FakeAppIntentGateway WithOpenResult(string path, AppIntentOpenFileResult result)
    {
        _openResults[VirtualPath.Parse(path).Value] = result;
        return this;
    }

    public FakeAppIntentGateway WithCapabilityDenied(string appId)
    {
        _denied.Add(appId);
        return this;
    }

    public ValueTask<AppIntentLaunchResult> LaunchAsync(
        string appId, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        Requests.Add((appId, arguments));

        if (_denied.Contains(appId))
        {
            throw new AppGatewayAccessDeniedException("apps.launch", CapabilityPolicyEvaluation.DenyMissing(1));
        }

        AppIntentLaunchResult result = _results.TryGetValue(appId, out AppIntentLaunchResult? scripted)
            ? scripted
            : new AppIntentLaunchResult(AppIntentLaunchOutcome.Launched);
        return ValueTask.FromResult(result);
    }

    public ValueTask<AppIntentOpenFileResult> OpenFileAsync(
        VirtualPath path, CancellationToken cancellationToken = default)
    {
        OpenFileRequests.Add(path);

        AppIntentOpenFileResult result = _openResults.TryGetValue(path.Value, out AppIntentOpenFileResult? scripted)
            ? scripted
            : new AppIntentOpenFileResult(AppIntentOpenFileOutcome.Opened);
        return ValueTask.FromResult(result);
    }
}
