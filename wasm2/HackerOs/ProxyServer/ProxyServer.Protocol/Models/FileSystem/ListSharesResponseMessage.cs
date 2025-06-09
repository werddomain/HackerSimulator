using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models.FileSystem
{
    /// <summary>
    /// Response message containing available shared folders.
    /// </summary>
    public class ListSharesResponseMessage : MessageBase
    {
        /// <summary>
        /// Gets or sets the list of shared folders.
        /// </summary>
        [JsonPropertyName("shares")]
        public List<SharedFolderInfo> Shares { get; set; } = new List<SharedFolderInfo>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ListSharesResponseMessage"/> class.
        /// </summary>
        public ListSharesResponseMessage() : base("LIST_SHARES_RESPONSE")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ListSharesResponseMessage"/> class.
        /// </summary>
        /// <param name="shares">The list of shares to include.</param>
        public ListSharesResponseMessage(List<SharedFolderInfo> shares) : base("LIST_SHARES_RESPONSE")
        {
            Shares = shares;
        }
    }
}
