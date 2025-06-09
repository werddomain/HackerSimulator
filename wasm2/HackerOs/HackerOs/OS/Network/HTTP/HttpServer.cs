using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HackerOs.OS.Network.Core;
using HackerOs.OS.Network.WebServer.Hosting;

namespace HackerOs.OS.Network.HTTP
{
    /// <summary>
    /// Represents a basic HTTP server in the HackerOS network stack
    /// </summary>
    public class HttpServer : IDisposable
    {
        private readonly ILogger<HttpServer> _logger;
        private readonly INetworkStack _networkStack;
        private readonly List<IHttpMiddleware> _middleware;
        private readonly List<IHttpRequestHandler> _requestHandlers;
        private readonly VirtualHostManager _virtualHostManager;
        private ISocket _serverSocket;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly object _syncLock = new object();
        private bool _isRunning;
        private bool _disposed;
        
        /// <summary>
        /// Gets a value indicating whether the server is running
        /// </summary>
        public bool IsRunning => _isRunning;
        
        /// <summary>
        /// Gets or sets the default port for the server
        /// </summary>
        public int Port { get; set; } = 80;
        
        /// <summary>
        /// Gets or sets the server name
        /// </summary>
        public string ServerName { get; set; } = "HackerOS/1.0";
        
        /// <summary>
        /// Gets the virtual host manager
        /// </summary>
        public VirtualHostManager VirtualHostManager => _virtualHostManager;
        
        /// <summary>
        /// Event fired when a request is received
        /// </summary>
        public event EventHandler<HttpRequestEventArgs> RequestReceived;
        
        /// <summary>
        /// Event fired when a response is sent
        /// /// </summary>
        public event EventHandler<HttpResponseEventArgs> ResponseSent;
        
        /// <summary>
        /// Event fired when a server error occurs
        /// </summary>
        public event EventHandler<HttpServerErrorEventArgs> ServerError;
        
        /// <summary>
        /// Creates a new instance of the HttpServer class
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="networkStack">The network stack to use</param>
        public HttpServer(ILogger<HttpServer> logger, INetworkStack networkStack)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _networkStack = networkStack ?? throw new ArgumentNullException(nameof(networkStack));
            _middleware = new List<IHttpMiddleware>();
            _requestHandlers = new List<IHttpRequestHandler>();
            _virtualHostManager = new VirtualHostManager();
            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = false;
            _disposed = false;
        }
        
