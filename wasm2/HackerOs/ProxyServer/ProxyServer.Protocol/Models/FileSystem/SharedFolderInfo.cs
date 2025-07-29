using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models.FileSystem
{
    /// <summary>
    /// Represents a shared folder configuration.
    /// </summary>
    public class SharedFolderInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the shared folder.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the host OS path.
        /// </summary>
        [JsonPropertyName("hostPath")]
        public string HostPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the alias name for the shared folder.
        /// </summary>
        [JsonPropertyName("alias")]
        public string Alias { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the permissions for the shared folder.
        /// </summary>
        [JsonPropertyName("permissions")]
        public string Permissions { get; set; } = "read-write";

        /// <summary>
        /// Gets or sets the metadata file name.
        /// </summary>
        [JsonPropertyName("mountInfo")]
        public string MountInfo { get; set; } = ".mount_info.json";

        /// <summary>
        /// Gets or sets the creation time of the shared folder configuration.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("o");

        /// <summary>
        /// Gets or sets the time when the shared folder was last updated.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public string UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("o");

        /// <summary>
        /// Gets or sets the last access time of the shared folder.
        /// </summary>
        [JsonPropertyName("lastAccessed")]
        public string? LastAccessed { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedFolderInfo"/> class.
        /// </summary>
        public SharedFolderInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedFolderInfo"/> class.
        /// </summary>
        /// <param name="hostPath">The host path.</param>
        /// <param name="alias">The alias for the shared folder.</param>
        /// <param name="permissions">The permissions for the shared folder.</param>
        public SharedFolderInfo(string hostPath, string alias, string permissions = "read-write")
        {
            HostPath = hostPath;
            Alias = alias;
            Permissions = permissions;
        }
    }
}
