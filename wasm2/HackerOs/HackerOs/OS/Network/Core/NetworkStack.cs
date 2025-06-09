using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Network.Core
{
    /// <summary>
    /// Implementation of the network stack for HackerOS.
    /// Provides a virtual network environment with interfaces, routing, and socket management.
    /// </summary>
    public class NetworkStack : INetworkStack
    {
        private readonly ILogger<NetworkStack> _logger;
        private readonly Dictionary<string, INetworkInterface> _interfaces;
        private readonly Dictionary<string, ISocket> _sockets;
        private bool _isInitialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkStack"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public NetworkStack(ILogger<NetworkStack> logger)
        {
            _logger = logger;
            _interfaces = new Dictionary<string, INetworkInterface>();
            _sockets = new Dictionary<string, ISocket>();
            _isInitialized = false;
        }        /// <inheritdoc/>
        public event EventHandler<NetworkInterfaceEventArgs>? InterfaceStatusChanged;

        /// <inheritdoc/>
        public event EventHandler<PacketReceivedEventArgs>? PacketReceived;

        /// <inheritdoc/>
        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.LogWarning("Network stack already initialized");
                return true;
            }

            try
            {
                _logger.LogInformation("Initializing network stack");
                
                // Create loopback interface
                var loopback = new VirtualNetworkInterface("lo", "Loopback Interface")
                {
                    IPAddress = "127.0.0.1",
                    SubnetMask = "255.0.0.0",
                    MacAddress = "00:00:00:00:00:00",
                    IsActive = true
                };
                
                _interfaces.Add(loopback.Name, loopback);

                // Create default ethernet interface
                var eth0 = new VirtualNetworkInterface("eth0", "Ethernet Interface")
                {
                    IPAddress = "192.168.1.100",
                    SubnetMask = "255.255.255.0",
                    Gateway = "192.168.1.1",
                    MacAddress = "DE:AD:BE:EF:00:01",
                    IsActive = true
                };
                
                _interfaces.Add(eth0.Name, eth0);

                // Subscribe to interface events
                foreach (var iface in _interfaces.Values)
                {
                    iface.StateChanged += OnNetworkInterfaceStatusChanged;
                    iface.PacketReceived += OnPacketReceived;
                }

                _isInitialized = true;
                _logger.LogInformation("Network stack initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize network stack");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task ShutdownAsync()
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("Network stack not initialized");
                return;
            }

            try
            {
                _logger.LogInformation("Shutting down network stack");
                
                // Unsubscribe from interface events
                foreach (var iface in _interfaces.Values)
                {
                    iface.StateChanged -= OnNetworkInterfaceStatusChanged;
                    iface.PacketReceived -= OnPacketReceived;
                }

                // Close all sockets
                foreach (var socket in _sockets.Values.ToList())
                {
                    socket.Dispose();
                }
                
                _sockets.Clear();
                _interfaces.Clear();
                _isInitialized = false;
                
                _logger.LogInformation("Network stack shut down successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to shut down network stack");
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<INetworkInterface> GetNetworkInterfaces()
        {
            return _interfaces.Values.ToList().AsReadOnly();
        }

        /// <inheritdoc/>
        public INetworkInterface? GetNetworkInterface(string name)
        {
            if (_interfaces.TryGetValue(name, out var iface))
            {
                return iface;
            }
            
            return null;
        }

        /// <inheritdoc/>
        public async Task<ISocket> CreateSocketAsync(SocketType socketType, ProtocolType protocolType)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Network stack not initialized");
            }

            try
            {
                var socket = new VirtualSocket(
                    Guid.NewGuid().ToString(),
                    socketType,
                    AddressFamily.InterNetwork,
                    protocolType
                );
                
                _sockets[socket.SocketId] = socket;
                _logger.LogDebug("Created socket: {SocketId}, Type: {SocketType}, Protocol: {Protocol}",
                    socket.SocketId, socketType, protocolType);
                
                return socket;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create socket");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<string?> ResolveHostnameAsync(string hostname)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Network stack not initialized");
            }

            // For now, handle a few hardcoded hostnames
            // Will be replaced with proper DNS resolution later
            switch (hostname.ToLowerInvariant())
            {
                case "localhost":
                    return "127.0.0.1";
                case "example.com":
                    return "93.184.216.34"; // Simulated external IP
                case "test.local":
                    return "192.168.1.150"; // Simulated local network
                case "hackeros.net":
                    return "192.168.1.100"; // Points to own machine
                default:
                    // Simulate DNS failure for unknown hosts
                    _logger.LogWarning("Failed to resolve hostname: {Hostname}", hostname);
                    return null;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SendPacketAsync(NetworkPacket packet)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Network stack not initialized");
            }

            try
            {
                _logger.LogDebug("Sending packet: {PacketId} from {Source} to {Destination}",
                    packet.PacketId, packet.SourceAddress, packet.DestinationAddress);

                // Determine the outgoing interface based on destination
                var outInterface = DetermineOutgoingInterface(packet.DestinationAddress);
                if (outInterface == null)
                {
                    _logger.LogWarning("No route to host: {Destination}", packet.DestinationAddress);
                    return false;
                }                // Send the packet via the appropriate interface
                await outInterface.SendPacketAsync(packet);
                return true; // Return true after sending the packet
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send packet");
                return false;
            }
        }

        /// <summary>
        /// Determines the appropriate outgoing interface for a destination address.
        /// </summary>
        /// <param name="destinationAddress">The destination address.</param>
        /// <returns>The appropriate interface, or null if no route exists.</returns>
        private INetworkInterface? DetermineOutgoingInterface(string destinationAddress)
        {
            // Handle loopback addresses
            if (destinationAddress.StartsWith("127."))
            {
                return _interfaces["lo"];
            }

            // For now, just use eth0 for all other traffic
            // Will be replaced with proper routing logic later
            if (_interfaces.TryGetValue("eth0", out var eth0) && eth0.IsActive)
            {                return eth0;
            }

            return null;
        }

        /// <inheritdoc/>
        public NetworkStatistics GetNetworkStatistics()
        {
            var stats = new NetworkStatistics();
            
            foreach (var intf in _interfaces.Values)
            {
                if (intf.Statistics != null)
                {
                    stats.PacketsSent += intf.Statistics.PacketsSent;
                    stats.PacketsReceived += intf.Statistics.PacketsReceived;
                    stats.BytesSent += intf.Statistics.BytesSent;
                    stats.BytesReceived += intf.Statistics.BytesReceived;
                    stats.TrafficByInterface[intf.Name] = intf.Statistics.BytesSent + intf.Statistics.BytesReceived;
                }
            }
            
            stats.ActiveConnections = _sockets.Count;
            stats.LastUpdated = DateTime.UtcNow;
            
            return stats;
        }

        /// <inheritdoc/>
        public bool IsNetworkAvailable => _interfaces.Values.Any(intf => intf.IsUp && intf.IsActive);        /// <summary>
        /// Handles network interface status changed events.
        /// </summary>
        private void OnNetworkInterfaceStatusChanged(object? sender, NetworkInterfaceStateChangedEventArgs e)
        {
            var networkInterface = sender as INetworkInterface;
            _logger.LogInformation("Network interface {Name} state changed from {PreviousState} to {NewState}",
                networkInterface?.Name, e.PreviousState, e.NewState);
            
            // Create a new NetworkInterfaceEventArgs to pass to InterfaceStatusChanged event
            if (networkInterface != null)
            {
                var eventArgs = new NetworkInterfaceEventArgs(networkInterface);
                InterfaceStatusChanged?.Invoke(this, eventArgs);
            }
        }

        /// <summary>
        /// Handles packet received events.
        /// </summary>
        private void OnPacketReceived(object? sender, PacketReceivedEventArgs e)
        {
            _logger.LogDebug("Packet received on interface {InterfaceName}: {PacketId}",
                (sender as INetworkInterface)?.Name, e.Packet.PacketId);
            PacketReceived?.Invoke(this, e);
        }
    }
}
