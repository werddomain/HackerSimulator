using System.Text.Json.Serialization;

namespace ProxyServer.FileSystem.Models
{
    /// <summary>
    /// Represents a mount point that maps a shared folder to a virtual path
    /// </summary>
    public class MountPoint
    {
        /// <summary>
        /// Unique identifier for the mount point
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The virtual path in the HackerOS environment
        /// </summary>
        public string VirtualPath { get; set; } = string.Empty;

        /// <summary>
        /// The ID of the shared folder that is being mounted
        /// </summary>
        public string SharedFolderId { get; set; } = string.Empty;

        /// <summary>
        /// Mount options for this mount point
        /// </summary>
        public MountOptions Options { get; set; } = new MountOptions();

        /// <summary>
        /// When this mount point was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Last time this mount point was accessed
        /// </summary>
        public DateTime LastAccessed { get; set; } = DateTime.Now;

        /// <summary>
        /// Flag to check if this is an active mount point
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Reference to the shared folder (not serialized)
        /// </summary>
        [JsonIgnore]
        public SharedFolderInfo? SharedFolder { get; set; }
    }
}
