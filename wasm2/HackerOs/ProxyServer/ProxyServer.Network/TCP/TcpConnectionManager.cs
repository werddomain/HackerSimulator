using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using ProxyServer.Protocol.Models.Network;

namespace ProxyServer.Network.TCP
{    /// <summary>
    /// Manages TCP connections proxied from the WebSocket clients.
    /// </summary>
    public class TcpConnectionManager : IDisposable
    {
        private readonly ILogger<TcpConnectionManager> _logger;
        private readonly ConcurrentDictionary<string, TcpConnection> _connections;        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Timer _idleConnectionTimer;
        private TimeSpan _idleTimeout = TimeSpan.FromMinutes(10); // Default idle timeout of 10 minutes
        private bool _disposed;

        /// <summary>
        /// Event raised when data is received from a TCP connection.
        /// </summary>
        public event EventHandler<TcpDataReceivedEventArgs>? DataReceived;

        /// <summary>
        /// Event raised when a TCP connection status changes.
        /// </summary>
        public event EventHandler<TcpConnectionStatusEventArgs>? ConnectionStatusChanged;        /// <summary>
        /// Initializes a new instance of the <see cref="TcpConnectionManager"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public TcpConnectionManager(ILogger<TcpConnectionManager> logger)
        {
            _logger = logger;
            _connections = new ConcurrentDictionary<string, TcpConnection>();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Initialize the idle connection timer to run every minute
            _idleConnectionTimer = new Timer(CheckIdleConnections, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Creates a new TCP connection to the specified endpoint.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="host">The host to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <param name="timeout">The connection timeout in milliseconds.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The connection status.</returns>
        public async Task<ConnectionStatusMessage.Status> CreateConnectionAsync(
            string connectionId,
            string host,
            int port,
            int timeout = 30000,
            CancellationToken cancellationToken = default)
        {
            if (_connections.ContainsKey(connectionId))
            {
                _logger.LogWarning("Connection with ID {ConnectionId} already exists", connectionId);
                return ConnectionStatusMessage.Status.Error;
            }

            _logger.LogInformation("Creating TCP connection to {Host}:{Port} with ID {ConnectionId}", 
                host, port, connectionId);

            // Create a linked cancellation token that includes the timeout
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationTokenSource.Token, cancellationToken, timeoutCts.Token);

            try
            {
                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(host, port, linkedCts.Token);

                var connection = new TcpConnection(connectionId, tcpClient, host, port);
                
                // Register for the connection's events
                connection.DataReceived += OnConnectionDataReceived;
                connection.ConnectionClosed += OnConnectionClosed;

                // Store the connection
                if (_connections.TryAdd(connectionId, connection))
                {
                    // Start receiving data
                    _ = connection.StartReceivingAsync(linkedCts.Token);
                    
                    _logger.LogInformation("TCP connection established for {ConnectionId} to {Host}:{Port}", 
                        connectionId, host, port);
                    
                    // Raise the status changed event
                    OnConnectionStatusChanged(connectionId, ConnectionStatusMessage.Status.Connected);
                    
                    return ConnectionStatusMessage.Status.Connected;
                }
                else
                {
                    _logger.LogError("Failed to add connection {ConnectionId} to the connections dictionary", connectionId);
                    tcpClient.Dispose();
                    return ConnectionStatusMessage.Status.Error;
                }
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    _logger.LogError("Connection timeout for {ConnectionId} to {Host}:{Port}", 
                        connectionId, host, port);
                    
                    OnConnectionStatusChanged(connectionId, ConnectionStatusMessage.Status.TimedOut);
                    return ConnectionStatusMessage.Status.TimedOut;
                }
                
                _logger.LogInformation("Connection canceled for {ConnectionId} to {Host}:{Port}", 
                    connectionId, host, port);
                
                OnConnectionStatusChanged(connectionId, ConnectionStatusMessage.Status.Closed);
                return ConnectionStatusMessage.Status.Closed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish TCP connection for {ConnectionId} to {Host}:{Port}", 
                    connectionId, host, port);
                
                OnConnectionStatusChanged(connectionId, ConnectionStatusMessage.Status.Failed, ex.Message);
                return ConnectionStatusMessage.Status.Failed;
            }
        }

