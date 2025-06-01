using System;
using System.Threading;
using System.Threading;

namespace HackerOs.IO.FileSystem
{
    /// <summary>
    /// Abstract base class for all virtual file system nodes (files and directories).
    /// Provides common properties and functionality for file system entities.
    /// </summary>
    public abstract class VirtualFileSystemNode
    {
        private static long _nextInodeNumber;

        /// <summary>
        /// The name of the file or directory (without path).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The full absolute path to this node in the virtual file system.
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// The timestamp when this node was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The timestamp when this node was last modified.
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The timestamp when this node was last accessed.
        /// </summary>
        public DateTime AccessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Linux-style file permissions for this node.
        /// </summary>
        public FilePermissions Permissions { get; set; } = new FilePermissions();

        /// <summary>
        /// The owner of this file system node.
        /// </summary>
        public string Owner { get; set; } = "root";

        /// <summary>
        /// The group that owns this file system node.
        /// </summary>
        public string Group { get; set; } = "root";

        /// <summary>
        /// The size of this node in bytes.
        /// For directories, this represents the size of the directory metadata.
        /// For files, this represents the content size.
        /// </summary>
        public long Size { get; set; } = 0;

        /// <summary>
        /// Unix-style inode number for this file system node.
        /// </summary>
        public long InodeNumber { get; set; }

        /// <summary>
        /// Number of hard links to this node.
        /// </summary>
        public int LinkCount { get; set; } = 1;

        /// <summary>
        /// Device ID where this node resides.
        /// </summary>
        public int DeviceId { get; set; } = 1;

        /// <summary>
        /// Mode bits representing file type and permissions.
        /// </summary>
        public uint Mode => (uint)((IsDirectory ? 0x4000 : 0x8000) | Permissions.ToOctal());

        /// <summary>
        /// Block size for file system I/O.
        /// </summary>
        public long BlockSize { get; set; } = 4096;

        /// <summary>
        /// Number of 512-byte blocks allocated for this node.
        /// </summary>
        public long Blocks => (Size + 511) / 512;

        /// <summary>
        /// Reference to the parent directory. Null for root directory.
        /// </summary>
        public VirtualDirectory? Parent { get; set; }        /// <summary>
        /// Indicates whether this node is a directory.
        /// </summary>
        public abstract bool IsDirectory { get; }

        /// <summary>
        /// Alias for CreatedAt property for compatibility.
        /// </summary>
        public DateTime CreatedTime => CreatedAt;

        /// <summary>
        /// Alias for AccessedAt property for compatibility.
        /// </summary>
        public DateTime LastAccessTime => AccessedAt;

        /// <summary>
        /// Alias for ModifiedAt property for compatibility.
        /// </summary>
        public DateTime LastModifiedTime => ModifiedAt;

        /// <summary>
        /// Indicates whether this node is a file.
        /// </summary>
        public bool IsFile => !IsDirectory;

        /// <summary>
        /// Indicates whether this is a hidden file (starts with dot).
        /// </summary>
        public bool IsHidden => Name.StartsWith('.');

        /// <summary>
        /// Updates the access time to the current UTC time.
        /// Called whenever the node is accessed.
        /// </summary>
        public virtual void UpdateAccessTime()
        {
            AccessedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Updates the modification time to the current UTC time.
        /// Called whenever the node content is modified.
        /// </summary>
        public virtual void UpdateModificationTime()
        {
            ModifiedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Validates that this node can be accessed by the specified user with the given access mode.
        /// </summary>
        /// <param name="userId">The user attempting to access the node</param>
        /// <param name="groupId">The group of the user attempting access</param>
        /// <param name="accessMode">The type of access being requested</param>
        /// <returns>True if access is allowed, false otherwise</returns>
        public virtual bool CanAccess(string userId, string groupId, FileAccessMode accessMode)
        {
            return Permissions.CanAccess(Owner, Group, userId, groupId, accessMode);
        }

        /// <summary>
        /// Gets the parent directory path, or "/" for root-level items.
        /// </summary>
        public string ParentPath
        {
            get
            {
                if (FullPath == "/")
                    return "/";
                
                var lastSlashIndex = FullPath.LastIndexOf('/');
                if (lastSlashIndex <= 0)
                    return "/";
                
                return FullPath.Substring(0, lastSlashIndex);
            }
        }

        /// <summary>
        /// Creates a deep copy of this node with all metadata.
        /// </summary>
        public abstract VirtualFileSystemNode Clone();

        /// <summary>
        /// Returns a string representation of this node for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"{(IsDirectory ? "d" : "-")}{Permissions} {Owner}:{Group} {Size,8} {ModifiedAt:yyyy-MM-dd HH:mm} {Name}";
        }

        static VirtualFileSystemNode()
        {
            // Initialize the inode counter
            _nextInodeNumber = 1;
        }

        /// <summary>
        /// Initializes a new instance of the VirtualFileSystemNode class.
        /// </summary>
        protected VirtualFileSystemNode()
        {
            // Assign unique inode number
            InodeNumber = Interlocked.Increment(ref _nextInodeNumber);
            
            // Set default timestamps
            var now = DateTime.UtcNow;
            CreatedAt = now;
            ModifiedAt = now;
            AccessedAt = now;
        }
    }

    /// <summary>
    /// Enumeration of file access modes for permission checking.
    /// </summary>
    public enum FileAccessMode
    {
        /// <summary>
        /// Read access to file content or directory listing.
        /// </summary>
        Read,

        /// <summary>
        /// Write access to modify file content or directory contents.
        /// </summary>
        Write,

        /// <summary>
        /// Execute access for files or traverse access for directories.
        /// </summary>
        Execute,

        /// <summary>
        /// Combined read and write access.
        /// </summary>
        ReadWrite
    }
}
