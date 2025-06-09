using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models.Network
{
    /// <summary>
    /// Message to request a TCP connection to a remote host.
    /// </summary>
    public class TcpConnectMessage : MessageBase
    {
        /// <summary>
        /// Gets or sets the host to connect to.
        /// </summary>
        [JsonPropertyName("host")]
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the port to connect to.
        /// </summary>
        [JsonPropertyName("port")]
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the connection ID assigned by the client.
        /// </summary>
        [JsonPropertyName("connectionId")]
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the connection timeout in milliseconds.
        /// </summary>
        [JsonPropertyName("timeout")]
        public int Timeout { get; set; } = 30000; // Default: 30 seconds

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpConnectMessage"/> class.
        /// </summary>
        public TcpConnectMessage() : base(MessageType.CONNECT_TCP)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpConnectMessage"/> class.
        /// </summary>
        /// <param name="host">The host to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <param name="connectionId">The connection ID assigned by the client.</param>
        public TcpConnectMessage(string host, int port, string connectionId) : base(MessageType.CONNECT_TCP)
        {
            Host = host;
            Port = port;
            ConnectionId = connectionId;
        }
    }
}
