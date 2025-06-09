using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models.FileSystem
{
    /// <summary>
    /// Message for requesting a list of available shared folders.
    /// </summary>
    public class ListSharesMessage : MessageBase
    {
        /// <summary>
        /// Gets or sets a filter to apply to the shares list.
        /// </summary>
        [JsonPropertyName("filter")]
        public string? Filter { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ListSharesMessage"/> class.
        /// </summary>
        public ListSharesMessage() : base(MessageType.LIST_SHARES)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ListSharesMessage"/> class.
        /// </summary>
        /// <param name="filter">Optional filter to apply to the shares list.</param>
        public ListSharesMessage(string? filter = null) : base(MessageType.LIST_SHARES)
        {
            Filter = filter;
        }
    }
}
