using System.Text.Json.Serialization;

namespace ProxyServer.Protocol.Models.FileSystem
{
    #region Shared Folder Models
    
    /// <summary>
    /// Request model for creating a new shared folder
    /// </summary>
    public class CreateSharedFolderRequest
    {
        /// <summary>
        /// Physical path on the host machine
        /// </summary>
        public string HostPath { get; set; } = string.Empty;

        /// <summary>
        /// Alias/name for the shared folder
        /// </summary>
        public string Alias { get; set; } = string.Empty;

        /// <summary>
        /// Permissions for the shared folder (read-only or read-write)
        /// </summary>
        public string Permission { get; set; } = "read-only";

        /// <summary>
        /// Optional list of allowed file extensions
        /// </summary>
        public List<string>? AllowedExtensions { get; set; }

        /// <summary>
        /// Optional list of blocked file extensions
        /// </summary>
        public List<string>? BlockedExtensions { get; set; }
    }

    /// <summary>
    /// Request model for updating an existing shared folder
    /// </summary>
    public class UpdateSharedFolderRequest
    {
        /// <summary>
        /// New alias for the shared folder (optional)
        /// </summary>
        public string? Alias { get; set; }

        /// <summary>
        /// New permissions for the shared folder (optional)
        /// </summary>
        public string? Permission { get; set; }

        /// <summary>
        /// New list of allowed file extensions (optional)
        /// </summary>
        public List<string>? AllowedExtensions { get; set; }

        /// <summary>
        /// New list of blocked file extensions (optional)
        /// </summary>
        public List<string>? BlockedExtensions { get; set; }
    }
    
    #endregion
    
    #region Mount Point Models
    
    /// <summary>
    /// Request model for creating a new mount point
    /// </summary>
    public class CreateMountPointRequest
    {
        /// <summary>
        /// ID of the shared folder to mount
        /// </summary>
        public string SharedFolderId { get; set; } = string.Empty;

        /// <summary>
        /// Virtual path where the shared folder will be mounted
        /// </summary>
        public string VirtualPath { get; set; } = string.Empty;

        /// <summary>
        /// Mount options
        /// </summary>
        public MountOptionsDto Options { get; set; } = new MountOptionsDto();
    }

    /// <summary>
    /// DTO for mount options
    /// </summary>
    public class MountOptionsDto
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
        public Dictionary<string, string>? CustomOptions { get; set; }
    }

    /// <summary>
    /// Response model for a mount point
    /// </summary>
    public class MountPointDto
    {
        /// <summary>
        /// Unique identifier for the mount point
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The virtual path in the HackerOS environment
        /// </summary>
        public string VirtualPath { get; set; } = string.Empty;

        /// <summary>
        /// The ID of the shared folder that is being mounted
        /// </summary>
        public string SharedFolderId { get; set; } = string.Empty;
        
        /// <summary>
        /// The alias of the shared folder being mounted
        /// </summary>
        public string SharedFolderAlias { get; set; } = string.Empty;

        /// <summary>
        /// Mount options for this mount point
        /// </summary>
        public MountOptionsDto Options { get; set; } = new MountOptionsDto();

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
    }
    
    #endregion
    
    #region File Operation Models
    
    /// <summary>
    /// DTO for file system entries (files and directories)
    /// </summary>
    public class FileSystemEntryDto
    {
        /// <summary>
        /// Name of the file or directory
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Virtual path in the HackerOS environment
        /// </summary>
        public string VirtualPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the entry is a directory
        /// </summary>
        public bool IsDirectory { get; set; }
        
        /// <summary>
        /// Size of the file in bytes (0 for directories)
        /// </summary>
        public long Size { get; set; }
        
        /// <summary>
        /// Last modified time of the file or directory
        /// </summary>
        public DateTime LastModified { get; set; }
        
        /// <summary>
        /// Last access time of the file or directory
        /// </summary>
        public DateTime LastAccessed { get; set; }
        
        /// <summary>
        /// Creation time of the file or directory
        /// </summary>
        public DateTime CreationTime { get; set; }
        
        /// <summary>
        /// File attributes as a string representation
        /// </summary>
        public string Attributes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for writing a file
    /// </summary>
    public class WriteFileRequest
    {
        /// <summary>
        /// Virtual path of the file to write
        /// </summary>
        public string VirtualPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Content of the file to write (Base64 encoded)
        /// </summary>
        public string Content { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether to overwrite if the file already exists
        /// </summary>
        public bool Overwrite { get; set; } = true;
    }

    /// <summary>
    /// Request model for copying a file
    /// </summary>
    public class CopyFileRequest
    {
        /// <summary>
        /// Virtual path of the source file or directory
        /// </summary>
        public string SourcePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Virtual path of the destination file or directory
        /// </summary>
        public string DestinationPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether to overwrite if the destination already exists
        /// </summary>
        public bool Overwrite { get; set; } = false;
        
        /// <summary>
        /// Whether to recursively copy directories
        /// </summary>
        public bool Recursive { get; set; } = true;
    }

    /// <summary>
    /// Request model for moving a file
    /// </summary>
    public class MoveFileRequest
    {
        /// <summary>
        /// Virtual path of the source file or directory
        /// </summary>
        public string SourcePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Virtual path of the destination file or directory
        /// </summary>
        public string DestinationPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether to overwrite if the destination already exists
        /// </summary>
        public bool Overwrite { get; set; } = false;
    }

    /// <summary>
    /// Request model for creating a directory
    /// </summary>
    public class CreateDirectoryRequest
    {
        /// <summary>
        /// Virtual path of the directory to create
        /// </summary>
        public string VirtualPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether to create parent directories if they don't exist
        /// </summary>
        public bool CreateParents { get; set; } = true;
    }

    /// <summary>
    /// Generic API response wrapper
    /// </summary>
    public class ApiResponse<T>
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Data returned by the operation
        /// </summary>
        public T? Data { get; set; }
        
        /// <summary>
        /// Creates a successful response with data
        /// </summary>
        public static ApiResponse<T> SuccessResponse(T data)
        {
            return new ApiResponse<T> { Success = true, Data = data };
        }
        
        /// <summary>
        /// Creates a failed response with an error message
        /// </summary>
        public static ApiResponse<T> ErrorResponse(string errorMessage)
        {
            return new ApiResponse<T> { Success = false, ErrorMessage = errorMessage };
        }
    }
    
    #endregion
}
