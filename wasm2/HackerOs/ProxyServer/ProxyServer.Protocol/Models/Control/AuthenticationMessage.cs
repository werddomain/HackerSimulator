using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models.Control
{
    /// <summary>
    /// Message for authenticating a client with the proxy server.
    /// </summary>
    public class AuthenticationMessage : MessageBase
    {
        /// <summary>
        /// Gets or sets the API key for authentication.
        /// </summary>
        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client identifier.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client version.
        /// </summary>
        [JsonPropertyName("clientVersion")]
        public string ClientVersion { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationMessage"/> class.
        /// </summary>
        public AuthenticationMessage() : base(MessageType.AUTHENTICATE)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationMessage"/> class.
        /// </summary>
        /// <param name="apiKey">The API key for authentication.</param>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="clientVersion">The client version.</param>
        public AuthenticationMessage(string apiKey, string clientId, string clientVersion) : base(MessageType.AUTHENTICATE)
        {
            ApiKey = apiKey;
            ClientId = clientId;
            ClientVersion = clientVersion;
        }
    }
}
