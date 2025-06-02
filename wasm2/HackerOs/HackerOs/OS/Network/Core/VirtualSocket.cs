using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HackerOs.OS.Network.Core
{
    /// <summary>
    /// Represents a virtual socket in the HackerOS network stack.
    /// Simulates a network socket with connect, send, and receive operations.
    /// </summary>
    public class VirtualSocket : ISocket
    {
        private readonly ConcurrentQueue<byte[]> _receiveQueue;
        private readonly SemaphoreSlim _receiveSemaphore;
        private readonly object _stateLock = new object();
        private SocketState _state;
        private NetworkEndPoint? _localEndPoint;
        private NetworkEndPoint? _remoteEndPoint;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualSocket"/> class.
        /// </summary>
        /// <param name="socketId">The socket identifier.</param>
        /// <param name="socketType">Type of the socket.</param>
        /// <param name="addressFamily">The address family.</param>
        /// <param name="protocol">The protocol.</param>
        public VirtualSocket(string socketId, SocketType socketType, AddressFamily addressFamily, ProtocolType protocol)
        {
            SocketId = socketId;
            SocketType = socketType;
            AddressFamily = addressFamily;
            Protocol = protocol;
            _state = SocketState.Created;
            _receiveQueue = new ConcurrentQueue<byte[]>();
            _receiveSemaphore = new SemaphoreSlim(0);
            _disposed = false;
        }

        /// <inheritdoc/>
        public string SocketId { get; }

        /// <inheritdoc/>
        public SocketType SocketType { get; }

        /// <inheritdoc/>
        public AddressFamily AddressFamily { get; }

        /// <inheritdoc/>
        public ProtocolType Protocol { get; }

        /// <inheritdoc/>
        public SocketState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    var oldState = _state;
                    _state = value;
                    OnStateChanged(new SocketStateEventArgs(this, oldState, _state));
                }
            }
        }

        /// <inheritdoc/>
        public NetworkEndPoint? LocalEndPoint => _localEndPoint;

        /// <inheritdoc/>
        public NetworkEndPoint? RemoteEndPoint => _remoteEndPoint;

        /// <inheritdoc/>
        public bool IsConnected => State == SocketState.Connected;

        /// <inheritdoc/>
        public bool IsBound => State == SocketState.Bound || State == SocketState.Listening || State == SocketState.Connected;

        /// <inheritdoc/>
        public bool IsListening => State == SocketState.Listening;

        /// <inheritdoc/>
        public event EventHandler<SocketStateEventArgs>? StateChanged;

        /// <inheritdoc/>
        public event EventHandler<SocketDataEventArgs>? DataReceived;

        /// <inheritdoc/>
        public async Task<bool> BindAsync(NetworkEndPoint localEndPoint)
        {
            ThrowIfDisposed();

            if (IsBound)
            {
                throw new InvalidOperationException("Socket is already bound");
            }

            lock (_stateLock)
            {
                _localEndPoint = localEndPoint;
                State = SocketState.Bound;
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> ConnectAsync(NetworkEndPoint remoteEndPoint)
        {
            ThrowIfDisposed();

            if (IsConnected)
            {
                throw new InvalidOperationException("Socket is already connected");
            }

            if (!IsBound)
            {
                // Auto-bind to a random local port if not already bound
                await BindAsync(new NetworkEndPoint("0.0.0.0", GetRandomPort()));
            }

            try
            {
                // Simulate connection delay
                await Task.Delay(SimulateConnectionDelay(remoteEndPoint));

                lock (_stateLock)
                {
                    _remoteEndPoint = remoteEndPoint;
                    State = SocketState.Connected;
                }

                return true;
            }
            catch
            {
                State = SocketState.Failed;
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ListenAsync(int backlog)
        {
            ThrowIfDisposed();

            if (!IsBound)
            {
                throw new InvalidOperationException("Socket must be bound before listening");
            }

            if (IsConnected)
            {
                throw new InvalidOperationException("Socket is already connected");
            }

            lock (_stateLock)
            {
                State = SocketState.Listening;
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task<ISocket> AcceptAsync()
        {
            ThrowIfDisposed();

            if (!IsListening)
            {
                throw new InvalidOperationException("Socket is not listening");
            }

            // This would normally block until a connection is available
            // For our simulation, we'll create a new socket and simulate a connection
            
            // Wait for a simulated connection (timeout after 30 seconds)
            var connectionReceived = await Task.Run(() => {
                // Simulate waiting for a connection
                return Task.Delay(new Random().Next(100, 5000)).ContinueWith(_ => true);
            }).WaitAsync(TimeSpan.FromSeconds(30));

            if (!connectionReceived)
            {
                throw new TimeoutException("Accept operation timed out");
            }

            // Create a new connected socket
            var acceptedSocketId = Guid.NewGuid().ToString();
            var acceptedSocket = new VirtualSocket(acceptedSocketId, SocketType, AddressFamily, Protocol);
            
            // Simulate client endpoint
            var clientPort = GetRandomPort();
            var clientIp = "192.168.1." + new Random().Next(2, 254);
            var clientEndpoint = new NetworkEndPoint(clientIp, clientPort);
            
            // Connect it
            await acceptedSocket.BindAsync(new NetworkEndPoint(LocalEndPoint!.Address, LocalEndPoint.Port));
            
            // Set its remote endpoint and state directly
            (acceptedSocket as VirtualSocket)!.SetRemoteEndPoint(clientEndpoint);
            (acceptedSocket as VirtualSocket)!.SetState(SocketState.Connected);
            
            return acceptedSocket;
        }

        /// <inheritdoc/>
        public async Task<int> SendAsync(byte[] buffer, int offset, int size)
        {
            ThrowIfDisposed();

            if (!IsConnected)
            {
                throw new InvalidOperationException("Socket is not connected");
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("Invalid offset or size");
            }

            try
            {
                // Simulate send delay based on data size
                await Task.Delay(SimulateSendDelay(size));

                // For UDP, we just pretend the data was sent
                if (SocketType == SocketType.Dgram)
                {
                    return size;
                }

                // For TCP, we'd normally have acknowledgments, etc.
                // But for our simulation, we'll just pretend it succeeded
                
                // Create a network packet (useful for network analysis tools)
                var packet = new NetworkPacket
                {
                    SourceAddress = LocalEndPoint!.Address,
                    SourcePort = LocalEndPoint.Port,
                    DestinationAddress = RemoteEndPoint!.Address,
                    DestinationPort = RemoteEndPoint.Port,
                    Protocol = this.Protocol,
                    Data = new byte[size]
                };
                
                // Copy the data to send
                Array.Copy(buffer, offset, packet.Data, 0, size);

                // In a real implementation, we'd send this via the network stack
                // For now, just return success
                return size;
            }
            catch
            {
                State = SocketState.Failed;
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> ReceiveAsync(byte[] buffer, int offset, int size)
        {
            ThrowIfDisposed();

            if (!IsConnected)
            {
                throw new InvalidOperationException("Socket is not connected");
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("Invalid offset or size");
            }

            try
            {
                // Wait for data to be available
                await _receiveSemaphore.WaitAsync();

                // Try to get data from the queue
                if (_receiveQueue.TryDequeue(out var data))
                {
                    // Determine how much data to copy
                    int bytesToCopy = Math.Min(size, data.Length);
                    Array.Copy(data, 0, buffer, offset, bytesToCopy);

                    // If we didn't use all the data, put the rest back
                    if (bytesToCopy < data.Length)
                    {
                        var remaining = new byte[data.Length - bytesToCopy];
                        Array.Copy(data, bytesToCopy, remaining, 0, remaining.Length);
                        EnqueueData(remaining);
                    }

                    return bytesToCopy;
                }

                return 0;
            }
            catch
            {
                State = SocketState.Failed;
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> CloseAsync()
        {
            if (_disposed)
            {
                return true;
            }

            try
            {
                // Simulate close operation
                if (IsConnected)
                {
                    // For TCP, we'd normally send a FIN packet
                    await Task.Delay(10); // Small delay to simulate TCP close
                }

                // Update state
                State = SocketState.Closed;
                
                return true;
            }
            catch
            {
                State = SocketState.Failed;
                return false;
            }
            finally
            {
                Dispose();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            State = SocketState.Closed;
            _receiveSemaphore.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Enqueues data to be received by this socket.
        /// </summary>
        /// <param name="data">The data to enqueue.</param>
        public void EnqueueData(byte[] data)
        {
            ThrowIfDisposed();

            if (!IsConnected)
            {
                throw new InvalidOperationException("Socket is not connected");
            }

            _receiveQueue.Enqueue(data);
            _receiveSemaphore.Release();
            
            OnDataReceived(new SocketDataEventArgs(this, data.Length));
        }

        /// <summary>
        /// Sets the remote endpoint directly (for internal use).
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint.</param>
        internal void SetRemoteEndPoint(NetworkEndPoint remoteEndPoint)
        {
            _remoteEndPoint = remoteEndPoint;
        }

        /// <summary>
        /// Sets the socket state directly (for internal use).
        /// </summary>
        /// <param name="state">The new state.</param>
        internal void SetState(SocketState state)
        {
            State = state;
        }

        /// <summary>
        /// Throws an exception if the socket is disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Socket is disposed");
            }
        }

        /// <summary>
        /// Simulates connection delay based on endpoint.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint.</param>
        /// <returns>The delay in milliseconds.</returns>
        private int SimulateConnectionDelay(NetworkEndPoint remoteEndPoint)
        {
            // Localhost connections are fast
            if (remoteEndPoint.Address == "127.0.0.1" || remoteEndPoint.Address == "localhost")
            {
                return new Random().Next(1, 5);
            }

            // Local network is moderately fast
            if (remoteEndPoint.Address.StartsWith("192.168.") || remoteEndPoint.Address.StartsWith("10."))
            {
                return new Random().Next(5, 50);
            }

            // External connections have more latency
            return new Random().Next(50, 500);
        }

        /// <summary>
        /// Simulates send delay based on data size.
        /// </summary>
        /// <param name="dataSize">Size of the data.</param>
        /// <returns>The delay in milliseconds.</returns>
        private int SimulateSendDelay(int dataSize)
        {
            // Base delay
            int baseDelay = 1;
            
            // Add delay based on data size (roughly 1ms per KB)
            int sizeDelay = dataSize / 1000;
            
            // Add some randomness
            int jitter = new Random().Next(0, 10);
            
            return baseDelay + sizeDelay + jitter;
        }

        /// <summary>
        /// Gets a random port number for ephemeral ports.
        /// </summary>
        /// <returns>A random port number.</returns>
        private int GetRandomPort()
        {
            // Ephemeral ports typically in range 49152-65535
            return new Random().Next(49152, 65535);
        }

        /// <summary>
        /// Raises the state changed event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnStateChanged(SocketStateEventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the data received event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnDataReceived(SocketDataEventArgs e)
        {
            DataReceived?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Represents a network endpoint (IP address and port).
    /// </summary>
    public class NetworkEndPoint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkEndPoint"/> class.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <param name="port">The port number.</param>
        public NetworkEndPoint(string address, int port)
        {
            Address = address;
            Port = port;
        }

        /// <summary>
        /// Gets the IP address.
        /// </summary>
        public string Address { get; }

        /// <summary>
        /// Gets the port number.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Returns a string representation of this endpoint.
        /// </summary>
        /// <returns>A string in the format "address:port".</returns>
        public override string ToString() => $"{Address}:{Port}";
    }

    /// <summary>
    /// Event arguments for socket state changes.
    /// </summary>
    public class SocketStateEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SocketStateEventArgs"/> class.
        /// </summary>
        /// <param name="socket">The socket.</param>
        /// <param name="oldState">The old state.</param>
        /// <param name="newState">The new state.</param>
        public SocketStateEventArgs(ISocket socket, SocketState oldState, SocketState newState)
        {
            Socket = socket;
            OldState = oldState;
            NewState = newState;
        }

        /// <summary>
        /// Gets the socket.
        /// </summary>
        public ISocket Socket { get; }

        /// <summary>
        /// Gets the old state.
        /// </summary>
        public SocketState OldState { get; }

        /// <summary>
        /// Gets the new state.
        /// </summary>
        public SocketState NewState { get; }
    }

    /// <summary>
    /// Event arguments for socket data received.
    /// </summary>
    public class SocketDataEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SocketDataEventArgs"/> class.
        /// </summary>
        /// <param name="socket">The socket.</param>
        /// <param name="bytesReceived">The number of bytes received.</param>
        public SocketDataEventArgs(ISocket socket, int bytesReceived)
        {
            Socket = socket;
            BytesReceived = bytesReceived;
        }

        /// <summary>
        /// Gets the socket.
        /// </summary>
        public ISocket Socket { get; }

        /// <summary>
        /// Gets the number of bytes received.
        /// </summary>
        public int BytesReceived { get; }
    }
}
