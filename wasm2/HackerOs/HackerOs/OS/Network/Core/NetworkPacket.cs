using System;
using System.Collections.Generic;

namespace HackerOs.OS.Network.Core
{
    /// <summary>
    /// Represents a network packet in the HackerOS virtual network stack.
    /// Contains packet data, headers, and metadata for network transmission.
    /// </summary>
    public class NetworkPacket
    {
        /// <summary>
        /// Gets or sets the unique identifier for this packet.
        /// </summary>
        public string PacketId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the source address.
        /// </summary>
        public string SourceAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the destination address.
        /// </summary>
        public string DestinationAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source port.
        /// </summary>
        public int SourcePort { get; set; }

        /// <summary>
        /// Gets or sets the destination port.
        /// </summary>
        public int DestinationPort { get; set; }

        /// <summary>
        /// Gets or sets the protocol type.
        /// </summary>
        public ProtocolType Protocol { get; set; }

        /// <summary>
        /// Gets or sets the packet data payload.
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the packet headers.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the packet flags.
        /// </summary>
        public PacketFlags Flags { get; set; } = PacketFlags.None;

        /// <summary>
        /// Gets or sets the time-to-live (TTL) value.
        /// </summary>
        public int TTL { get; set; } = 64;

        /// <summary>
        /// Gets or sets the packet sequence number.
        /// </summary>
        public uint SequenceNumber { get; set; }

        /// <summary>
        /// Gets or sets the acknowledgment number.
        /// </summary>
        public uint AcknowledgmentNumber { get; set; }

        /// <summary>
        /// Gets or sets the window size.
        /// </summary>
        public int WindowSize { get; set; } = 65535;

        /// <summary>
        /// Gets or sets the checksum.
        /// </summary>
        public ushort Checksum { get; set; }

        /// <summary>
        /// Gets or sets the packet priority.
        /// </summary>
        public PacketPriority Priority { get; set; } = PacketPriority.Normal;

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the packet size in bytes.
        /// </summary>
        public int Size => Data?.Length ?? 0;

        /// <summary>
        /// Gets or sets additional metadata for the packet.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the NetworkPacket class.
        /// </summary>
        public NetworkPacket()
        {
        }

        /// <summary>
        /// Initializes a new instance of the NetworkPacket class.
        /// </summary>
        /// <param name="sourceAddress">The source address.</param>
        /// <param name="destinationAddress">The destination address.</param>
        /// <param name="data">The packet data.</param>
        /// <param name="protocol">The protocol type.</param>
        public NetworkPacket(string sourceAddress, string destinationAddress, byte[] data, ProtocolType protocol = ProtocolType.Tcp)
        {
            SourceAddress = sourceAddress;
            DestinationAddress = destinationAddress;
            Data = data ?? Array.Empty<byte>();
            Protocol = protocol;
        }

        /// <summary>
        /// Initializes a new instance of the NetworkPacket class.
        /// </summary>
        /// <param name="sourceAddress">The source address.</param>
        /// <param name="sourcePort">The source port.</param>
        /// <param name="destinationAddress">The destination address.</param>
        /// <param name="destinationPort">The destination port.</param>
        /// <param name="data">The packet data.</param>
        /// <param name="protocol">The protocol type.</param>
        public NetworkPacket(string sourceAddress, int sourcePort, string destinationAddress, int destinationPort, 
                           byte[] data, ProtocolType protocol = ProtocolType.Tcp)
            : this(sourceAddress, destinationAddress, data, protocol)
        {
            SourcePort = sourcePort;
            DestinationPort = destinationPort;
        }

        /// <summary>
        /// Creates a copy of the current packet.
        /// </summary>
        /// <returns>A new NetworkPacket instance with the same data.</returns>
        public NetworkPacket Clone()
        {
            var clone = new NetworkPacket
            {
                PacketId = PacketId,
                SourceAddress = SourceAddress,
                DestinationAddress = DestinationAddress,
                SourcePort = SourcePort,
                DestinationPort = DestinationPort,
                Protocol = Protocol,
                Data = new byte[Data.Length],
                Headers = new Dictionary<string, string>(Headers),
                Flags = Flags,
                TTL = TTL,
                SequenceNumber = SequenceNumber,
                AcknowledgmentNumber = AcknowledgmentNumber,
                WindowSize = WindowSize,
                Checksum = Checksum,
                Priority = Priority,
                CreatedAt = CreatedAt,
                Metadata = new Dictionary<string, object>(Metadata)
            };

            Array.Copy(Data, clone.Data, Data.Length);
            return clone;
        }

