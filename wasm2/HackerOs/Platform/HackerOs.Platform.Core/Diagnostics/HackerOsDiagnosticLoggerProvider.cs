using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Time;
using Microsoft.Extensions.Logging;

namespace HackerOs.Platform.Core.Diagnostics;

/// <summary>
/// Bridges the standard <see cref="Microsoft.Extensions.Logging"/> pipeline into the HackerOS
/// diagnostic sink: any class that takes an injected <c>ILogger&lt;T&gt;</c> automatically logs into
/// <see cref="IDiagnosticSink"/> (visible live in Error Log Viewer) and, best-effort, into
/// <see cref="IPersistentDiagnosticRepository"/> (durable, IndexedDB-backed).
/// </summary>
public sealed class HackerOsDiagnosticLoggerProvider : ILoggerProvider
{
    private readonly IDiagnosticSink _sink;
    private readonly IPersistentDiagnosticRepository _repository;
    private readonly ISimulationClock _clock;

    /// <summary>Initializes a provider bound to the process-wide diagnostic sink and repository.</summary>
    public HackerOsDiagnosticLoggerProvider(
        IDiagnosticSink sink,
        IPersistentDiagnosticRepository repository,
        ISimulationClock clock)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) =>
        new HackerOsDiagnosticLogger(_sink, _repository, _clock, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

/// <summary>
/// One category-scoped <see cref="ILogger"/> that records into <see cref="IDiagnosticSink"/>
/// synchronously and persists to <see cref="IPersistentDiagnosticRepository"/> best-effort,
/// mirroring the fallback discipline used by <c>HostExceptionReporter</c>.
/// </summary>
internal sealed class HackerOsDiagnosticLogger : ILogger
{
    private readonly IDiagnosticSink _sink;
    private readonly IPersistentDiagnosticRepository _repository;
    private readonly ISimulationClock _clock;
    private readonly string _category;

    public HackerOsDiagnosticLogger(
        IDiagnosticSink sink, IPersistentDiagnosticRepository repository, ISimulationClock clock, string category)
    {
        _sink = sink;
        _repository = repository;
        _clock = clock;
        _category = category;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        DiagnosticSeverity severity = ToSeverity(logLevel);
        string message = formatter(state, exception);

        Dictionary<string, string>? properties = null;
        if (exception is not null)
        {
            properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name
            };
        }

        DiagnosticEntry entry = new(_clock.UtcNow, severity, _category, message, Guid.NewGuid(), properties);
        _sink.Record(entry);
        _ = PersistAsync(entry);
    }

    private async Task PersistAsync(DiagnosticEntry entry)
    {
        try
        {
            await _repository.AppendAsync(entry).ConfigureAwait(false);
        }
        catch (Exception persistenceException) when (persistenceException is not OperationCanceledException)
        {
            _sink.Record(new DiagnosticEntry(
                _clock.UtcNow,
                DiagnosticSeverity.Warning,
                _category,
                "A log entry could not be persisted.",
                entry.CorrelationId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["exceptionType"] = persistenceException.GetType().FullName ?? persistenceException.GetType().Name
                }));
        }
    }

    private static DiagnosticSeverity ToSeverity(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => DiagnosticSeverity.Trace,
        LogLevel.Debug => DiagnosticSeverity.Debug,
        LogLevel.Information => DiagnosticSeverity.Information,
        LogLevel.Warning => DiagnosticSeverity.Warning,
        LogLevel.Error => DiagnosticSeverity.Error,
        LogLevel.Critical => DiagnosticSeverity.Critical,
        _ => DiagnosticSeverity.Information
    };

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();
        public void Dispose()
        {
        }
    }
}
