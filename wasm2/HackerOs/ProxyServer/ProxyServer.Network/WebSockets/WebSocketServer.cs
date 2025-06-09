using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ProxyServer.Authentication;
using ProxyServer.Protocol;
using ProxyServer.Protocol.Models;
using ProxyServer.Protocol.Models.Control;

namespace ProxyServer.Network.WebSockets
{    /// <summary>
    /// WebSocket server for real-time communication with clients.
    /// </summary>
    public class WebSocketServer : IDisposable
    {
        private readonly ILogger<WebSocketServer> _logger;
        private readonly IAuthenticationService _authenticationService;
        private readonly AuthorizationMiddleware _authorizationMiddleware;
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly List<WebSocketClient> _clients;
        private readonly object _clientsLock = new object();
        private readonly Dictionary<string, Func<MessageBase, WebSocketClient, Task>> _messageHandlers;
        private bool _isRunning;        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketServer"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="authenticationService">The authentication service.</param>
        public WebSocketServer(ILogger<WebSocketServer> logger, IAuthenticationService authenticationService)
        {            _logger = logger;
            _authenticationService = authenticationService;
            
            // Create a logger for AuthorizationMiddleware using LoggerFactory
            using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var authLogger = loggerFactory.CreateLogger<AuthorizationMiddleware>();
            _authorizationMiddleware = new AuthorizationMiddleware(authenticationService, authLogger);
            
            _listener = new HttpListener();
            _cancellationTokenSource = new CancellationTokenSource();
            _clients = new List<WebSocketClient>();
            _messageHandlers = new Dictionary<string, Func<MessageBase, WebSocketClient, Task>>();
            
            // Register default message handlers
            RegisterDefaultMessageHandlers();
        }

        /// <summary>
        /// Starts the WebSocket server on the specified port.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StartAsync(int port, CancellationToken cancellationToken = default)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("WebSocket server is already running");
            }

            string prefix = $"http://localhost:{port}/";
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            _isRunning = true;

