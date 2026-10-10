using HackerOs.Platform.Core.Diagnostics;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Time;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Provides one app instance's structured diagnostic logging, stamping every entry with the
/// app's own ID as source, its PID, and a fresh per-call correlation ID. Entries go to
/// <c>/var/log/apps/&lt;appId&gt;.log</c> through <see cref="LogFileWriter"/> (ADR 0045) and to the
/// in-memory buffer of recent entries. The app itself never writes under <c>/var/log</c>.
/// </summary>
public sealed class AppLoggingGateway : IAppLoggingGateway
{
    private readonly IDiagnosticSink _sink;
    private readonly ISimulationClock _clock;
    private readonly string _appId;
    private readonly LogFileWriter? _writer;
    private readonly LogFileTarget? _target;
    private readonly long? _processId;
    private readonly IDiagnosticRedactor _redactor;

    /// <summary>Initializes a logging gateway bound to one app.</summary>
    /// <param name="sink">In-memory buffer of recent entries.</param>
    /// <param name="clock">Simulation clock stamping every entry.</param>
    /// <param name="appId">App identifier, used as source and as log file name.</param>
    /// <param name="writer">
    /// Platform log writer, or <see langword="null"/> to keep entries in memory only (headless tests).
    /// </param>
    /// <param name="processId">PID written in each line, when the hosting process is known.</param>
    /// <param name="redactor">Redactor applied to properties before they reach the file.</param>
    public AppLoggingGateway(
        IDiagnosticSink sink,
        ISimulationClock clock,
        string appId,
        LogFileWriter? writer = null,
        long? processId = null,
        IDiagnosticRedactor? redactor = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        _appId = appId;
        _writer = writer;
        _target = writer is null ? null : LogFileTarget.ForApp(appId);
        _processId = processId;
        _redactor = redactor ?? new SensitiveKeyDiagnosticRedactor();
    }

    /// <inheritdoc />
    public void Log(DiagnosticSeverity severity, string message, IReadOnlyDictionary<string, string>? properties = null)
    {
        DiagnosticEntry entry = new(_clock.UtcNow, severity, _appId, message, Guid.NewGuid(), properties);
        _sink.Record(entry);
        if (_writer is not null)
        {
            _writer.Enqueue(_target!, LogLineFormat.Format(DiagnosticRedaction.Apply(entry, _redactor), _processId));
        }
    }
}
