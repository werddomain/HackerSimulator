using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Gateways;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Provides one app instance's authorized read access to system diagnostic log entries,
/// requiring <see cref="AppCapabilities.DiagnosticsRead"/>.
/// </summary>
public sealed class AppDiagnosticsGateway : IAppDiagnosticsGateway
{
    private readonly IDiagnosticSink _sink;
    private readonly ICapabilityChecker _capabilities;

    /// <summary>Initializes a diagnostics-read gateway bound to one app.</summary>
    public AppDiagnosticsGateway(IDiagnosticSink sink, ICapabilityChecker capabilities)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    }

    /// <inheritdoc />
    public IReadOnlyList<DiagnosticEntry> Entries
    {
        get
        {
            _capabilities.Require(AppCapabilities.DiagnosticsRead);
            return _sink.Entries;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        _capabilities.Require(AppCapabilities.DiagnosticsClear);
        _sink.Clear();
    }
}
