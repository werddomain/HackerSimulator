using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models.Network
{
    /// <summary>
    /// Message for closing a TCP connection.
    /// </summary>
    public class CloseConnectionMessage : MessageBase
    {
        /// <summary>
        /// Gets or sets the connection ID.
        /// </summary>
        [JsonPropertyName("connectionId")]
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reason for closing the connection.
        /// </summary>
        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CloseConnectionMessage"/> class.
        /// </summary>
        public CloseConnectionMessage() : base(MessageType.CLOSE_CONNECTION)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CloseConnectionMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection ID.</param>
        /// <param name="reason">Optional reason for closing.</param>
        public CloseConnectionMessage(string connectionId, string? reason = null) : base(MessageType.CLOSE_CONNECTION)
        {
            ConnectionId = connectionId;
            Reason = reason;
        }
    }
}
