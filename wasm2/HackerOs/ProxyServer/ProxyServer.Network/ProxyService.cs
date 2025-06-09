using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ProxyServer.Network.TCP;
using ProxyServer.Network.WebSockets;
using ProxyServer.Protocol.Models;
using ProxyServer.Protocol.Models.Control;
using ProxyServer.Protocol.Models.Network;

namespace ProxyServer.Network
{
    /// <summary>
    /// Service that proxies WebSocket connections to TCP connections.
    /// </summary>
    public class ProxyService : IDisposable
    {
        private readonly ILogger<ProxyService> _logger;
        private readonly WebSocketServer _webSocketServer;
        private readonly TcpConnectionManager _tcpConnectionManager;
        private readonly ConcurrentDictionary<string, string> _connectionToClientMap;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProxyService"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="webSocketServer">The WebSocket server.</param>
        /// <param name="tcpConnectionManager">The TCP connection manager.</param>
        public ProxyService(
            ILogger<ProxyService> logger,
            WebSocketServer webSocketServer,
            TcpConnectionManager tcpConnectionManager)
        {
            _logger = logger;
            _webSocketServer = webSocketServer;
            _tcpConnectionManager = tcpConnectionManager;
            _connectionToClientMap = new ConcurrentDictionary<string, string>();

            // Wire up the TCP connection manager events
            _tcpConnectionManager.DataReceived += OnTcpDataReceived;
            _tcpConnectionManager.ConnectionStatusChanged += OnTcpConnectionStatusChanged;

            // Register WebSocket message handlers
            RegisterMessageHandlers();
        }

        /// <summary>
        /// Starts the proxy service.
        /// </summary>
        /// <param name="port">The WebSocket server port.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task StartAsync(int port, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting proxy service on port {Port}", port);
            return _webSocketServer.StartAsync(port, cancellationToken);
        }

        /// <summary>
        /// Stops the proxy service.
        /// </summary>
        public void Stop()
        {
            _logger.LogInformation("Stopping proxy service");
            _webSocketServer.Stop();
        }

        /// <summary>
        /// Gets the number of active WebSocket clients.
        /// </summary>
        public int ClientCount => _webSocketServer.ClientCount;

        /// <summary>
        /// Gets the number of active TCP connections.
        /// </summary>
        public int ConnectionCount => _tcpConnectionManager.ConnectionCount;

        /// <summary>
        /// Gets comprehensive connection statistics.
        /// </summary>
        /// <returns>A summary of all connection statistics.</returns>
        public ConnectionStatisticsSummary GetConnectionStatistics()
        {
            return _tcpConnectionManager.GetConnectionStatistics();
        }

        /// <summary>
        /// Gets detailed information about all active connections.
        /// </summary>
        /// <returns>A collection of connection details.</returns>
        public IEnumerable<ConnectionInfo> GetActiveConnections()
        {
            return _tcpConnectionManager.GetAllConnections()
                .Select(conn => new ConnectionInfo
                {
                    ConnectionId = conn.ConnectionId,
                    RemoteEndPoint = conn.RemoteEndPoint,
                    LocalEndPoint = conn.LocalEndPoint,
                    CreatedAt = conn.CreatedAt,
                    LastActivity = conn.LastActivity,
                    BytesSent = conn.BytesSent,
                    BytesReceived = conn.BytesReceived,
                    SendOperationCount = conn.SendOperationCount,
                    ReceiveOperationCount = conn.ReceiveOperationCount,
                    Health = conn.Health,
                    ConnectionDuration = conn.ConnectionDuration,
                    IsActive = conn.IsActive,
                    ClientId = _connectionToClientMap.TryGetValue(conn.ConnectionId, out var clientId) ? clientId : null
                })
                .ToList();
        }

        /// <summary>
        /// Gets connections that are approaching the idle timeout.
        /// </summary>
        /// <param name="warningThreshold">The threshold (as a percentage of idle timeout) to warn about idle connections.</param>
        /// <returns>Connections that are approaching idle timeout.</returns>
        public IEnumerable<ConnectionInfo> GetConnectionsApproachingIdleTimeout(double warningThreshold = 0.8)
        {
            var approachingConnections = _tcpConnectionManager.GetConnectionsApproachingIdleTimeout(warningThreshold);
            return approachingConnections.Select(conn => new ConnectionInfo
                {
                    ConnectionId = conn.ConnectionId,
                    RemoteEndPoint = conn.RemoteEndPoint,
                    LocalEndPoint = conn.LocalEndPoint,
                    CreatedAt = conn.CreatedAt,
                    LastActivity = conn.LastActivity,
                    BytesSent = conn.BytesSent,
                    BytesReceived = conn.BytesReceived,
                    SendOperationCount = conn.SendOperationCount,
                    ReceiveOperationCount = conn.ReceiveOperationCount,
                    Health = conn.Health,
                    ConnectionDuration = conn.ConnectionDuration,
                    IsActive = conn.IsActive,
                    ClientId = _connectionToClientMap.TryGetValue(conn.ConnectionId, out var clientId) ? clientId : null
                })
                .ToList();
        }

