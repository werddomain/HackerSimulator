using System.Text.Json.Serialization;

namespace ProxyServer.FileSystem.Models
{
    /// <summary>
    /// Represents permission level for a shared folder
    /// </summary>
    public enum SharedFolderPermission
    {
        ReadOnly,
        ReadWrite
    }

    /// <summary>
    /// Represents a shared folder configuration
    /// </summary>
    public class SharedFolderInfo
    {
        /// <summary>
        /// Unique identifier for the shared folder
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Physical path on the host machine
        /// </summary>
        public string HostPath { get; set; } = string.Empty;

        /// <summary>
        /// Alias/name for the shared folder (used for display and mounting)
        /// </summary>
        public string Alias { get; set; } = string.Empty;

        /// <summary>
        /// Permissions for the shared folder (read-only or read-write)
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SharedFolderPermission Permission { get; set; } = SharedFolderPermission.ReadOnly;

        /// <summary>
        /// Optional list of allowed file extensions (null means all are allowed)
        /// </summary>
        public List<string>? AllowedExtensions { get; set; }

        /// <summary>
        /// Optional list of blocked file extensions
        /// </summary>
        public List<string>? BlockedExtensions { get; set; }

        /// <summary>
        /// Name of the metadata file inside the shared folder
        /// </summary>
        public string MetadataFileName { get; set; } = ".mount_info.json";

        /// <summary>
        /// Last access time of the shared folder
        /// </summary>
        public DateTime LastAccessed { get; set; } = DateTime.Now;

        /// <summary>
        /// Creation time of the shared folder configuration
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// String representation of permissions for protocol compatibility
        /// </summary>
        [JsonIgnore]
        public string PermissionString => Permission == SharedFolderPermission.ReadOnly ? "read-only" : "read-write";

        /// <summary>
        /// Checks if the shared folder exists on disk
        /// </summary>
        [JsonIgnore]
        public bool Exists => !string.IsNullOrEmpty(HostPath) && Directory.Exists(HostPath);
    }
}
