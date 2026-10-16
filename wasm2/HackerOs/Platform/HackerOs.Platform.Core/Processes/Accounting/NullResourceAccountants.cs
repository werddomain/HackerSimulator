using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.Core.Processes.Accounting;

/// <summary>
/// CPU accountant of a host whose CPU measurement is off, at build or in <c>appsettings.json</c>
/// (ADR 0066, section 4): it never measures, so every reader falls back to the
/// <see cref="IResourceSimulator"/> and reports <see cref="ResourceMeasurementSource.Simulated"/>.
/// </summary>
public sealed class NullCpuTimeAccountant : ICpuTimeAccountant
{
    /// <summary>Gets the shared instance (the accountant is stateless).</summary>
    public static NullCpuTimeAccountant Instance { get; } = new();

    private NullCpuTimeAccountant()
    {
    }

    /// <inheritdoc />
    public bool IsMeasuring => false;

    /// <inheritdoc />
    public bool TryGetCpuUsage(ProcessId pid, out double coreEquivalents)
    {
        coreEquivalents = 0;
        return false;
    }
}

/// <summary>
/// Memory accountant of a host whose memory measurement is off (ADR 0066, section 4): every reader
/// falls back to the simulated value.
/// </summary>
public sealed class NullMemoryAccountant : IMemoryAccountant
{
    /// <summary>Gets the shared instance (the accountant is stateless).</summary>
    public static NullMemoryAccountant Instance { get; } = new();

    private NullMemoryAccountant()
    {
    }

    /// <inheritdoc />
    public bool IsMeasuring => false;

    /// <inheritdoc />
    public bool TryGetResidentBytes(ProcessId pid, out long bytes)
    {
        bytes = 0;
        return false;
    }
}
