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

        /// <inheritdoc/>
        public string MacAddress { get; set; }

        /// <inheritdoc/>
        public int MTU { get; set; }

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
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> Statistics => _statistics;

        /// <inheritdoc/>
        public event EventHandler<NetworkInterfaceEventArgs>? StatusChanged;

        /// <inheritdoc/>
        public event EventHandler<NetworkPacketEventArgs>? PacketReceived;

        /// <inheritdoc/>
        public async Task<bool> BringUpAsync()
        {
            if (string.IsNullOrEmpty(IPAddress) || string.IsNullOrEmpty(SubnetMask))
            {
                return false;
            }

            IsActive = true;
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> BringDownAsync()
        {
            IsActive = false;
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> SendPacketAsync(NetworkPacket packet)
        {
            if (!IsActive)
            {
                IncrementStatistic("Dropped");
                return false;
            }

            try
            {
                // Update statistics
                IncrementStatistic("PacketsSent");
                IncrementStatistic("BytesSent", packet.Data.Length);
                UpdateLastActivity();

                // If this is the loopback interface or the packet is destined for this interface,
                // process it locally
                if (Name == "lo" || IsDestinationLocal(packet.DestinationAddress))
                {
                    await ReceivePacketAsync(packet);
                    return true;
                }

                // Simulate network transmission delay
                await Task.Delay(SimulateNetworkDelay(packet));

                return true;
            }
            catch
            {
                IncrementStatistic("Errors");
                return false;
            }
        }

        /// <inheritdoc/>
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
                OnPacketReceived(new NetworkPacketEventArgs(packet));

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
        }

        /// <summary>
        /// Raises the status changed event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnStatusChanged(NetworkInterfaceEventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the packet received event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnPacketReceived(NetworkPacketEventArgs e)
        {
            PacketReceived?.Invoke(this, e);
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