        /// <summary>
        /// Manually closes idle connections and returns the count of closed connections.
        /// </summary>
        /// <returns>The number of connections that were closed.</returns>
        public async Task<int> CloseIdleConnectionsAsync()
        {
            return await _tcpConnectionManager.CloseIdleConnectionsAsync();
        }

        /// <summary>
        /// Sets the idle timeout for TCP connections.
        /// </summary>
        /// <param name="timeout">The idle timeout to set.</param>
        public void SetIdleTimeout(TimeSpan timeout)
        {
            _tcpConnectionManager.SetIdleTimeout(timeout);
        }

        private void RegisterMessageHandlers()
        {
            // Handle TCP connect requests
            _webSocketServer.RegisterMessageHandler(MessageType.CONNECT_TCP, async (message, client) =>
            {
                if (message is TcpConnectMessage connectMessage)
                {
                    _logger.LogInformation("Received connect request from client {ClientId} to {Host}:{Port} with connection ID {ConnectionId}",
                        client.ClientId, connectMessage.Host, connectMessage.Port, connectMessage.ConnectionId);

                    // Store the mapping between connection ID and client ID
                    _connectionToClientMap[connectMessage.ConnectionId] = client.ClientId;

                    // Create the TCP connection
                    var status = await _tcpConnectionManager.CreateConnectionAsync(
                        connectMessage.ConnectionId,
                        connectMessage.Host,
                        connectMessage.Port,
                        connectMessage.Timeout);

                    // Send connection status to the client
                    await client.SendMessageAsync(new ConnectionStatusMessage(
                        connectMessage.ConnectionId,
                        status));
                }
            });

            // Handle data messages
            _webSocketServer.RegisterMessageHandler(MessageType.SEND_DATA, async (message, client) =>
            {
                if (message is DataMessage dataMessage)
                {
                    _logger.LogDebug("Received data from client {ClientId} for connection {ConnectionId}, {DataLength} bytes",
                        client.ClientId, dataMessage.ConnectionId, dataMessage.Data.Length);

                    if (_connectionToClientMap.TryGetValue(dataMessage.ConnectionId, out var clientId))
                    {
                        if (clientId != client.ClientId)
                        {
                            _logger.LogWarning("Client {ClientId} attempted to send data to connection {ConnectionId} owned by client {OwnerClientId}",
                                client.ClientId, dataMessage.ConnectionId, clientId);
                            
                            await client.SendMessageAsync(ErrorMessage.Failure(
                                $"Connection {dataMessage.ConnectionId} is not owned by this client"));
                            return;
                        }

                        // Send the data to the TCP connection
                        var data = dataMessage.GetBinaryData();
                        var success = await _tcpConnectionManager.SendDataAsync(dataMessage.ConnectionId, data);
                        
                        if (!success)
                        {
                            await client.SendMessageAsync(ErrorMessage.Failure(
                                $"Failed to send data to connection {dataMessage.ConnectionId}"));
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Received data for unknown connection {ConnectionId} from client {ClientId}",
                            dataMessage.ConnectionId, client.ClientId);
                        
                        await client.SendMessageAsync(ErrorMessage.Failure(
                            $"Connection {dataMessage.ConnectionId} does not exist"));
                    }
                }
            });

            // Handle close connection requests
            _webSocketServer.RegisterMessageHandler(MessageType.CLOSE_CONNECTION, async (message, client) =>
            {
                if (message is CloseConnectionMessage closeMessage)
                {
                    _logger.LogInformation("Received close request from client {ClientId} for connection {ConnectionId}",
                        client.ClientId, closeMessage.ConnectionId);

                    if (_connectionToClientMap.TryGetValue(closeMessage.ConnectionId, out var clientId))
                    {
                        if (clientId != client.ClientId)
                        {
                            _logger.LogWarning("Client {ClientId} attempted to close connection {ConnectionId} owned by client {OwnerClientId}",
                                client.ClientId, closeMessage.ConnectionId, clientId);
                            
                            await client.SendMessageAsync(ErrorMessage.Failure(
                                $"Connection {closeMessage.ConnectionId} is not owned by this client"));
                            return;
                        }

                        // Close the TCP connection
                        await _tcpConnectionManager.CloseConnectionAsync(closeMessage.ConnectionId, closeMessage.Reason);
                        
                        // Remove the mapping
                        _connectionToClientMap.TryRemove(closeMessage.ConnectionId, out _);
                    }
                    else
                    {
                        _logger.LogWarning("Received close request for unknown connection {ConnectionId} from client {ClientId}",
                            closeMessage.ConnectionId, client.ClientId);
                        
                        await client.SendMessageAsync(ErrorMessage.Failure(
                            $"Connection {closeMessage.ConnectionId} does not exist"));
                    }
                }
            });
        }

