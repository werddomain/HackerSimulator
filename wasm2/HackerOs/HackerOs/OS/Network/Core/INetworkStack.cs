// Core Network Stack Interface - Main network operations abstraction
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Network.Core
{
    /// <summary>
    /// Core network stack interface providing virtual network operations
    /// Simulates a complete network stack for the HackerOS environment
    /// </summary>
    public interface INetworkStack
    {
        /// <summary>
        /// Initialize the network stack
        /// </summary>
        Task<bool> InitializeAsync();

        /// <summary>
        /// Shutdown the network stack
        /// </summary>
        Task ShutdownAsync();

        /// <summary>
        /// Get all available network interfaces
        /// </summary>
        IReadOnlyList<INetworkInterface> GetNetworkInterfaces();

        /// <summary>
        /// Get network interface by name
        /// </summary>
        INetworkInterface? GetNetworkInterface(string name);

        /// <summary>
        /// Create a new virtual socket
        /// </summary>
        Task<ISocket> CreateSocketAsync(SocketType socketType, ProtocolType protocolType);

        /// <summary>
        /// Resolve hostname to IP address using DNS
        /// </summary>
        Task<string?> ResolveHostnameAsync(string hostname);

        /// <summary>
        /// Send a network packet
        /// </summary>
        Task<bool> SendPacketAsync(NetworkPacket packet);

        /// <summary>
        /// Network interface status changed event
        /// </summary>
        event EventHandler<NetworkInterfaceEventArgs> InterfaceStatusChanged;

        /// <summary>
        /// Packet received event
        /// </summary>
        event EventHandler<PacketReceivedEventArgs> PacketReceived;

        /// <summary>
        /// Get network statistics
        /// </summary>
        NetworkStatistics GetNetworkStatistics();

        /// <summary>
        /// Check if network is available
        /// </summary>
        bool IsNetworkAvailable { get; }
    }

    /// <summary>
    /// Network interface types
    /// </summary>
    public enum NetworkInterfaceType
    {
        Loopback,
        Ethernet,
        Wireless,
        Virtual
    }

    /// <summary>
    /// Network statistics information
    /// </summary>
    public class NetworkStatistics
    {
        public long PacketsSent { get; set; }
        public long PacketsReceived { get; set; }
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public int ActiveConnections { get; set; }
        public TimeSpan Uptime { get; set; }
        public Dictionary<string, int> ConnectionsByProtocol { get; set; } = new();
        public Dictionary<string, long> TrafficByInterface { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
