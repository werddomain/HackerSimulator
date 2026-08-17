using HackerOs.Simulation.Abstractions.Diagnostics;

namespace HackerOs.Ecosystem;

/// <summary>Records host failures with a user-safe correlation ID and redacted structured metadata.</summary>
public sealed class HostExceptionReporter
{
    private readonly IDiagnosticSink _sink;
    private readonly IPersistentDiagnosticRepository _persistentRepository;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes the reporter over volatile and durable diagnostic boundaries.</summary>
    public HostExceptionReporter(
        IDiagnosticSink sink,
        IPersistentDiagnosticRepository persistentRepository,
        TimeProvider timeProvider)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _persistentRepository = persistentRepository
            ?? throw new ArgumentNullException(nameof(persistentRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>Records one failure without persisting its potentially sensitive message or stack trace.</summary>
    public async ValueTask<Guid> ReportAsync(
        Exception exception,
        string phase,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);

        Guid correlationId = Guid.NewGuid();
        DiagnosticEntry entry = new(
            _timeProvider.GetUtcNow(),
            DiagnosticSeverity.Critical,
            "ecosystem-host",
            "An unexpected host failure interrupted the current surface.",
            correlationId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name,
                ["phase"] = phase
            });

        _sink.Record(entry);
        try
        {
            await _persistentRepository.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception persistenceException) when (persistenceException is not OperationCanceledException)
        {
            _sink.Record(new DiagnosticEntry(
                _timeProvider.GetUtcNow(),
                DiagnosticSeverity.Warning,
                "ecosystem-host",
                "The host failure could not be persisted.",
                correlationId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["exceptionType"] = persistenceException.GetType().FullName
                        ?? persistenceException.GetType().Name,
                    ["phase"] = "diagnostic-persistence"
                }));
        }

        return correlationId;
    }
}