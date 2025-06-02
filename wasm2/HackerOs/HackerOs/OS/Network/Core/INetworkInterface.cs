using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Network.Core
{
    /// <summary>
    /// Represents a virtual network interface in the HackerOS network stack.
    /// Provides abstraction for network communication including packet transmission,
    /// address management, and interface configuration.
    /// </summary>
    public interface INetworkInterface
    {
        /// <summary>
        /// Gets the unique identifier for this network interface.
        /// </summary>
        string InterfaceId { get; }

        /// <summary>
        /// Gets the friendly name of the network interface (e.g., "eth0", "wlan0").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets or sets the IP address assigned to this interface.
        /// </summary>
        string IPAddress { get; set; }

        /// <summary>
        /// Gets or sets the subnet mask for this interface.
        /// </summary>
        string SubnetMask { get; set; }

        /// <summary>
        /// Gets or sets the default gateway for this interface.
        /// </summary>
        string Gateway { get; set; }

        /// <summary>
        /// Gets or sets the MAC address for this interface.
        /// </summary>
        string MacAddress { get; set; }

        /// <summary>
        /// Gets the maximum transmission unit (MTU) for this interface.
        /// </summary>
        int MTU { get; }

        /// <summary>
        /// Gets a value indicating whether this interface is currently active and operational.
        /// </summary>
        bool IsUp { get; }

        /// <summary>
        /// Gets a value indicating whether this interface supports DHCP.
        /// </summary>
        bool SupportsDHCP { get; }

        /// <summary>
        /// Gets the current link state of the interface.
        /// </summary>
        NetworkLinkState LinkState { get; }

        /// <summary>
        /// Gets network statistics for this interface.
        /// </summary>
        NetworkInterfaceStatistics Statistics { get; }

        /// <summary>
        /// Event raised when the interface state changes.
        /// </summary>
        event EventHandler<NetworkInterfaceStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Event raised when a packet is received on this interface.
        /// </summary>
        event EventHandler<PacketReceivedEventArgs> PacketReceived;

        /// <summary>
        /// Activates the network interface and brings it up.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task BringUpAsync();

        /// <summary>
        /// Deactivates the network interface and brings it down.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task BringDownAsync();

        /// <summary>
        /// Sends a packet through this network interface.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SendPacketAsync(NetworkPacket packet);

        /// <summary>
        /// Configures the interface with the specified network settings.
        /// </summary>
        /// <param name="config">The network configuration to apply.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ConfigureAsync(NetworkInterfaceConfig config);

        /// <summary>
        /// Performs a DHCP request to obtain network configuration automatically.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation and returns the DHCP lease.</returns>
        Task<DHCPLease> RequestDHCPLeaseAsync();

        /// <summary>
        /// Releases the current DHCP lease.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ReleaseDHCPLeaseAsync();

        /// <summary>
        /// Gets the current network configuration for this interface.
        /// </summary>
        /// <returns>The current network interface configuration.</returns>
        NetworkInterfaceConfig GetConfiguration();

        /// <summary>
        /// Performs a ping to the specified destination.
        /// </summary>
        /// <param name="destination">The destination IP address to ping.</param>
        /// <param name="timeout">The timeout for the ping operation.</param>
        /// <returns>A task that represents the asynchronous operation and returns the ping result.</returns>
        Task<PingResult> PingAsync(string destination, TimeSpan timeout);
    }

    /// <summary>
    /// Represents the link state of a network interface.
    /// </summary>
    public enum NetworkLinkState
    {
        /// <summary>
        /// The interface is down and not operational.
        /// </summary>
        Down,

        /// <summary>
        /// The interface is up and operational.
        /// </summary>
        Up,

        /// <summary>
        /// The interface is in an unknown state.
        /// </summary>
        Unknown,

        /// <summary>
        /// The interface is dormant (waiting for external event).
        /// </summary>
        Dormant,

        /// <summary>
        /// The interface is in testing mode.
        /// </summary>
        Testing
    }

    /// <summary>
    /// Contains network statistics for a network interface.
    /// </summary>
    public class NetworkInterfaceStatistics
    {
        /// <summary>
        /// Gets or sets the number of bytes received.
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes transmitted.
        /// </summary>
        public long BytesTransmitted { get; set; }

        /// <summary>
        /// Gets or sets the number of packets received.
        /// </summary>
        public long PacketsReceived { get; set; }

        /// <summary>
        /// Gets or sets the number of packets transmitted.
        /// </summary>
        public long PacketsTransmitted { get; set; }

        /// <summary>
        /// Gets or sets the number of receive errors.
        /// </summary>
        public long ReceiveErrors { get; set; }

        /// <summary>
        /// Gets or sets the number of transmission errors.
        /// </summary>
        public long TransmissionErrors { get; set; }

        /// <summary>
        /// Gets or sets the number of dropped packets.
        /// </summary>
        public long DroppedPackets { get; set; }

        /// <summary>
        /// Gets or sets the number of collisions detected.
        /// </summary>
        public long Collisions { get; set; }

        /// <summary>
        /// Gets the interface uptime.
        /// </summary>
        public TimeSpan Uptime { get; set; }
    }

    /// <summary>
    /// Event arguments for network interface state changes.
    /// </summary>
    public class NetworkInterfaceStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous link state.
        /// </summary>
        public NetworkLinkState PreviousState { get; }

        /// <summary>
        /// Gets the new link state.
        /// </summary>
        public NetworkLinkState NewState { get; }

        /// <summary>
        /// Gets the timestamp when the state change occurred.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the NetworkInterfaceStateChangedEventArgs class.
        /// </summary>
        /// <param name="previousState">The previous link state.</param>
        /// <param name="newState">The new link state.</param>
        public NetworkInterfaceStateChangedEventArgs(NetworkLinkState previousState, NetworkLinkState newState)
        {
            PreviousState = previousState;
            NewState = newState;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for packet received events.
    /// </summary>
    public class PacketReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the received packet.
        /// </summary>
        public NetworkPacket Packet { get; }

        /// <summary>
        /// Gets the timestamp when the packet was received.
        /// </summary>
        public DateTime ReceivedAt { get; }

        /// <summary>
        /// Initializes a new instance of the PacketReceivedEventArgs class.
        /// </summary>
        /// <param name="packet">The received packet.</param>
        public PacketReceivedEventArgs(NetworkPacket packet)
        {
            Packet = packet ?? throw new ArgumentNullException(nameof(packet));
            ReceivedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents network interface configuration.
    /// </summary>
    public class NetworkInterfaceConfig
    {
        /// <summary>
        /// Gets or sets the IP address.
        /// </summary>
        public string IPAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the subnet mask.
        /// </summary>
        public string SubnetMask { get; set; } = "255.255.255.0";

        /// <summary>
        /// Gets or sets the default gateway.
        /// </summary>
        public string Gateway { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the DNS servers.
        /// </summary>
        public List<string> DnsServers { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets a value indicating whether DHCP is enabled.
        /// </summary>
        public bool UseDHCP { get; set; } = true;

        /// <summary>
        /// Gets or sets the MTU size.
        /// </summary>
        public int MTU { get; set; } = 1500;
    }

    /// <summary>
    /// Represents a DHCP lease.
    /// </summary>
    public class DHCPLease
    {
        /// <summary>
        /// Gets or sets the assigned IP address.
        /// </summary>
        public string IPAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the subnet mask.
        /// </summary>
        public string SubnetMask { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the default gateway.
        /// </summary>
        public string Gateway { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the DNS servers.
        /// </summary>
        public List<string> DnsServers { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the lease expiration time.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the DHCP server address.
        /// </summary>
        public string DHCPServer { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a ping result.
    /// </summary>
    public class PingResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the ping was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the round-trip time.
        /// </summary>
        public TimeSpan RoundTripTime { get; set; }

        /// <summary>
        /// Gets or sets the TTL (Time To Live).
        /// </summary>
        public int TTL { get; set; }

        /// <summary>
        /// Gets or sets the error message if the ping failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the destination address that was pinged.
        /// </summary>
        public string Destination { get; set; } = string.Empty;
    }
}
