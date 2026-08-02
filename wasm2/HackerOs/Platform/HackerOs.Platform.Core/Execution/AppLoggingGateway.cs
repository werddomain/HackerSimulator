using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Time;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Provides one app instance's structured diagnostic logging, stamping every entry with the
/// app's own ID as category and a fresh per-call correlation ID.
/// </summary>
public sealed class AppLoggingGateway : IAppLoggingGateway
{
    private readonly IDiagnosticSink _sink;
    private readonly ISimulationClock _clock;
    private readonly string _appId;

    /// <summary>Initializes a logging gateway bound to one app.</summary>
    public AppLoggingGateway(IDiagnosticSink sink, ISimulationClock clock, string appId)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        _appId = appId;
    }

    /// <inheritdoc />
    public void Log(DiagnosticSeverity severity, string message, IReadOnlyDictionary<string, string>? properties = null) =>
        _sink.Record(new DiagnosticEntry(_clock.UtcNow, severity, _appId, message, Guid.NewGuid(), properties));
}
