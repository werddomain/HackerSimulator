using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace HackerOs.OS.Network.Core
{
    /// <summary>
    /// Represents a virtual socket in the HackerOS network stack.
    /// Provides abstraction for network communication endpoints including TCP, UDP, and other protocols.
    /// </summary>
    public interface ISocket : IDisposable
    {
        /// <summary>
        /// Gets the unique identifier for this socket.
        /// </summary>
        string SocketId { get; }

        /// <summary>
        /// Gets the socket type (TCP, UDP, etc.).
        /// </summary>
        SocketType SocketType { get; }

        /// <summary>
        /// Gets the protocol family (IPv4, IPv6, etc.).
        /// </summary>
        AddressFamily AddressFamily { get; }

        /// <summary>
        /// Gets the protocol type.
        /// </summary>
        ProtocolType Protocol { get; }

        /// <summary>
        /// Gets the current state of the socket.
        /// </summary>
        SocketState State { get; }

        /// <summary>
        /// Gets the local endpoint address.
        /// </summary>
        NetworkEndPoint? LocalEndPoint { get; }

        /// <summary>
        /// Gets the remote endpoint address.
        /// </summary>
        NetworkEndPoint? RemoteEndPoint { get; }

        /// <summary>
        /// Gets a value indicating whether the socket is connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets a value indicating whether the socket is bound to a local address.
        /// </summary>
        bool IsBound { get; }

        /// <summary>
        /// Gets a value indicating whether the socket is listening for connections.
        /// </summary>
        bool IsListening { get; }

        /// <summary>
        /// Gets or sets the socket timeout for operations.
        /// </summary>
        TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets or sets the size of the receive buffer.
        /// </summary>
        int ReceiveBufferSize { get; set; }

        /// <summary>
        /// Gets or sets the size of the send buffer.
        /// </summary>
        int SendBufferSize { get; set; }

        /// <summary>
        /// Gets socket statistics.
        /// </summary>
        SocketStatistics Statistics { get; }

        /// <summary>
        /// Event raised when data is received on this socket.
        /// </summary>
        event EventHandler<SocketDataReceivedEventArgs> DataReceived;

        /// <summary>
        /// Event raised when the socket state changes.
        /// </summary>
        event EventHandler<SocketStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Event raised when an error occurs on the socket.
        /// </summary>
        event EventHandler<SocketErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// Binds the socket to a local endpoint.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task BindAsync(NetworkEndPoint localEndPoint);

        /// <summary>
        /// Connects the socket to a remote endpoint.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint to connect to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ConnectAsync(NetworkEndPoint remoteEndPoint);

        /// <summary>
        /// Starts listening for incoming connections (TCP only).
        /// </summary>
        /// <param name="backlog">The maximum length of the pending connections queue.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ListenAsync(int backlog = 10);

        /// <summary>
        /// Accepts a pending connection request (TCP only).
        /// </summary>
        /// <returns>A task that represents the asynchronous operation and returns the accepted socket.</returns>
        Task<ISocket> AcceptAsync();

        /// <summary>
        /// Sends data through the socket.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <param name="flags">Socket send flags.</param>
        /// <returns>A task that represents the asynchronous operation and returns the number of bytes sent.</returns>
        Task<int> SendAsync(byte[] data, SocketFlags flags = SocketFlags.None);

        /// <summary>
        /// Sends data to a specific endpoint (UDP only).
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <param name="remoteEndPoint">The destination endpoint.</param>
        /// <param name="flags">Socket send flags.</param>
        /// <returns>A task that represents the asynchronous operation and returns the number of bytes sent.</returns>
        Task<int> SendToAsync(byte[] data, NetworkEndPoint remoteEndPoint, SocketFlags flags = SocketFlags.None);

        /// <summary>
        /// Receives data from the socket.
        /// </summary>
        /// <param name="buffer">The buffer to store received data.</param>
        /// <param name="flags">Socket receive flags.</param>
        /// <returns>A task that represents the asynchronous operation and returns the number of bytes received.</returns>
        Task<int> ReceiveAsync(byte[] buffer, SocketFlags flags = SocketFlags.None);

        /// <summary>
        /// Receives data from any source (UDP only).
        /// </summary>
        /// <param name="buffer">The buffer to store received data.</param>
        /// <param name="flags">Socket receive flags.</param>
        /// <returns>A task that represents the asynchronous operation and returns the received data and source endpoint.</returns>
        Task<SocketReceiveResult> ReceiveFromAsync(byte[] buffer, SocketFlags flags = SocketFlags.None);

        /// <summary>
        /// Shuts down the socket for sending and/or receiving.
        /// </summary>
        /// <param name="how">Specifies which operations to shut down.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ShutdownAsync(SocketShutdown how);

        /// <summary>
        /// Closes the socket and releases associated resources.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task CloseAsync();

        /// <summary>
        /// Gets socket options.
        /// </summary>
        /// <param name="optionLevel">The socket option level.</param>
        /// <param name="optionName">The socket option name.</param>
        /// <returns>The socket option value.</returns>
        object GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName);

        /// <summary>
        /// Sets socket options.
        /// </summary>
        /// <param name="optionLevel">The socket option level.</param>
        /// <param name="optionName">The socket option name.</param>
        /// <param name="optionValue">The socket option value.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SetSocketOptionAsync(SocketOptionLevel optionLevel, SocketOptionName optionName, object optionValue);
    }

    /// <summary>
    /// Represents the type of socket.
    /// </summary>
    public enum SocketType
    {
        /// <summary>
        /// Stream socket (TCP).
        /// </summary>
        Stream,

        /// <summary>
        /// Datagram socket (UDP).
        /// </summary>
        Dgram,

        /// <summary>
        /// Raw socket.
        /// </summary>
        Raw,

        /// <summary>
        /// Reliable datagram socket.
        /// </summary>
        Rdm,

        /// <summary>
        /// Sequenced packet socket.
        /// </summary>
        Seqpacket
    }

    /// <summary>
    /// Represents the address family.
    /// </summary>
    public enum AddressFamily
    {
        /// <summary>
        /// IPv4 address family.
        /// </summary>
        InterNetwork,

        /// <summary>
        /// IPv6 address family.
        /// </summary>
        InterNetworkV6,

        /// <summary>
        /// Unix domain sockets.
        /// </summary>
        Unix
    }

    /// <summary>
    /// Represents the protocol type.
    /// </summary>
    public enum ProtocolType
    {
        /// <summary>
        /// TCP protocol.
        /// </summary>
        Tcp,

        /// <summary>
        /// UDP protocol.
        /// </summary>
        Udp,

        /// <summary>
        /// ICMP protocol.
        /// </summary>
        Icmp,

        /// <summary>
        /// Raw IP protocol.
        /// </summary>
        IP
    }

    /// <summary>
    /// Represents the state of a socket.
    /// </summary>
    public enum SocketState
    {
        /// <summary>
        /// Socket is created but not bound or connected.
        /// </summary>
        Created,

        /// <summary>
        /// Socket is bound to a local address.
        /// </summary>
        Bound,

        /// <summary>
        /// Socket is listening for connections.
        /// </summary>
        Listening,

        /// <summary>
        /// Socket is connecting to a remote endpoint.
        /// </summary>
        Connecting,

        /// <summary>
        /// Socket is connected.
        /// </summary>
        Connected,

        /// <summary>
        /// Socket is disconnected.
        /// </summary>
        Disconnected,

        

        /// <summary>
        /// Socket is closed.
        /// </summary>
        Closed,

        /// <summary>
        /// Socket encountered an error.
        /// </summary>
        Error = -1,

        /// <summary>
        /// The socket has encountered an error and failed
        /// </summary>
        Failed = -1
    }

    /// <summary>
    /// Represents socket flags for send/receive operations.
    /// </summary>
    [Flags]
    public enum SocketFlags
    {
        /// <summary>
        /// No flags.
        /// </summary>
        None = 0,

        /// <summary>
        /// Process out-of-band data.
        /// </summary>
        OutOfBand = 1,

        /// <summary>
        /// Peek at incoming data.
        /// </summary>
        Peek = 2,

        /// <summary>
        /// Wait for complete message.
        /// </summary>
        WaitAll = 4,

        /// <summary>
        /// Don't route packets.
        /// </summary>
        DontRoute = 8
    }

    /// <summary>
    /// Specifies how to shut down a socket.
    /// </summary>
    public enum SocketShutdown
    {
        /// <summary>
        /// Shutdown receive operations.
        /// </summary>
        Receive,

        /// <summary>
        /// Shutdown send operations.
        /// </summary>
        Send,

        /// <summary>
        /// Shutdown both send and receive operations.
        /// </summary>
        Both
    }

    /// <summary>
    /// Represents socket option levels.
    /// </summary>
    public enum SocketOptionLevel
    {
        /// <summary>
        /// Socket level options.
        /// </summary>
        Socket,

        /// <summary>
        /// IP level options.
        /// </summary>
        IP,

        /// <summary>
        /// TCP level options.
        /// </summary>
        Tcp,

        /// <summary>
        /// UDP level options.
        /// </summary>
        Udp
    }

    /// <summary>
    /// Represents socket option names.
    /// </summary>
    public enum SocketOptionName
    {
        /// <summary>
        /// Allow address reuse.
        /// </summary>
        ReuseAddress,

        /// <summary>
        /// Keep connection alive.
        /// </summary>
        KeepAlive,

        /// <summary>
        /// Linger on close.
        /// </summary>
        Linger,

        /// <summary>
        /// Receive buffer size.
        /// </summary>
        ReceiveBuffer,

        /// <summary>
        /// Send buffer size.
        /// </summary>
        SendBuffer,

        /// <summary>
        /// Receive timeout.
        /// </summary>
        ReceiveTimeout,

        /// <summary>
        /// Send timeout.
        /// </summary>
        SendTimeout,

        /// <summary>
        /// Socket type.
        /// </summary>
        Type,

        /// <summary>
        /// Socket error.
        /// </summary>
        Error
    }

    /// <summary>
    /// Represents a network endpoint with address and port.
    /// </summary>
    public class NetworkEndPoint
    {
        /// <summary>
        /// Gets or sets the IP address.
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the port number.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the address family.
        /// </summary>
        public AddressFamily AddressFamily { get; set; } = AddressFamily.InterNetwork;

        /// <summary>
        /// Initializes a new instance of the NetworkEndPoint class.
        /// </summary>
        public NetworkEndPoint() { }

        /// <summary>
        /// Initializes a new instance of the NetworkEndPoint class.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <param name="port">The port number.</param>
        public NetworkEndPoint(string address, int port)
        {
            Address = address;
            Port = port;
        }

        /// <summary>
        /// Returns a string representation of the endpoint.
        /// </summary>
        /// <returns>A string in the format "address:port".</returns>
        public override string ToString()
        {
            return $"{Address}:{Port}";
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the objects are equal; otherwise, false.</returns>
        public override bool Equals(object? obj)
        {
            if (obj is NetworkEndPoint other)
            {
                return Address == other.Address && Port == other.Port && AddressFamily == other.AddressFamily;
            }
            return false;
        }

        /// <summary>
        /// Returns a hash code for the current object.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Address, Port, AddressFamily);
        }    }

    /// <summary>
    /// Event arguments for socket data received events.
    /// </summary>
    public class SocketDataReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the received data.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Gets the source endpoint (for UDP sockets).
        /// </summary>
        public NetworkEndPoint? SourceEndPoint { get; }

        /// <summary>
        /// Gets the timestamp when the data was received.
        /// </summary>
        public DateTime ReceivedAt { get; }

        /// <summary>
        /// Initializes a new instance of the SocketDataReceivedEventArgs class.
        /// </summary>
        /// <param name="data">The received data.</param>
        /// <param name="sourceEndPoint">The source endpoint.</param>
        public SocketDataReceivedEventArgs(byte[] data, NetworkEndPoint? sourceEndPoint = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            SourceEndPoint = sourceEndPoint;
            ReceivedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for socket state changed events.
    /// </summary>
    public class SocketStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous socket state.
        /// </summary>
        public SocketState PreviousState { get; }

        /// <summary>
        /// Gets the new socket state.
        /// </summary>
        public SocketState NewState { get; }

        /// <summary>
        /// Gets the timestamp when the state change occurred.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the SocketStateChangedEventArgs class.
        /// </summary>
        /// <param name="previousState">The previous socket state.</param>
        /// <param name="newState">The new socket state.</param>
        public SocketStateChangedEventArgs(SocketState previousState, SocketState newState)
        {
            PreviousState = previousState;
            NewState = newState;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for socket error events.
    /// </summary>
    public class SocketErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the error that occurred.
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Gets the timestamp when the error occurred.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the SocketErrorEventArgs class.
        /// </summary>
        /// <param name="error">The error that occurred.</param>
        public SocketErrorEventArgs(Exception error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents the result of a ReceiveFromAsync operation.
    /// </summary>
    public class SocketReceiveResult
    {
        /// <summary>
        /// Gets the number of bytes received.
        /// </summary>
        public int BytesReceived { get; }

        /// <summary>
        /// Gets the source endpoint.
        /// </summary>
        public NetworkEndPoint SourceEndPoint { get; }

        /// <summary>
        /// Initializes a new instance of the SocketReceiveResult class.
        /// </summary>
        /// <param name="bytesReceived">The number of bytes received.</param>
        /// <param name="sourceEndPoint">The source endpoint.</param>
        public SocketReceiveResult(int bytesReceived, NetworkEndPoint sourceEndPoint)
        {
            BytesReceived = bytesReceived;
            SourceEndPoint = sourceEndPoint ?? throw new ArgumentNullException(nameof(sourceEndPoint));
        }
    }
}