        private async void OnTcpDataReceived(object? sender, TcpDataReceivedEventArgs e)
        {
            if (_connectionToClientMap.TryGetValue(e.ConnectionId, out var clientId))
            {
                _logger.LogDebug("Received {DataLength} bytes from TCP connection {ConnectionId}, forwarding to client {ClientId}",
                    e.Data.Length, e.ConnectionId, clientId);
                
                // Create a data message
                var dataMessage = DataMessage.FromBinaryData(e.ConnectionId, e.Data);
                
                try
                {
                    // Broadcast to all WebSocket clients would go here, but for security,
                    // we only send to the client that owns the connection
                    foreach (var client in GetWebSocketClientsById(clientId))
                    {
                        await client.SendMessageAsync(dataMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send TCP data to client {ClientId} for connection {ConnectionId}",
                        clientId, e.ConnectionId);
                }
            }
            else
            {
                _logger.LogWarning("Received data for unknown connection {ConnectionId}", e.ConnectionId);
            }
        }

        private async void OnTcpConnectionStatusChanged(object? sender, TcpConnectionStatusEventArgs e)
        {
            if (_connectionToClientMap.TryGetValue(e.ConnectionId, out var clientId))
            {
                _logger.LogInformation("TCP connection {ConnectionId} status changed to {Status}, notifying client {ClientId}",
                    e.ConnectionId, e.Status, clientId);
                
                // Create a status message
                var statusMessage = new ConnectionStatusMessage(e.ConnectionId, e.Status, e.Reason);
                
                try
                {
                    // Send to the client that owns the connection
                    foreach (var client in GetWebSocketClientsById(clientId))
                    {
                        await client.SendMessageAsync(statusMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send connection status to client {ClientId} for connection {ConnectionId}",
                        clientId, e.ConnectionId);
                }

                // If the connection is closed or failed, remove the mapping
                if (e.Status == ConnectionStatusMessage.Status.Closed ||
                    e.Status == ConnectionStatusMessage.Status.Failed ||
                    e.Status == ConnectionStatusMessage.Status.Error)
                {
                    _connectionToClientMap.TryRemove(e.ConnectionId, out _);
                }
            }
        }        // Look up clients by ID from the WebSocket server
        private IEnumerable<WebSocketClient> GetWebSocketClientsById(string clientId)
        {
            return _webSocketServer.GetClientById(clientId);
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
                // Unregister from events
                _tcpConnectionManager.DataReceived -= OnTcpDataReceived;
                _tcpConnectionManager.ConnectionStatusChanged -= OnTcpConnectionStatusChanged;
                
                // Stop the server
                _webSocketServer.Stop();
                
                // Dispose managers
                _tcpConnectionManager.Dispose();
                _webSocketServer.Dispose();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Detailed information about a TCP connection.
    /// </summary>
    public class ConnectionInfo
    {
        /// <summary>
        /// Gets or sets the connection identifier.
        /// </summary>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the remote endpoint (host:port).
        /// </summary>
        public string RemoteEndPoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the local endpoint information.
        /// </summary>
        public string? LocalEndPoint { get; set; }

        /// <summary>
        /// Gets or sets when the connection was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the last activity timestamp.
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Gets or sets the total bytes sent.
        /// </summary>
        public long BytesSent { get; set; }

        /// <summary>
        /// Gets or sets the total bytes received.
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// Gets or sets the number of send operations.
        /// </summary>
        public int SendOperationCount { get; set; }

        /// <summary>
        /// Gets or sets the number of receive operations.
        /// </summary>
        public int ReceiveOperationCount { get; set; }

        /// <summary>
        /// Gets or sets the connection health status.
        /// </summary>
        public TCP.ConnectionHealth Health { get; set; }

        /// <summary>
        /// Gets or sets the connection duration.
        /// </summary>
        public TimeSpan ConnectionDuration { get; set; }

        /// <summary>
        /// Gets or sets whether the connection is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the WebSocket client ID that owns this connection.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets the total data transferred (sent + received).
        /// </summary>
        public long TotalDataTransferred => BytesSent + BytesReceived;

        /// <summary>
        /// Gets the time since last activity.
        /// </summary>
        public TimeSpan TimeSinceLastActivity => DateTime.UtcNow - LastActivity;

        /// <summary>
        /// Gets the average send operation size.
        /// </summary>
        public double AverageSendSize => SendOperationCount > 0 ? (double)BytesSent / SendOperationCount : 0;

        /// <summary>
        /// Gets the average receive operation size.
        /// </summary>
        public double AverageReceiveSize => ReceiveOperationCount > 0 ? (double)BytesReceived / ReceiveOperationCount : 0;
    }
}
