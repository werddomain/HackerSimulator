using System;
using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models
{
    /// <summary>
    /// Base class for all protocol messages exchanged between the proxy server and clients.
    /// </summary>
    public abstract class MessageBase
    {
        /// <summary>
        /// Gets or sets the unique identifier for this message.
        /// </summary>
        [JsonPropertyName("messageId")]
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the message type.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this message was created.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = DateTimeOffset.UtcNow.ToString("o");

        /// <summary>
        /// Gets or sets the authentication signature for this message.
        /// </summary>
        [JsonPropertyName("signature")]
        public string? Signature { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBase"/> class.
        /// </summary>
        /// <param name="type">The message type.</param>
        protected MessageBase(string type)
        {
            Type = type;
        }
    }
}