        /// <summary>
        /// Starts the HTTP server on the specified port
        /// </summary>
        public async Task<bool> StartAsync(int port = 0)
        {
            if (_isRunning)
            {
                _logger.LogWarning("HTTP server already running");
                return true;
            }
            
            if (port > 0)
            {
                Port = port;
            }
            
            try
            {
                _logger.LogInformation("Starting HTTP server on port {Port}", Port);
                
                // Create the server socket
                _serverSocket = await _networkStack.CreateSocketAsync(SocketType.Stream, ProtocolType.Tcp);
                
                // Bind to the specified port
                var endpoint = new NetworkEndPoint("0.0.0.0", Port);
                
                await _serverSocket.BindAsync(endpoint);
                
                // Start listening for connections
                await _serverSocket.ListenAsync(10);  // Max 10 pending connections
                
                // Start accepting connections
                _isRunning = true;
                _ = AcceptConnectionsAsync(_cancellationTokenSource.Token);
                
                _logger.LogInformation("HTTP server started on port {Port}", Port);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting HTTP server on port {Port}", Port);
                _isRunning = false;
                
                // Clean up if necessary
                if (_serverSocket != null)
                {
                    await _serverSocket.CloseAsync();
                    _serverSocket = null;
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Stops the HTTP server
        /// </summary>
        public async Task<bool> StopAsync()
        {
            if (!_isRunning)
            {
                _logger.LogWarning("HTTP server not running");
                return true;
            }
            
            try
            {
                _logger.LogInformation("Stopping HTTP server on port {Port}", Port);
                
                // Cancel ongoing operations
                _cancellationTokenSource.Cancel();
                
                // Close the server socket
                if (_serverSocket != null)
                {
                    await _serverSocket.CloseAsync();
                    _serverSocket = null;
                }
                
                _isRunning = false;
                _logger.LogInformation("HTTP server stopped");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping HTTP server");
                return false;
            }
        }
        
        /// <summary>
        /// Adds a middleware component to the HTTP pipeline
        /// </summary>
        public void UseMiddleware(IHttpMiddleware middleware)
        {
            if (middleware == null)
            {
                throw new ArgumentNullException(nameof(middleware));
            }
            
            _middleware.Add(middleware);
            _logger.LogDebug("Added middleware: {Middleware}", middleware.GetType().Name);
        }
        
        /// <summary>
        /// Adds a request handler to the HTTP server
        /// </summary>
        public void AddRequestHandler(IHttpRequestHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            
            _requestHandlers.Add(handler);
            _logger.LogDebug("Added request handler: {Handler}", handler.GetType().Name);
        }
        
        /// <summary>
        /// Adds a virtual host to the server
        /// </summary>
        /// <param name="host">The virtual host to add</param>
        public void AddVirtualHost(IVirtualHost host)
        {
            _virtualHostManager.AddHost(host);
            _logger.LogInformation("Added virtual host: {Hostname}", host.Hostname);
        }
        
        /// <summary>
        /// Sets the default virtual host
        /// </summary>
        /// <param name="host">The virtual host to set as default</param>
        public void SetDefaultVirtualHost(IVirtualHost host)
        {
            _virtualHostManager.SetDefaultHost(host);
            _logger.LogInformation("Set default virtual host: {Hostname}", host.Hostname);
        }
        
        /// <summary>
        /// Continuously accepts new connections
        /// </summary>
        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting connection acceptance loop");
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    try
                    {
                        // Accept a new client connection
                        var clientSocket = await _serverSocket.AcceptAsync();
                        
                        if (clientSocket != null)
                        {
                            // Handle the client connection in a separate task
                            _ = HandleClientAsync(clientSocket, cancellationToken);
                        }
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogError(ex, "Error accepting client connection");
                        
                        // Brief pause to prevent tight loop in case of persistent errors
                        await Task.Delay(100, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Connection acceptance loop canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in connection acceptance loop");
                OnServerError(ex, "AcceptConnections");
            }
            
            _logger.LogDebug("Connection acceptance loop ended");
        }
        
        /// <summary>
        /// Handles a client connection
        /// </summary>
        private async Task HandleClientAsync(ISocket clientSocket, CancellationToken cancellationToken)
        {
            using (clientSocket)
            {
                try
                {
                    _logger.LogDebug("Handling client connection from {RemoteEndPoint}", clientSocket.RemoteEndPoint);
                    
                    // Create buffer for receiving data
                    var receiveBuffer = new byte[8192]; // 8KB buffer
                    
                    // Receive request data
                    int bytesRead = await clientSocket.ReceiveAsync(receiveBuffer, 0, receiveBuffer.Length);
                    
                    if (bytesRead > 0)
                    {
                        // Parse the request
                        var requestData = Encoding.UTF8.GetString(receiveBuffer, 0, bytesRead);
                        var httpContext = ParseRequest(requestData, clientSocket);
                        
                        if (httpContext != null)
                        {
                            // Process the request
                            await ProcessRequestAsync(httpContext, cancellationToken);
                            
                            // Send the response
                            await SendResponseAsync(httpContext, clientSocket, cancellationToken);
                        }
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error handling client connection");
                    OnServerError(ex, "HandleClient");
                    
                    // Attempt to send an error response
                    try
                    {
                        var errorResponse = 
                            "HTTP/1.1 500 Internal Server Error\r\n" +
                            $"Date: {DateTime.UtcNow:R}\r\n" +
                            "Content-Type: text/plain\r\n" +
                            "Connection: close\r\n" +
                            "\r\n" +
                            "500 Internal Server Error";
                        
                        var errorBytes = Encoding.UTF8.GetBytes(errorResponse);
                        await clientSocket.SendAsync(errorBytes, 0, errorBytes.Length);
                    }
                    catch
                    {
                        // Ignore errors while sending error response
                    }
                }
            }
        }
        
        /// <summary>
        /// Parses an HTTP request from raw data
        /// </summary>
        private HttpContext ParseRequest(string requestData, ISocket clientSocket)
        {
            try
            {
                // Split the request into lines
                var lines = requestData.Split(new[] { "\r\n" }, StringSplitOptions.None);
                
                if (lines.Length < 1)
                {
                    _logger.LogWarning("Invalid HTTP request: not enough lines");
                    return null;
                }
                
                // Parse the request line
                var requestLineParts = lines[0].Split(' ');
                
                if (requestLineParts.Length < 3)
                {
                    _logger.LogWarning("Invalid HTTP request line: {RequestLine}", lines[0]);
                    return null;
                }
                
                // Extract method, URL, and HTTP version
                var methodStr = requestLineParts[0];
                var url = requestLineParts[1];
                var httpVersion = requestLineParts[2];
                
                // Parse the HTTP method
                if (!HttpMethodExtensions.TryParse(methodStr, out var method))
                {
                    _logger.LogWarning("Invalid HTTP method: {Method}", methodStr);
                    return null;
                }
                
                // Create the request URL
                if (!Uri.TryCreate($"http://{clientSocket.LocalEndPoint.Address}{url}", UriKind.Absolute, out var uri))
                {
                    // Try with a default host if the URL is relative
                    if (!Uri.TryCreate($"http://localhost{url}", UriKind.Absolute, out uri))
                    {
                        _logger.LogWarning("Invalid URL: {Url}", url);
                        return null;
                    }
                }
                
                // Create the request object
                var request = new HttpRequest(uri)
                {
                    Method = method,
                    HttpVersion = httpVersion,
                    RemoteIpAddress = clientSocket.RemoteEndPoint.Address,
                    RemotePort = clientSocket.RemoteEndPoint.Port,
                    LocalIpAddress = clientSocket.LocalEndPoint.Address,
                    LocalPort = clientSocket.LocalEndPoint.Port
                };
                
                // Parse headers
                int i = 1;
                while (i < lines.Length && !string.IsNullOrEmpty(lines[i]))
                {
                    var headerParts = lines[i].Split(new[] { ':' }, 2);
                    
                    if (headerParts.Length == 2)
                    {
                        var headerName = headerParts[0].Trim();
                        var headerValue = headerParts[1].Trim();
                        request.Headers.AddHeader(headerName, headerValue);
                    }
                    
                    i++;
                }
                
                // Parse cookies
                request.ParseCookies();
                
                // Extract body (if any)
                if (i < lines.Length - 1)
                {
                    var bodyStart = requestData.IndexOf("\r\n\r\n") + 4;
                    
                    if (bodyStart < requestData.Length)
                    {
                        var bodyData = requestData.Substring(bodyStart);
                        request.Content = bodyData;
                    }
                }
                
                // Create the response object
                var response = new HttpResponse();
                
                // Create and return the HTTP context
                var context = new HttpContext(request, response)
                {
                    Host = request.Headers.GetHeaderValue("Host"),
                    IsHttps = false // We don't support HTTPS yet
                };
                
                return context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing HTTP request");
                return null;
            }
        }
        
        /// <summary>
        /// Processes an HTTP request through middleware and handlers
        /// </summary>
        private async Task ProcessRequestAsync(HttpContext context, CancellationToken cancellationToken)
        {
            try
            {
                // Fire the request received event
                OnRequestReceived(context);
                
                // Parse form data if it's a POST request
                if (context.Request.Method == HttpMethod.POST)
                {
                    await context.Request.ParseFormDataAsync();
                }
                
                // Run request through middleware pipeline
                bool handled = false;
                
                // Process middleware
                foreach (var middleware in _middleware)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    
                    var result = await middleware.ProcessAsync(context);
                    
                    if (result.Handled)
                    {
                        handled = true;
                        break;
                    }
                }
                
                // If not handled by middleware, check virtual hosts
                if (!handled && !cancellationToken.IsCancellationRequested)
                {
                    await _virtualHostManager.ProcessRequestAsync(context);
                }
                
                // If still not handled, try request handlers
                if (!handled && !cancellationToken.IsCancellationRequested)
                {
                    // Find a handler that can handle this request
                    bool handlerFound = false;
                    
                    foreach (var handler in _requestHandlers)
                    {
                        if (handler.CanHandleRequest(context))
                        {
                            handlerFound = true;
                            await handler.HandleRequestAsync(context);
                            break;
                        }
                    }
                    
                    // If no handler was found, return 404
                    if (!handlerFound)
                    {
                        await context.SetErrorAsync(HttpStatusCode.NotFound, "The requested resource was not found.");
                    }
                }
                
                // Ensure the response has a server header
                if (!context.Response.Headers.ContainsHeader("Server"))
                {
                    context.Response.Headers["Server"] = ServerName;
                }
                
                // Ensure the response has a date header
                if (!context.Response.Headers.ContainsHeader("Date"))
                {
                    context.Response.Headers["Date"] = DateTime.UtcNow.ToString("R");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing HTTP request");
                
                // Set an error response
                await context.SetErrorAsync(HttpStatusCode.InternalServerError, "An internal server error occurred.");
            }
        }
        
        /// <summary>
        /// Sends an HTTP response to the client
        /// </summary>
        private async Task SendResponseAsync(HttpContext context, ISocket clientSocket, CancellationToken cancellationToken)
        {
            try
            {
                // Get the full response as a string
                var responseText = await context.Response.GetFullResponseAsync();
                
                // Convert to bytes
                var responseBytes = Encoding.UTF8.GetBytes(responseText);
                
                // Send the response
                await clientSocket.SendAsync(responseBytes, 0, responseBytes.Length);
                
                // Log the response
                _logger.LogDebug("Sent HTTP response: {StatusCode} {Method} {Url}",
                    (int)context.Response.StatusCode,
                    context.Request.Method,
                    context.Request.Url);
                
                // Fire the response sent event
                OnResponseSent(context);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error sending HTTP response");
                OnServerError(ex, "SendResponse");
            }
        }
        
        /// <summary>
        /// Fires the RequestReceived event
        /// </summary>
        protected virtual void OnRequestReceived(HttpContext context)
        {
            RequestReceived?.Invoke(this, new HttpRequestEventArgs { Context = context });
        }
        
        /// <summary>
        /// Fires the ResponseSent event
        /// </summary>
        protected virtual void OnResponseSent(HttpContext context)
        {
            ResponseSent?.Invoke(this, new HttpResponseEventArgs { Context = context });
        }
        
        /// <summary>
        /// Fires the ServerError event
        /// </summary>
        protected virtual void OnServerError(Exception exception, string operation)
        {
            ServerError?.Invoke(this, new HttpServerErrorEventArgs
            {
                Exception = exception,
                Operation = operation,
                Timestamp = DateTime.UtcNow
            });
        }
        
        /// <summary>
        /// Disposes the HTTP server
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Disposes the HTTP server
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            
            if (disposing)
            {
                // Stop the server if it's running
                if (_isRunning)
                {
                    StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                
                // Dispose the cancellation token source
                _cancellationTokenSource.Dispose();
            }
            
            _disposed = true;
        }
    }
    
    /// <summary>
    /// Interface for HTTP middleware components
    /// </summary>
    public interface IHttpMiddleware
    {
        /// <summary>
        /// Processes an HTTP request
        /// </summary>
        Task<MiddlewareResult> ProcessAsync(HttpContext context);
    }
    
    /// <summary>
    /// Interface for HTTP request handlers
    /// </summary>
    public interface IHttpRequestHandler
    {
        /// <summary>
        /// Determines whether this handler can handle the specified request
        /// </summary>
        bool CanHandleRequest(HttpContext context);
        
        /// <summary>
        /// Handles an HTTP request
        /// </summary>
        Task HandleRequestAsync(HttpContext context);
    }
    
    /// <summary>
    /// Result of middleware processing
    /// </summary>
    public class MiddlewareResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the request was handled
        /// </summary>
        public bool Handled { get; set; }
        
        /// <summary>
        /// Gets or sets additional result data
        /// </summary>
        public object Data { get; set; }
    }
    
    /// <summary>
    /// Event arguments for HTTP request events
    /// </summary>
    public class HttpRequestEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the HTTP context
        /// </summary>
        public HttpContext Context { get; set; }
    }
    
    /// <summary>
    /// Event arguments for HTTP response events
    /// </summary>
    public class HttpResponseEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the HTTP context
        /// </summary>
        public HttpContext Context { get; set; }
    }
    
    /// <summary>
    /// Event arguments for HTTP server error events
    /// </summary>
    public class HttpServerErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the exception that occurred
        /// </summary>
        public Exception Exception { get; set; }
        
        /// <summary>
        /// Gets or sets the operation that was being performed
        /// </summary>
        public string Operation { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