        /// <summary>
        /// Sends data to an existing TCP connection.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="data">The data to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the data was sent successfully, false otherwise.</returns>
        public async Task<bool> SendDataAsync(
            string connectionId, 
            byte[] data, 
            CancellationToken cancellationToken = default)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                try
                {
                    await connection.SendAsync(data, cancellationToken);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send data to connection {ConnectionId}", connectionId);
                    await CloseConnectionAsync(connectionId, ex.Message);
                    return false;
                }
            }
            else
            {
                _logger.LogWarning("Attempted to send data to non-existent connection {ConnectionId}", connectionId);
                return false;
            }
        }

        /// <summary>
        /// Closes a TCP connection.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="reason">The reason for closing the connection.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task CloseConnectionAsync(string connectionId, string? reason = null)
        {
            if (_connections.TryRemove(connectionId, out var connection))
            {
                _logger.LogInformation("Closing TCP connection {ConnectionId}. Reason: {Reason}", 
                    connectionId, reason ?? "Client requested");
                
                connection.DataReceived -= OnConnectionDataReceived;
                connection.ConnectionClosed -= OnConnectionClosed;
                
                await connection.CloseAsync();
                OnConnectionStatusChanged(connectionId, ConnectionStatusMessage.Status.Closed, reason);
            }
        }

        /// <summary>
        /// Gets a connection by its ID.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <returns>The connection, or null if it does not exist.</returns>
        public TcpConnection? GetConnection(string connectionId)
        {
            _connections.TryGetValue(connectionId, out var connection);
            return connection;
        }

        /// <summary>
        /// Gets all active connections.
        /// </summary>
        /// <returns>An enumerable of active connections.</returns>
        public IEnumerable<TcpConnection> GetAllConnections()
        {
            return _connections.Values.ToList();
        }

        /// <summary>
        /// Gets the number of active connections.
        /// </summary>
        public int ConnectionCount => _connections.Count;

        /// <summary>
        /// Gets connection statistics summary.
        /// </summary>
        /// <returns>A summary of connection statistics.</returns>
        public ConnectionStatisticsSummary GetConnectionStatistics()
        {
            var connections = _connections.Values.ToList();
            if (connections.Count == 0)
            {
                return new ConnectionStatisticsSummary();
            }

            return new ConnectionStatisticsSummary
            {
                ActiveConnections = connections.Count,
                TotalBytesSent = connections.Sum(c => c.BytesSent),
                TotalBytesReceived = connections.Sum(c => c.BytesReceived),
                TotalSendOperations = connections.Sum(c => c.SendOperationCount),
                TotalReceiveOperations = connections.Sum(c => c.ReceiveOperationCount),
                AverageConnectionDuration = connections.Count > 0 
                    ? TimeSpan.FromTicks((long)connections.Average(c => c.ConnectionDuration.Ticks))
                    : TimeSpan.Zero,
                OldestConnectionAge = connections.Max(c => c.ConnectionDuration),
                NewestConnectionAge = connections.Min(c => c.ConnectionDuration)
            };
        }

        /// <summary>
        /// Gets connections that are approaching the idle timeout.
        /// </summary>
        /// <param name="warningThreshold">The threshold (as a percentage of idle timeout) to warn about idle connections.</param>
        /// <returns>Connections that are approaching idle timeout.</returns>
        public IEnumerable<TcpConnection> GetConnectionsApproachingIdleTimeout(double warningThreshold = 0.8)
        {
            if (warningThreshold <= 0 || warningThreshold >= 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(warningThreshold), "Warning threshold must be between 0 and 1.");
            }

            var now = DateTime.UtcNow;
            var warningTimeout = TimeSpan.FromTicks((long)(_idleTimeout.Ticks * warningThreshold));
            
            return _connections.Values
                .Where(c => (now - c.LastActivity) > warningTimeout)
                .ToList();
        }

        /// <summary>
        /// Closes all idle connections immediately.
        /// </summary>
        /// <returns>The number of connections that were closed.</returns>
        public async Task<int> CloseIdleConnectionsAsync()
        {
            var now = DateTime.UtcNow;
            var idleConnections = _connections.Values
                .Where(c => (now - c.LastActivity) > _idleTimeout)
                .ToList();

            foreach (var connection in idleConnections)
            {
                await CloseConnectionAsync(connection.ConnectionId, "Manual idle timeout cleanup");
            }

            return idleConnections.Count;
        }

        /// <summary>
        /// Sets the idle timeout for connections.
        /// </summary>
        /// <param name="timeout">The idle timeout.</param>
        public void SetIdleTimeout(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
            }
            
            // Using volatile field for thread-safe read/write of the timeout value
            _idleTimeout = timeout;
            
            _logger.LogInformation("Idle connection timeout set to {Timeout}", timeout);
        }
        
        /// <summary>
        /// Checks for and closes idle connections.
        /// </summary>
        private async void CheckIdleConnections(object? state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var idleConnections = _connections.Values
                    .Where(c => (now - c.LastActivity) > _idleTimeout)
                    .ToList();
                
                if (idleConnections.Count > 0)
                {
                    _logger.LogInformation("Found {Count} idle connections to close", idleConnections.Count);
                    
                    foreach (var connection in idleConnections)
                    {
                        await CloseConnectionAsync(connection.ConnectionId, "Connection idle timeout exceeded");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for idle connections");
            }
        }

        private void OnConnectionDataReceived(object? sender, TcpDataReceivedEventArgs e)
        {
            DataReceived?.Invoke(this, e);
        }

        private void OnConnectionClosed(object? sender, TcpConnectionEventArgs e)
        {
            if (_connections.TryRemove(e.ConnectionId, out var connection))
            {
                _logger.LogInformation("TCP connection {ConnectionId} closed by remote host", e.ConnectionId);
                
                connection.DataReceived -= OnConnectionDataReceived;
                connection.ConnectionClosed -= OnConnectionClosed;
                
                OnConnectionStatusChanged(e.ConnectionId, ConnectionStatusMessage.Status.Closed, "Connection closed by remote host");
            }
        }

        private void OnConnectionStatusChanged(
            string connectionId, 
            ConnectionStatusMessage.Status status, 
            string? details = null)
        {
            ConnectionStatusChanged?.Invoke(this, new TcpConnectionStatusEventArgs(connectionId, status, details));
        }

        /// <summary>
        /// Disposes all connections and resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }        /// <summary>
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
                _cancellationTokenSource.Cancel();
                
                // Dispose the idle connection timer
                _idleConnectionTimer?.Dispose();
                
                foreach (var connection in _connections.Values)
                {
                    connection.Dispose();
                }
                _connections.Clear();
                
                _cancellationTokenSource.Dispose();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Summary of connection statistics.
    /// </summary>
    public class ConnectionStatisticsSummary
    {
        /// <summary>
        /// Gets or sets the number of active connections.
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Gets or sets the total bytes sent across all connections.
        /// </summary>
        public long TotalBytesSent { get; set; }

        /// <summary>
        /// Gets or sets the total bytes received across all connections.
        /// </summary>
        public long TotalBytesReceived { get; set; }

        /// <summary>
        /// Gets or sets the total number of send operations.
        /// </summary>
        public int TotalSendOperations { get; set; }

        /// <summary>
        /// Gets or sets the total number of receive operations.
        /// </summary>
        public int TotalReceiveOperations { get; set; }

        /// <summary>
        /// Gets or sets the average connection duration.
        /// </summary>
        public TimeSpan AverageConnectionDuration { get; set; }

        /// <summary>
        /// Gets or sets the age of the oldest connection.
        /// </summary>
        public TimeSpan OldestConnectionAge { get; set; }

        /// <summary>
        /// Gets or sets the age of the newest connection.
        /// </summary>
        public TimeSpan NewestConnectionAge { get; set; }

        /// <summary>
        /// Gets the total data transferred (sent + received).
        /// </summary>
        public long TotalDataTransferred => TotalBytesSent + TotalBytesReceived;

        /// <summary>
        /// Gets the average send operation size.
        /// </summary>
        public double AverageSendSize => TotalSendOperations > 0 ? (double)TotalBytesSent / TotalSendOperations : 0;

        /// <summary>
        /// Gets the average receive operation size.
        /// </summary>
        public double AverageReceiveSize => TotalReceiveOperations > 0 ? (double)TotalBytesReceived / TotalReceiveOperations : 0;
    }
}
