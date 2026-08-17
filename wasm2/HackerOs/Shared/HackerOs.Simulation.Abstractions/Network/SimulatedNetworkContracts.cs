using System.Collections.Immutable;

namespace HackerOs.Simulation.Abstractions.Network;

/// <summary>
/// State of a simulated port (open, closed, or filtered by a firewall).
/// </summary>
public enum SimulatedPortState
{
    /// <summary>A service is actively listening on this port.</summary>
    Open,
    /// <summary>No service is listening; connection actively refused.</summary>
    Closed,
    /// <summary>A firewall drops packets without acknowledgement.</summary>
    Filtered
}

/// <summary>
/// Immutable descriptor for one service running behind a simulated port.
/// </summary>
public sealed record SimulatedPortService(
    string Name,
    string? Version = null,
    string? Info = null);

/// <summary>
/// Immutable port entry for a simulated host.
/// </summary>
public sealed record SimulatedPort(
    int PortNumber,
    SimulatedPortState State,
    SimulatedPortService? Service = null)
{
    /// <summary>Returns true when the port is reachable by external clients.</summary>
    public bool IsReachable => State == SimulatedPortState.Open;
}

/// <summary>
/// Immutable OS fingerprint returned by scanning a simulated host.
/// </summary>
public sealed record SimulatedOsFingerprint(
    string Name,
    string Version,
    /// <summary>Simulated detection accuracy percentage (0-100).</summary>
    int Accuracy);

/// <summary>
/// Immutable record representing a single host in the simulated network.
/// A host may be addressed by IP or by its canonical hostname.
/// </summary>
public sealed record SimulatedHost(
    /// <summary>Dot-decimal IPv4 string, e.g. "192.168.1.10".</summary>
    string Ip,
    /// <summary>Primary fully-qualified hostname, e.g. "example.com".</summary>
    string Hostname,
    /// <summary>Whether the host is currently reachable (responds to ping).</summary>
    bool IsUp,
    /// <summary>Simulated round-trip time in milliseconds.</summary>
    double LatencyMs,
    ImmutableArray<SimulatedPort> Ports,
    SimulatedOsFingerprint? OsFingerprint = null,
    /// <summary>Optional MAC address (meaningful for LAN hosts only).</summary>
    string? MacAddress = null)
{
    /// <summary>
    /// Returns ports visible in the given inclusive port range, in port-number order.
    /// </summary>
    public IEnumerable<SimulatedPort> ScanPorts(int firstPort, int lastPort) =>
        Ports
            .Where(p => p.PortNumber >= firstPort && p.PortNumber <= lastPort)
            .OrderBy(p => p.PortNumber);
}
