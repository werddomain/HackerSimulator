using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models.Network
{
    /// <summary>
    /// Message for sending TCP data to or from a connection.
    /// </summary>
    public class DataMessage : MessageBase
    {
        /// <summary>
        /// Gets or sets the connection ID.
        /// </summary>
        [JsonPropertyName("connectionId")]
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the data being sent, encoded as base64.
        /// </summary>
        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataMessage"/> class.
        /// </summary>
        public DataMessage() : base(MessageType.SEND_DATA)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection ID.</param>
        /// <param name="data">The base64 encoded data.</param>
        public DataMessage(string connectionId, string data) : base(MessageType.SEND_DATA)
        {
            ConnectionId = connectionId;
            Data = data;
        }

        /// <summary>
        /// Creates a new DataMessage from binary data.
        /// </summary>
        /// <param name="connectionId">The connection ID.</param>
        /// <param name="binaryData">The binary data to encode and send.</param>
        /// <returns>A new DataMessage instance.</returns>
        public static DataMessage FromBinaryData(string connectionId, byte[] binaryData)
        {
            return new DataMessage(
                connectionId, 
                Convert.ToBase64String(binaryData)
            );
        }

        /// <summary>
        /// Gets the binary data from this message.
        /// </summary>
        /// <returns>The decoded binary data.</returns>
        public byte[] GetBinaryData()
        {
            if (string.IsNullOrEmpty(Data))
            {
                return Array.Empty<byte>();
            }

            return Convert.FromBase64String(Data);
        }
    }
}
