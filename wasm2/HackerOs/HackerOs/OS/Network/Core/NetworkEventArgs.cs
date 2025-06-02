using System;

namespace HackerOs.OS.Network.Core
{
    /// <summary>
    /// Event arguments for network interface events.
    /// </summary>
    public class NetworkInterfaceEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkInterfaceEventArgs"/> class.
        /// </summary>
        /// <param name="networkInterface">The network interface.</param>
        public NetworkInterfaceEventArgs(INetworkInterface networkInterface)
        {
            Interface = networkInterface;
        }

        /// <summary>
        /// Gets the network interface.
        /// </summary>
        public INetworkInterface Interface { get; }
    }

    /// <summary>
    /// Event arguments for network packet events.
    /// </summary>
    public class NetworkPacketEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkPacketEventArgs"/> class.
        /// </summary>
        /// <param name="packet">The network packet.</param>
        public NetworkPacketEventArgs(NetworkPacket packet)
        {
            Packet = packet;
        }

        /// <summary>
        /// Gets the network packet.
        /// </summary>
        public NetworkPacket Packet { get; }
    }
}
