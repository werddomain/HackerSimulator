using System;
using System.Threading;

namespace HackerOs.OS.Network.Core
{
    /// <summary>
    /// Maintains statistics about a socket's activity
    /// </summary>
    public class SocketStatistics
    {
        private long _bytesSent;
        private long _bytesReceived;
        private long _packetsSent;
        private long _packetsReceived;
        private DateTime _creationTime;
        private DateTime _lastActivityTime;

        public SocketStatistics()
        {
            _bytesSent = 0;
            _bytesReceived = 0;
            _packetsSent = 0;
            _packetsReceived = 0;
            _creationTime = DateTime.UtcNow;
            _lastActivityTime = _creationTime;
        }

        /// <summary>
        /// Gets the total number of bytes sent through the socket
        /// </summary>
        public long BytesSent => _bytesSent;

        /// <summary>
        /// Gets the total number of bytes received through the socket
        /// </summary>
        public long BytesReceived => _bytesReceived;

        /// <summary>
        /// Gets the total number of packets sent through the socket
        /// </summary>
        public long PacketsSent => _packetsSent;

        /// <summary>
        /// Gets the total number of packets received through the socket
        /// </summary>
        public long PacketsReceived => _packetsReceived;

        /// <summary>
        /// Gets the creation time of the socket
        /// </summary>
        public DateTime CreationTime => _creationTime;

        /// <summary>
        /// Gets the time of the last activity on the socket
        /// </summary>
        public DateTime LastActivityTime => _lastActivityTime;

        /// <summary>
        /// Increments the bytes sent counter
        /// </summary>
        /// <param name="bytes">The number of bytes sent</param>
        public void IncrementBytesSent(int bytes)
        {
            Interlocked.Add(ref _bytesSent, bytes);
            _lastActivityTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Increments the bytes received counter
        /// </summary>
        /// <param name="bytes">The number of bytes received</param>
        public void IncrementBytesReceived(int bytes)
        {
            Interlocked.Add(ref _bytesReceived, bytes);
            _lastActivityTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Increments the packets sent counter
        /// </summary>
        public void IncrementPacketsSent()
        {
            Interlocked.Increment(ref _packetsSent);
            _lastActivityTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Increments the packets received counter
        /// </summary>
        public void IncrementPacketsReceived()
        {
            Interlocked.Increment(ref _packetsReceived);
            _lastActivityTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Resets all statistics counters to zero
        /// </summary>
        public void Reset()
        {
            _bytesSent = 0;
            _bytesReceived = 0;
            _packetsSent = 0;
            _packetsReceived = 0;
            _lastActivityTime = DateTime.UtcNow;
        }
    }
}
