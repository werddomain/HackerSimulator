using System.Net;
using System.Net.WebSockets;
using System.Text;
using ProxyServer.Protocol;
using ProxyServer.Protocol.Models;

namespace ProxyServer.Network.WebSockets
{
    /// <summary>
    /// Represents a connected WebSocket client.
    /// </summary>
    public class WebSocketClient : IDisposable
    {
        private readonly SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1, 1);
        private bool _disposed;

        /// <summary>
        /// Gets the client identifier.
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// Gets the WebSocket.
        /// </summary>
        public WebSocket WebSocket { get; }

        /// <summary>
        /// Gets the remote endpoint.
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Gets or sets the authentication status.
        /// </summary>
        public bool IsAuthenticated { get; set; }        /// <summary>
        /// Gets or sets the authentication token.
        /// </summary>
        public string? AuthToken { get; set; }

        /// <summary>
        /// Gets or sets the authenticated user identifier.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Gets or sets the client connection time.
        /// </summary>
        public DateTime ConnectedAt { get; set; }

        /// <summary>
        /// Gets or sets the last activity time.
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Gets the connection state.
        /// </summary>
        public WebSocketState State => WebSocket.State;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketClient"/> class.
        /// </summary>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="webSocket">The WebSocket.</param>
        /// <param name="remoteEndPoint">The remote endpoint.</param>
        public WebSocketClient(string clientId, WebSocket webSocket, IPEndPoint remoteEndPoint)
        {
            ClientId = clientId;
            WebSocket = webSocket;
            RemoteEndPoint = remoteEndPoint;
            ConnectedAt = DateTime.UtcNow;
            LastActivity = DateTime.UtcNow;
            IsAuthenticated = false;
        }

        /// <summary>
        /// Sends a message to the client.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        public async Task SendMessageAsync(MessageBase message, CancellationToken cancellationToken = default)
        {
            if (WebSocket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is not open");
            }

            await _sendSemaphore.WaitAsync(cancellationToken);
            try
            {
                string json = MessageSerializer.Serialize(message);
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                await WebSocket.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
                
                LastActivity = DateTime.UtcNow;
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        /// <summary>
        /// Sends a binary message to the client.
        /// </summary>
        /// <param name="data">The binary data to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        public async Task SendBinaryAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (WebSocket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is not open");
            }

            await _sendSemaphore.WaitAsync(cancellationToken);
            try
            {
                await WebSocket.SendAsync(
                    new ArraySegment<byte>(data),
                    WebSocketMessageType.Binary,
                    true,
                    cancellationToken);
                
                LastActivity = DateTime.UtcNow;
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        /// <summary>
        /// Closes the WebSocket connection.
        /// </summary>
        /// <param name="closeStatus">The close status.</param>
        /// <param name="statusDescription">The status description.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous close operation.</returns>
        public async Task CloseAsync(
            WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure,
            string statusDescription = "Closing",
            CancellationToken cancellationToken = default)
        {
            if (WebSocket.State == WebSocketState.Open)
            {
                await WebSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
            }
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
                if (WebSocket.State != WebSocketState.Closed && WebSocket.State != WebSocketState.Aborted)
                {
                    // Close the WebSocket if it's not already closed
                    WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disposed", default).ConfigureAwait(false);
                }
                WebSocket.Dispose();
            }

            _disposed = true;
        }
    }
}
