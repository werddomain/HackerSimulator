using System.Text.Json.Serialization;

namespace ProxyServer.FileSystem.Models
{
    /// <summary>
    /// Options for configuring a mount point
    /// </summary>
    public class MountOptions
    {
        /// <summary>
        /// When true, the mount point is read-only regardless of the underlying shared folder permissions
        /// </summary>
        public bool ReadOnly { get; set; } = false;

        /// <summary>
        /// When true, the mount point is case-sensitive for file and directory names
        /// </summary>
        public bool CaseSensitive { get; set; } = false;

        /// <summary>
        /// When true, file access is tracked in the metadata file
        /// </summary>
        public bool TrackAccess { get; set; } = true;

        /// <summary>
        /// When true, symlinks in the mount point are followed (with security checks)
        /// </summary>
        public bool FollowSymlinks { get; set; } = false;

        /// <summary>
        /// Maximum file size in bytes that can be uploaded to this mount point (0 = unlimited)
        /// </summary>
        public long MaxFileSize { get; set; } = 0;

        /// <summary>
        /// Additional custom options as key-value pairs
        /// </summary>
        public Dictionary<string, string> CustomOptions { get; set; } = new Dictionary<string, string>();        /// <summary>
        /// Effective permission based on mount options and shared folder permissions
        /// </summary>
        /// <param name="sharedFolderPermission">The permission of the shared folder</param>
        /// <returns>The effective permission after applying mount options</returns>
        public SharedFolderPermission GetEffectivePermission(SharedFolderPermission sharedFolderPermission)
        {
            // If mount is read-only, then the effective permission is always read-only
            if (ReadOnly)
            {
                return SharedFolderPermission.ReadOnly;
            }

            // Otherwise, use the shared folder's permission level
            return sharedFolderPermission;
        }
    }
}
