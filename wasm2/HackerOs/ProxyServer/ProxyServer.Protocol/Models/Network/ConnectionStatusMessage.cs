using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models.Network
{
    /// <summary>
    /// Message for reporting the status of a TCP connection.
    /// </summary>
    public class ConnectionStatusMessage : MessageBase
    {
        /// <summary>
        /// Defines possible connection statuses.
        /// </summary>
        public enum Status
        {
            /// <summary>
            /// Connection request received, not yet established.
            /// </summary>
            Pending,

            /// <summary>
            /// Connection successfully established.
            /// </summary>
            Connected,

            /// <summary>
            /// Connection failed to establish.
            /// </summary>
            Failed,

            /// <summary>
            /// Connection was closed.
            /// </summary>
            Closed,

            /// <summary>
            /// Connection timed out.
            /// </summary>
            TimedOut,

            /// <summary>
            /// Connection had an error.
            /// </summary>
            Error
        }

        /// <summary>
        /// Gets or sets the connection ID.
        /// </summary>
        [JsonPropertyName("connectionId")]
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the status of the connection.
        /// </summary>
        [JsonPropertyName("status")]
        public Status ConnectionStatus { get; set; }

        /// <summary>
        /// Gets or sets additional details about the connection status.
        /// </summary>
        [JsonPropertyName("details")]
        public string? Details { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionStatusMessage"/> class.
        /// </summary>
        public ConnectionStatusMessage() : base(MessageType.CONNECTION_STATUS)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionStatusMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection ID.</param>
        /// <param name="status">The connection status.</param>
        /// <param name="details">Optional details about the connection status.</param>
        public ConnectionStatusMessage(string connectionId, Status status, string? details = null) 
            : base(MessageType.CONNECTION_STATUS)
        {
            ConnectionId = connectionId;
            ConnectionStatus = status;
            Details = details;
        }
    }
}
