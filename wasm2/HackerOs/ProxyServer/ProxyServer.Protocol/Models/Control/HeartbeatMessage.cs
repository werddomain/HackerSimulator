using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models.Control
{
    /// <summary>
    /// Message for maintaining connection keepalive.
    /// </summary>
    public class HeartbeatMessage : MessageBase
    {
        /// <summary>
        /// Gets or sets the client timestamp.
        /// </summary>
        [JsonPropertyName("clientTime")]
        public string ClientTime { get; set; } = DateTimeOffset.UtcNow.ToString("o");

        /// <summary>
        /// Gets or sets the client statistics.
        /// </summary>
        [JsonPropertyName("clientStats")]
        public Dictionary<string, object>? ClientStats { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HeartbeatMessage"/> class.
        /// </summary>
        public HeartbeatMessage() : base(MessageType.HEARTBEAT)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HeartbeatMessage"/> class.
        /// </summary>
        /// <param name="clientStats">Optional client statistics.</param>
        public HeartbeatMessage(Dictionary<string, object>? clientStats = null) : base(MessageType.HEARTBEAT)
        {
            ClientStats = clientStats;
        }
    }
}
