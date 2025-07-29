using System.Net.Sockets;

namespace ProxyServer.Network.TCP
{
    /// <summary>
    /// Represents a TCP connection proxied from a client.
    /// </summary>
    public class TcpConnection : IDisposable
    {
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1, 1);
        private bool _disposed;

        /// <summary>
        /// Gets the connection identifier.
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// Gets the host this connection is connected to.
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// Gets the port this connection is connected to.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets the time this connection was created.
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Gets the last activity time of this connection.
        /// </summary>
        public DateTime LastActivity { get; private set; }        /// <summary>
        /// Gets the total bytes sent through this connection.
        /// </summary>
        public long BytesSent { get; private set; }

        /// <summary>
        /// Gets the total bytes received through this connection.
        /// </summary>
        public long BytesReceived { get; private set; }
        
        /// <summary>
        /// Gets the number of send operations performed.
        /// </summary>
        public int SendOperationCount { get; private set; }
        
        /// <summary>
        /// Gets the number of receive operations performed.
        /// </summary>
        public int ReceiveOperationCount { get; private set; }
        
        /// <summary>
        /// Gets the average send operation size in bytes.
        /// </summary>
        public double AverageSendSize => SendOperationCount > 0 ? (double)BytesSent / SendOperationCount : 0;
        
        /// <summary>
        /// Gets the average receive operation size in bytes.
        /// </summary>
        public double AverageReceiveSize => ReceiveOperationCount > 0 ? (double)BytesReceived / ReceiveOperationCount : 0;
        
        /// <summary>
        /// Gets the duration of the connection.
        /// </summary>
        public TimeSpan ConnectionDuration => DateTime.UtcNow - CreatedAt;

        /// <summary>
        /// Gets a value indicating whether the connection is active.
        /// </summary>
        public bool IsActive => _tcpClient.Connected;

        /// <summary>
        /// Gets the connection health status.
        /// </summary>
        public ConnectionHealth Health
        {
            get
            {
                if (!IsActive)
                    return ConnectionHealth.Disconnected;

                var timeSinceLastActivity = DateTime.UtcNow - LastActivity;
                if (timeSinceLastActivity > TimeSpan.FromMinutes(5))
                    return ConnectionHealth.Stale;
                if (timeSinceLastActivity > TimeSpan.FromMinutes(1))
                    return ConnectionHealth.Idle;

                return ConnectionHealth.Active;
            }
        }

        /// <summary>
        /// Gets the remote endpoint information.
        /// </summary>
        public string RemoteEndPoint => $"{Host}:{Port}";

        /// <summary>
        /// Gets the local endpoint information if available.
        /// </summary>
        public string? LocalEndPoint
        {
            get
            {
                try
                {
                    return _tcpClient.Client?.LocalEndPoint?.ToString();
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Event raised when data is received from the TCP connection.
        /// </summary>
        public event EventHandler<TcpDataReceivedEventArgs>? DataReceived;

        /// <summary>
        /// Event raised when the TCP connection is closed by the remote host.
        /// </summary>
        public event EventHandler<TcpConnectionEventArgs>? ConnectionClosed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpConnection"/> class.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="tcpClient">The TCP client.</param>
        /// <param name="host">The host.</param>
        /// <param name="port">The port.</param>
        public TcpConnection(string connectionId, TcpClient tcpClient, string host, int port)
        {
            ConnectionId = connectionId;
            _tcpClient = tcpClient;
            _stream = tcpClient.GetStream();
            Host = host;
            Port = port;
            CreatedAt = DateTime.UtcNow;
            LastActivity = CreatedAt;
            BytesSent = 0;
            BytesReceived = 0;
        }

        /// <summary>
        /// Sends data to the TCP connection.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (!_tcpClient.Connected)
            {
                throw new InvalidOperationException("TCP connection is not active");
            }

            await _sendSemaphore.WaitAsync(cancellationToken);
            try
            {                await _stream.WriteAsync(data, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
                
                BytesSent += data.Length;
                SendOperationCount++;
                LastActivity = DateTime.UtcNow;
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        /// <summary>
        /// Starts receiving data from the TCP connection asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StartReceivingAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var buffer = new byte[4096];

                while (_tcpClient.Connected && !cancellationToken.IsCancellationRequested)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Normal cancellation
                        break;
                    }
                    catch (IOException)
                    {
                        // Connection was closed
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        // End of stream, connection was closed by remote host
                        break;
                    }                    BytesReceived += bytesRead;
                    ReceiveOperationCount++;
                    LastActivity = DateTime.UtcNow;

                    // Copy the received data to a new buffer
                    var data = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);

                    // Raise the data received event
                    OnDataReceived(data);
                }

                // If we got here, the connection was closed
                OnConnectionClosed();
            }
            catch (Exception ex)
            {
                // Handle any other exceptions
                OnConnectionClosed(ex.Message);
            }
        }

        /// <summary>
        /// Closes the TCP connection.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task CloseAsync()
        {
            if (_tcpClient.Connected)
            {
                _tcpClient.Close();
            }

            return Task.CompletedTask;
        }

        private void OnDataReceived(byte[] data)
        {
            DataReceived?.Invoke(this, new TcpDataReceivedEventArgs(ConnectionId, data));
        }

        private void OnConnectionClosed(string? reason = null)
        {
            ConnectionClosed?.Invoke(this, new TcpConnectionEventArgs(ConnectionId, reason));
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _sendSemaphore.Dispose();
                _stream.Dispose();
                _tcpClient.Dispose();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Event arguments for TCP connection events.
    /// </summary>
    public class TcpConnectionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the connection identifier.
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// Gets the reason for the event.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpConnectionEventArgs"/> class.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="reason">The reason for the event.</param>
        public TcpConnectionEventArgs(string connectionId, string? reason = null)
        {
            ConnectionId = connectionId;
            Reason = reason;
        }
    }

    /// <summary>
    /// Event arguments for TCP data received events.
    /// </summary>
    public class TcpDataReceivedEventArgs : TcpConnectionEventArgs
    {
        /// <summary>
        /// Gets the received data.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpDataReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="data">The received data.</param>
        public TcpDataReceivedEventArgs(string connectionId, byte[] data) : base(connectionId)
        {
            Data = data;
        }
    }

    /// <summary>
    /// Event arguments for TCP connection status change events.
    /// </summary>
    public class TcpConnectionStatusEventArgs : TcpConnectionEventArgs
    {
        /// <summary>
        /// Gets the connection status.
        /// </summary>
        public Protocol.Models.Network.ConnectionStatusMessage.Status Status { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpConnectionStatusEventArgs"/> class.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="status">The connection status.</param>
        /// <param name="reason">The reason for the status change.</param>
        public TcpConnectionStatusEventArgs(
            string connectionId, 
            Protocol.Models.Network.ConnectionStatusMessage.Status status,
            string? reason = null) : base(connectionId, reason)
        {
            Status = status;
        }
    }

    /// <summary>
    /// Represents the health status of a TCP connection.
    /// </summary>
    public enum ConnectionHealth
    {
        /// <summary>
        /// Connection is active and recently used.
        /// </summary>
        Active,

        /// <summary>
        /// Connection is established but idle for a moderate time.
        /// </summary>
        Idle,

        /// <summary>
        /// Connection is established but hasn't been used for a long time.
        /// </summary>
        Stale,

        /// <summary>
        /// Connection is not active or has been closed.
        /// </summary>
        Disconnected
    }
}