            _logger.LogInformation("WebSocket server started on {Prefix}", prefix);

            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    _cancellationTokenSource.Token, cancellationToken);
                
                while (!linkedCts.Token.IsCancellationRequested)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await _listener.GetContextAsync();
                    }
                    catch (HttpListenerException)
                    {
                        if (linkedCts.Token.IsCancellationRequested)
                        {
                            break;
                        }
                        throw;
                    }

                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = ProcessWebSocketRequestAsync(context, linkedCts.Token);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
            }
            finally
            {
                _isRunning = false;
                _listener.Stop();
                _logger.LogInformation("WebSocket server stopped");
            }
        }

        /// <summary>
        /// Stops the WebSocket server.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            _cancellationTokenSource.Cancel();
            
            // Close all client connections
            lock (_clientsLock)
            {
                foreach (var client in _clients)
                {
                    client.Dispose();
                }
                _clients.Clear();
            }
        }

        /// <summary>
        /// Gets the number of connected clients.
        /// </summary>
        public int ClientCount
        {
            get
            {
                lock (_clientsLock)
                {
                    return _clients.Count;
                }
            }
        }

        /// <summary>
        /// Registers a message handler for a specific message type.
        /// </summary>
        /// <param name="messageType">The message type to handle.</param>
        /// <param name="handler">The handler function.</param>
        public void RegisterMessageHandler(string messageType, Func<MessageBase, WebSocketClient, Task> handler)
        {
            _messageHandlers[messageType] = handler;
        }        /// <summary>
        /// Broadcasts a message to all connected clients.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        /// <param name="excludeClient">Optional client to exclude from the broadcast.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BroadcastMessageAsync(MessageBase message, WebSocketClient? excludeClient = null)
        {
            List<WebSocketClient> clientsCopy;
            lock (_clientsLock)
            {
                clientsCopy = _clients.ToList();
            }

            var sendTasks = new List<Task>();
            foreach (var client in clientsCopy)
            {
                if (client == excludeClient)
                {
                    continue;
                }

                sendTasks.Add(client.SendMessageAsync(message));
            }

            await Task.WhenAll(sendTasks);
        }
        
        /// <summary>
        /// Gets clients by their client ID.
        /// </summary>
        /// <param name="clientId">The client ID to look for.</param>
        /// <returns>The clients with the specified ID, or an empty collection if no match found.</returns>
        public IEnumerable<WebSocketClient> GetClientById(string clientId)
        {
            List<WebSocketClient> clientsCopy;
            lock (_clientsLock)
            {
                clientsCopy = _clients.ToList();
            }
            
            return clientsCopy.Where(c => c.ClientId == clientId);
        }

        /// <summary>
        /// Gets the number of authenticated clients.
        /// </summary>
        public int AuthenticatedClientCount
        {
            get
            {
                lock (_clientsLock)
                {
                    return _clients.Count(c => c.IsAuthenticated);
                }
            }
        }

        /// <summary>
        /// Gets information about all connected clients.
        /// </summary>
        /// <returns>A list of client information objects.</returns>
        public List<ClientInfo> GetClientInfo()
        {
            lock (_clientsLock)
            {
                return _clients.Select(c => new ClientInfo
                {
                    ClientId = c.ClientId,
                    IsAuthenticated = c.IsAuthenticated,
                    UserId = c.UserId,
                    ConnectedAt = c.ConnectedAt,
                    LastActivity = c.LastActivity,
                    RemoteEndPoint = c.RemoteEndPoint.ToString()
                }).ToList();
            }
        }

        /// <summary>
        /// Revokes authentication for a specific client.
        /// </summary>
        /// <param name="clientId">The client identifier.</param>
        /// <returns>True if the client was found and de-authenticated; otherwise, false.</returns>
        public async Task<bool> RevokeClientAuthenticationAsync(string clientId)
        {
            WebSocketClient? targetClient;
            lock (_clientsLock)
            {
                targetClient = _clients.FirstOrDefault(c => c.ClientId == clientId);
            }

            if (targetClient == null)
            {
                return false;
            }

            // Revoke the session token if the client is authenticated
            if (targetClient.IsAuthenticated && !string.IsNullOrEmpty(targetClient.AuthToken))
            {
                await _authenticationService.RevokeTokenAsync(targetClient.AuthToken);
            }

            // Update client state
            targetClient.IsAuthenticated = false;
            targetClient.AuthToken = null;
            targetClient.UserId = null;

            _logger.LogInformation("Revoked authentication for client {ClientId}", clientId);
            return true;
        }

        /// <summary>
        /// Broadcasts a message to all authenticated clients with a specific permission.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        /// <param name="requiredPermission">The required permission.</param>
        /// <param name="excludeClient">Optional client to exclude from the broadcast.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BroadcastToAuthorizedClientsAsync(MessageBase message, string requiredPermission, WebSocketClient? excludeClient = null)
        {
            List<WebSocketClient> authorizedClients;
            lock (_clientsLock)
            {
                authorizedClients = _clients.Where(c => c != excludeClient && c.IsAuthenticated).ToList();
            }

            var tasks = new List<Task>();
            foreach (var client in authorizedClients)
            {
                // Check if client has the required permission
                if (!string.IsNullOrEmpty(client.AuthToken))
                {
                    var hasPermission = await _authenticationService.HasPermissionAsync(client.AuthToken, requiredPermission);
                    if (hasPermission)
                    {
                        tasks.Add(client.SendMessageAsync(message));
                    }
                }
            }

            await Task.WhenAll(tasks);
        }

        private async Task ProcessWebSocketRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            try
            {
                WebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
                WebSocket webSocket = webSocketContext.WebSocket;
                
                var clientId = Guid.NewGuid().ToString();
                var remoteEndpoint = context.Request.RemoteEndPoint;
                
                _logger.LogInformation("WebSocket client connected: {ClientId} from {RemoteEndpoint}", 
                    clientId, remoteEndpoint);

                var client = new WebSocketClient(clientId, webSocket, remoteEndpoint);

                // Add client to the list
                lock (_clientsLock)
                {
                    _clients.Add(client);
                }

                // Start processing messages
                try
                {
                    await ProcessMessagesAsync(client, cancellationToken);
                }
                catch (WebSocketException ex)
                {
                    _logger.LogInformation("WebSocket closed: {ClientId}. Reason: {Reason}", clientId, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing messages for client {ClientId}", clientId);
                }
                finally
                {
                    // Remove client from the list
                    lock (_clientsLock)
                    {
                        _clients.Remove(client);
                    }

                    client.Dispose();
                    _logger.LogInformation("WebSocket client disconnected: {ClientId}", clientId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting WebSocket connection");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        private async Task ProcessMessagesAsync(WebSocketClient client, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            while (client.WebSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                await using var ms = new MemoryStream();

                do
                {
                    result = await client.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await client.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                            "Closing as requested", cancellationToken);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } 
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var messageBytes = ms.ToArray();
                    var messageString = Encoding.UTF8.GetString(messageBytes);
                    
                    // Process the message
                    await HandleMessageAsync(messageString, client);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    _logger.LogWarning("Binary message received but not supported yet");
                }
            }
        }        private async Task HandleMessageAsync(string messageJson, WebSocketClient client)
        {
            try
            {
                var message = MessageSerializer.Deserialize(messageJson);
                if (message == null)
                {
                    _logger.LogWarning("Failed to deserialize message from client {ClientId}: {Message}", 
                        client.ClientId, messageJson);
                    await client.SendMessageAsync(ErrorMessage.Failure("Invalid message format"));
                    return;
                }

                _logger.LogDebug("Received message of type {MessageType} from client {ClientId}", 
                    message.Type, client.ClientId);

                // Handle authentication messages first, before any authorization checks
                if (message.Type == MessageType.AUTHENTICATE)
                {
                    await HandleAuthenticationMessageAsync(message, client);
                    return;
                }                // For all other messages, check authorization first
                var authorizationResult = await _authorizationMiddleware.CheckAuthorizationAsync(message, client);
                if (!authorizationResult.Success)
                {
                    _logger.LogWarning("Authorization failed for client {ClientId}, message type {MessageType}: {Reason}", 
                        client.ClientId, message.Type, authorizationResult.ErrorMessage);
                    await client.SendMessageAsync(ErrorMessage.Failure(authorizationResult.ErrorMessage ?? "Authorization failed"));
                    return;
                }

                // Handle the message based on its type
                if (_messageHandlers.TryGetValue(message.Type, out var handler))
                {
                    await handler(message, client);
                }
                else
                {
                    _logger.LogWarning("No handler registered for message type {MessageType}", message.Type);
                    await client.SendMessageAsync(ErrorMessage.Failure($"Unsupported message type: {message.Type}"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message from client {ClientId}", client.ClientId);
                await client.SendMessageAsync(ErrorMessage.FromException(ex));
            }
        }        private async Task HandleAuthenticationMessageAsync(MessageBase message, WebSocketClient client)
        {
            try
            {
                if (message is not AuthenticationMessage authMessage)
                {
                    _logger.LogWarning("Invalid authentication message from client {ClientId}", client.ClientId);
                    await client.SendMessageAsync(new AuthenticationResponseMessage
                    {
                        Success = false,
                        ErrorMessage = "Invalid authentication message format",
                        SessionToken = null
                    });
                    return;
                }

                _logger.LogInformation("Processing authentication request from client {ClientId} with API key ending in ...{ApiKeySuffix}", 
                    client.ClientId, authMessage.ApiKey?.Length > 4 ? authMessage.ApiKey[^4..] : "****");

                // Attempt authentication
                var result = await _authenticationService.AuthenticateAsync(
                    authMessage.ApiKey!, 
                    client.ClientId, 
                    authMessage.ClientVersion);                if (result.Success)
                {
                    // Update client authentication state
                    client.IsAuthenticated = true;
                    client.AuthToken = result.SessionToken;
                    client.UserId = result.UserInfo?.UserId;                    _logger.LogInformation("Authentication successful for client {ClientId}, user {UserId}", 
                        client.ClientId, result.UserInfo?.UserId);

                    // Send success response
                    await client.SendMessageAsync(new AuthenticationResponseMessage
                    {
                        Success = true,
                        SessionToken = result.SessionToken,
                        ExpiresAt = result.ExpiresAt?.ToString("o"),                        UserInfo = new UserInfoDto
                        {
                            UserId = result.UserInfo?.UserId ?? string.Empty,
                            Role = result.UserInfo?.Role ?? string.Empty,
                            Permissions = result.UserInfo?.Permissions?.ToList() ?? new List<string>()
                        }
                    });
                }
                else
                {
                    _logger.LogWarning("Authentication failed for client {ClientId}: {Reason}", 
                        client.ClientId, result.ErrorMessage);

                    // Send failure response
                    await client.SendMessageAsync(new AuthenticationResponseMessage
                    {
                        Success = false,
                        ErrorMessage = result.ErrorMessage,
                        SessionToken = null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing authentication for client {ClientId}", client.ClientId);
                await client.SendMessageAsync(new AuthenticationResponseMessage
                {
                    Success = false,
                    ErrorMessage = "Internal authentication error",
                    SessionToken = null
                });
            }
        }

        private void RegisterDefaultMessageHandlers()
        {
            // Register heartbeat handler
            RegisterMessageHandler(MessageType.HEARTBEAT, async (message, client) =>
            {
                if (message is HeartbeatMessage heartbeat)
                {
                    // Just echo back the heartbeat with server timestamp
                    await client.SendMessageAsync(new HeartbeatMessage
                    {
                        ClientTime = heartbeat.ClientTime,
                        ClientStats = null
                    });
                }
            });

            // Add more default handlers as needed
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Client information for monitoring and administration.
    /// </summary>
    public class ClientInfo
    {
        /// <summary>
        /// Gets or sets the client identifier.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the client is authenticated.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Gets or sets the authenticated user identifier.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Gets or sets the connection time.
        /// </summary>
        public DateTime ConnectedAt { get; set; }

        /// <summary>
        /// Gets or sets the last activity time.
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Gets or sets the remote endpoint.
        /// </summary>
        public string RemoteEndPoint { get; set; } = string.Empty;
    }
}