        /// <summary>
        /// Calculates a simple checksum for the packet.
        /// </summary>
        /// <returns>The calculated checksum.</returns>
        public ushort CalculateChecksum()
        {
            uint sum = 0;
            
            // Include addresses in checksum
            foreach (char c in SourceAddress + DestinationAddress)
            {
                sum += (uint)c;
            }

            // Include ports
            sum += (uint)SourcePort + (uint)DestinationPort;

            // Include data
            for (int i = 0; i < Data.Length; i += 2)
            {
                ushort word = (ushort)(Data[i] << 8);
                if (i + 1 < Data.Length)
                {
                    word |= Data[i + 1];
                }
                sum += word;
            }

            // Fold 32-bit sum to 16 bits
            while ((sum >> 16) != 0)
            {
                sum = (sum & 0xFFFF) + (sum >> 16);
            }

            return (ushort)~sum;
        }

        /// <summary>
        /// Validates the packet checksum.
        /// </summary>
        /// <returns>True if the checksum is valid; otherwise, false.</returns>
        public bool ValidateChecksum()
        {
            return Checksum == CalculateChecksum();
        }

        /// <summary>
        /// Sets a header value.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        public void SetHeader(string name, string value)
        {
            Headers[name] = value;
        }

        /// <summary>
        /// Gets a header value.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <returns>The header value, or null if not found.</returns>
        public string? GetHeader(string name)
        {
            return Headers.TryGetValue(name, out string? value) ? value : null;
        }

        /// <summary>
        /// Checks if the packet has a specific header.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <returns>True if the header exists; otherwise, false.</returns>
        public bool HasHeader(string name)
        {
            return Headers.ContainsKey(name);
        }

        /// <summary>
        /// Sets metadata for the packet.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        public void SetMetadata(string key, object value)
        {
            Metadata[key] = value;
        }

        /// <summary>
        /// Gets metadata from the packet.
        /// </summary>
        /// <typeparam name="T">The type of the metadata value.</typeparam>
        /// <param name="key">The metadata key.</param>
        /// <returns>The metadata value, or default(T) if not found.</returns>
        public T? GetMetadata<T>(string key)
        {
            if (Metadata.TryGetValue(key, out object? value) && value is T typedValue)
            {
                return typedValue;
            }
            return default(T);
        }

        /// <summary>
        /// Returns a string representation of the packet.
        /// </summary>
        /// <returns>A string describing the packet.</returns>
        public override string ToString()
        {
            return $"Packet {PacketId}: {SourceAddress}:{SourcePort} -> {DestinationAddress}:{DestinationPort} " +
                   $"({Protocol}, {Size} bytes, TTL={TTL})";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the objects are equal; otherwise, false.</returns>
        public override bool Equals(object? obj)
        {
            if (obj is NetworkPacket other)
            {
                return PacketId == other.PacketId;
            }
            return false;
        }

        /// <summary>
        /// Returns a hash code for the current object.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            return PacketId.GetHashCode();
        }
    }

    /// <summary>
    /// Represents packet flags.
    /// </summary>
    [Flags]
    public enum PacketFlags
    {
        /// <summary>
        /// No flags set.
        /// </summary>
        None = 0,

        /// <summary>
        /// Acknowledgment flag.
        /// </summary>
        Ack = 1,

        /// <summary>
        /// Synchronize flag.
        /// </summary>
        Syn = 2,

        /// <summary>
        /// Finish flag.
        /// </summary>
        Fin = 4,

        /// <summary>
        /// Reset flag.
        /// </summary>
        Rst = 8,

        /// <summary>
        /// Push flag.
        /// </summary>
        Psh = 16,

        /// <summary>
        /// Urgent flag.
        /// </summary>
        Urg = 32,

        /// <summary>
        /// Don't fragment flag.
        /// </summary>
        DontFragment = 64,

        /// <summary>
        /// More fragments flag.
        /// </summary>
        MoreFragments = 128
    }

    /// <summary>
    /// Represents packet priority levels.
    /// </summary>
    public enum PacketPriority
    {
        /// <summary>
        /// Low priority.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority.
        /// </summary>
        High = 2,

        /// <summary>
        /// Critical priority.
        /// </summary>
        Critical = 3
    }
}
