using HackerOs.Ecosystem;
using HackerOs.Platform.Core.Diagnostics;
using HackerOs.Simulation.Abstractions.Diagnostics;

namespace HackerOs.Ecosystem.Tests;

public sealed class HostExceptionReporterTests
{
    [Fact]
    public async Task ReportAsync_records_safe_correlated_volatile_and_persistent_entries()
    {
        BoundedDiagnosticSink sink = new(10);
        RecordingRepository persistent = new();
        HostExceptionReporter reporter = new(sink, persistent, TimeProvider.System);

        Guid correlationId = await reporter.ReportAsync(
            new InvalidOperationException("password=secret"),
            "test-phase");

        DiagnosticEntry entry = Assert.Single(sink.Entries);
        DiagnosticEntry persisted = Assert.Single(persistent.Entries);
        Assert.Equal(correlationId, entry.CorrelationId);
        Assert.Equal(entry.CorrelationId, persisted.CorrelationId);
        Assert.Equal(entry.Category, persisted.Category);
        Assert.Equal(entry.Message, persisted.Message);
        Assert.DoesNotContain("secret", entry.Message, StringComparison.Ordinal);
        Assert.Equal(typeof(InvalidOperationException).FullName, entry.Properties["exceptionType"]);
        Assert.Equal("test-phase", entry.Properties["phase"]);
    }

    [Fact]
    public async Task ReportAsync_retains_volatile_diagnostic_when_persistence_fails()
    {
        BoundedDiagnosticSink sink = new(10);
        HostExceptionReporter reporter = new(
            sink,
            new RecordingRepository { AppendException = new InvalidDataException("storage failure") },
            TimeProvider.System);

        Guid correlationId = await reporter.ReportAsync(new Exception("sensitive"), "render");

        Assert.Equal(2, sink.Entries.Count);
        Assert.All(sink.Entries, entry => Assert.Equal(correlationId, entry.CorrelationId));
        Assert.Equal(DiagnosticSeverity.Warning, sink.Entries[1].Severity);
        Assert.DoesNotContain("storage failure", sink.Entries[1].Message, StringComparison.Ordinal);
    }

    private sealed class RecordingRepository : IPersistentDiagnosticRepository
    {
        internal List<DiagnosticEntry> Entries { get; } = [];
        internal Exception? AppendException { get; init; }

        public ValueTask AppendAsync(DiagnosticEntry entry, CancellationToken cancellationToken = default)
        {
            if (AppendException is not null)
            {
                throw AppendException;
            }

            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<DiagnosticEntry>> ReadAllAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<DiagnosticEntry>>(Entries);
    }
}