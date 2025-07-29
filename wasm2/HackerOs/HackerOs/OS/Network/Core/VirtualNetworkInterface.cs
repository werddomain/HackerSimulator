using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.Network.Core
{
    /// <summary>
    /// Represents a virtual network interface in the HackerOS network stack.
    /// Simulates a network adapter with IP configuration, packet handling, and statistics.
    /// </summary>
    public class VirtualNetworkInterface : INetworkInterface
    {
        private readonly List<NetworkPacket> _packetQueue;
        private readonly Dictionary<string, string> _statistics;
        private bool _isActive;
        private readonly object _statisticsLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualNetworkInterface"/> class.
        /// </summary>
        /// <param name="name">The name of the interface (e.g., eth0, lo).</param>
        /// <param name="description">The description of the interface.</param>
        public VirtualNetworkInterface(string name, string description)
        {
            InterfaceId = Guid.NewGuid().ToString();
            Name = name;
            Description = description;
            IPAddress = string.Empty;
            SubnetMask = string.Empty;
            Gateway = string.Empty;
            MacAddress = GenerateMacAddress();
            MTU = 1500; // Default MTU
            _isActive = false;
            _packetQueue = new List<NetworkPacket>();
            _statistics = new Dictionary<string, string>
            {
                { "PacketsSent", "0" },
                { "PacketsReceived", "0" },
                { "BytesSent", "0" },
                { "BytesReceived", "0" },
                { "Errors", "0" },
                { "Dropped", "0" },
                { "LastActivity", DateTime.UtcNow.ToString("o") }
            };

            // Initialize NetworkInterfaceStatistics
            Statistics = new NetworkInterfaceStatistics();
        }

        /// <inheritdoc/>
        public string InterfaceId { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public string Description { get; }

        /// <inheritdoc/>
        public string IPAddress { get; set; }

        /// <inheritdoc/>
        public string SubnetMask { get; set; }       
         /// <inheritdoc/>
        public string Gateway { get; set; }

        /// <summary>
        /// Gets or sets the DNS servers for this interface.
        /// </summary>
        public List<string> DnsServers { get; set; } = new List<string>();
        
        /// <inheritdoc/>
        public string MacAddress { get; set; }
        
        /// <inheritdoc/>
        public int MTU { get; set; }

        /// <inheritdoc/>
        public bool IsUp => _isActive;

        /// <inheritdoc/>
        public bool SupportsDHCP { get; private set; } = true;

        /// <inheritdoc/>
        public NetworkLinkState LinkState => _isActive ? NetworkLinkState.Up : NetworkLinkState.Down;

        /// <inheritdoc/>
        public NetworkInterfaceStatistics Statistics { get; private set; }

        /// <inheritdoc/>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnStatusChanged(new NetworkInterfaceEventArgs(this));
                }
            }
        }        /// <inheritdoc/>
        public event EventHandler<NetworkInterfaceStateChangedEventArgs>? StateChanged;

        /// <inheritdoc/>
        public event EventHandler<PacketReceivedEventArgs>? PacketReceived;
        
        /// <inheritdoc/>
        public async Task BringUpAsync()
        {
            if (string.IsNullOrEmpty(IPAddress) || string.IsNullOrEmpty(SubnetMask))
            {
                return;
            }

            IsActive = true;
        }

        /// <inheritdoc/>
        public async Task BringDownAsync()
        {
            IsActive = false;
        }          /// <inheritdoc/>
        public async Task SendPacketAsync(NetworkPacket packet)
        {
            if (!IsActive)
            {
                IncrementStatistic("Dropped");
                Statistics.DroppedPackets++;
                return;
            }

            try
            {
                // Update statistics
                IncrementStatistic("PacketsSent");
                IncrementStatistic("BytesSent", packet.Data.Length);
                Statistics.PacketsTransmitted++;
                Statistics.BytesTransmitted += packet.Data.Length;
                UpdateLastActivity();

                // If this is the loopback interface or the packet is destined for this interface,
                // process it locally
                if (Name == "lo" || IsDestinationLocal(packet.DestinationAddress))
                {
                    await ReceivePacketAsync(packet);
                    return;
                }

                // Simulate network transmission delay
                await Task.Delay(SimulateNetworkDelay(packet));
            }
            catch
            {
                IncrementStatistic("Errors");
            }
        }/// <summary>
        /// Handles a received network packet.
        /// </summary>
        /// <param name="packet">The network packet that was received.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task<bool> ReceivePacketAsync(NetworkPacket packet)
        {
            if (!IsActive)
            {
                IncrementStatistic("Dropped");
                return false;
            }

            try
            {
                // Update statistics
                IncrementStatistic("PacketsReceived");
                IncrementStatistic("BytesReceived", packet.Data.Length);
                UpdateLastActivity();

                // Add to packet queue (could be used for packet capture/analysis)
                lock (_packetQueue)
                {
                    _packetQueue.Add(packet);
                    // Keep queue size manageable
                    if (_packetQueue.Count > 100)
                    {
                        _packetQueue.RemoveAt(0);
                    }
                }                
                // Raise packet received event
                OnPacketReceived(new PacketReceivedEventArgs(packet));

                return true;
            }
            catch
            {
                IncrementStatistic("Errors");
                return false;
            }
        }

        /// <inheritdoc/>
        public Task<NetworkInterfaceConfiguration> GetConfigurationAsync()
        {
            var config = new NetworkInterfaceConfiguration
            {
                InterfaceId = InterfaceId,
                Name = Name,
                IPAddress = IPAddress,
                SubnetMask = SubnetMask,
                Gateway = Gateway,
                MacAddress = MacAddress,
                MTU = MTU,
                IsActive = IsActive
            };

            return Task.FromResult(config);
        }

        /// <inheritdoc/>
        public async Task<bool> ConfigureAsync(NetworkInterfaceConfiguration configuration)
        {
            if (configuration == null)
            {
                return false;
            }

            IPAddress = configuration.IPAddress;
            SubnetMask = configuration.SubnetMask;
            Gateway = configuration.Gateway;
            MacAddress = configuration.MacAddress;
            MTU = configuration.MTU;

            // Change state if requested
            if (IsActive != configuration.IsActive)
            {
                if (configuration.IsActive)
                {
                    await BringUpAsync();
                }
                else
                {
                    await BringDownAsync();
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if the destination address is local to this interface.
        /// </summary>
        /// <param name="destinationAddress">The destination IP address.</param>
        /// <returns>True if the address is local to this interface; otherwise, false.</returns>
        private bool IsDestinationLocal(string destinationAddress)
        {
            // Check if it's exactly this interface's IP
            if (destinationAddress == IPAddress)
            {
                return true;
            }

            // Check for broadcast address
            if (destinationAddress == "255.255.255.255")
            {
                return true;
            }

            // TODO: Implement proper subnet checking

            return false;
        }

        /// <summary>
        /// Simulates network delay based on packet size and network conditions.
        /// </summary>
        /// <param name="packet">The packet being sent.</param>
        /// <returns>The delay in milliseconds.</returns>
        private int SimulateNetworkDelay(NetworkPacket packet)
        {
            // Simulate network conditions based on interface
            // For loopback, almost no delay
            if (Name == "lo")
            {
                return 1;
            }

            // For ethernet, simulate based on packet size
            var basePing = 5; // Base latency in ms
            var packetSizeImpact = packet.Data.Length / 1000; // 1ms per KB

            // Add some randomness
            var random = new Random();
            var jitter = random.Next(0, 10);

            return basePing + packetSizeImpact + jitter;
        }

        /// <summary>
        /// Generates a random MAC address.
        /// </summary>
        /// <returns>A MAC address string.</returns>
        private string GenerateMacAddress()
        {
            var random = new Random();
            var mac = new byte[6];
            random.NextBytes(mac);

            // Ensure it's a unicast, locally administered address
            mac[0] = (byte)(mac[0] & 0xFE | 0x02);

            return string.Join(":", mac.Select(b => b.ToString("X2")));
        }

        /// <summary>
        /// Increments a statistic counter.
        /// </summary>
        /// <param name="key">The statistic key.</param>
        /// <param name="amount">The amount to increment by.</param>
        private void IncrementStatistic(string key, int amount = 1)
        {
            lock (_statisticsLock)
            {
                if (_statistics.TryGetValue(key, out var valueStr) &&
                    int.TryParse(valueStr, out var value))
                {
                    _statistics[key] = (value + amount).ToString();
                }
            }
        }

        /// <summary>
        /// Updates the last activity timestamp.
        /// </summary>
        private void UpdateLastActivity()
        {
            lock (_statisticsLock)
            {
                _statistics["LastActivity"] = DateTime.UtcNow.ToString("o");
            }
        }        /// <summary>
        /// Raises the status changed event.
        /// </summary>
        /// <param name="e">The event arguments.</param>        
        protected virtual void OnStatusChanged(NetworkInterfaceEventArgs e)
        {
            StateChanged?.Invoke(this, new NetworkInterfaceStateChangedEventArgs(NetworkLinkState.Down, LinkState));
        }

        /// <summary>
        /// Raises the packet received event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnPacketReceived(PacketReceivedEventArgs e)
        {
            PacketReceived?.Invoke(this, e);
        }

        /// <inheritdoc/>
        public async Task ConfigureAsync(NetworkInterfaceConfig config)
        {
            IPAddress = config.IPAddress;
            SubnetMask = config.SubnetMask;
            Gateway = config.Gateway;
            // Apply other configuration settings as needed
        }

        /// <inheritdoc/>
        public async Task<DHCPLease> RequestDHCPLeaseAsync()
        {
            // Simulate DHCP lease request            
            var lease = new DHCPLease
            {
                IPAddress = "192.168.1.100", // Example IP
                SubnetMask = "255.255.255.0",
                Gateway = "192.168.1.1",
                DnsServers = new List<string> { "8.8.8.8", "8.8.4.4" },
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                DHCPServer = "192.168.1.1"
            };
            
            // Apply the lease
            IPAddress = lease.IPAddress;
            SubnetMask = lease.SubnetMask;
            Gateway = lease.Gateway;
            
            return lease;
        }

        /// <inheritdoc/>
        public async Task ReleaseDHCPLeaseAsync()
        {
            // Reset to default values
            IPAddress = string.Empty;
            SubnetMask = string.Empty;
            Gateway = string.Empty;
        }        /// <inheritdoc/>
        public NetworkInterfaceConfig GetConfiguration()
        {
            return new NetworkInterfaceConfig
            {
                IPAddress = IPAddress,
                SubnetMask = SubnetMask,
                Gateway = Gateway,
                DnsServers = DnsServers,
                // Add other properties as needed
            };
        }

        /// <inheritdoc/>
        public async Task<PingResult> PingAsync(string destination, TimeSpan timeout)
        {
            if (!IsActive)
            {
                return new PingResult
                {
                    Success = false,
                    ErrorMessage = "Interface is not active",
                    Destination = destination
                };
            }

            // Simulate ping operation
            await Task.Delay(50); // Simulate network delay
            
            return new PingResult
            {
                Success = true,
                RoundTripTime = TimeSpan.FromMilliseconds(50),
                Destination = destination,
                TTL = 64
            };
        }
    }

    /// <summary>
    /// Represents configuration for a network interface.
    /// </summary>
    public class NetworkInterfaceConfiguration
    {
        /// <summary>
        /// Gets or sets the interface ID.
        /// </summary>
        public string InterfaceId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the interface name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the IP address.
        /// </summary>
        public string IPAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the subnet mask.
        /// </summary>
        public string SubnetMask { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the gateway address.
        /// </summary>
        public string Gateway { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the MAC address.
        /// </summary>
        public string MacAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the MTU.
        /// </summary>
        public int MTU { get; set; } = 1500;

        /// <summary>
        /// Gets or sets a value indicating whether the interface is active.
        /// </summary>
        public bool IsActive { get; set; }
    }
}
