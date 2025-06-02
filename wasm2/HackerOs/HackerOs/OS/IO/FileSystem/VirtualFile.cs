using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Represents a file in the virtual file system.
    /// Contains file content, metadata, and symbolic link information.
    /// </summary>
    public class VirtualFile : VirtualFileSystemNode
    {
        /// <summary>
        /// The binary content of the file.
        /// </summary>
        public byte[] Content { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// The MIME type of the file content.
        /// </summary>
        public string MimeType { get; set; } = "application/octet-stream";

        /// <summary>
        /// Indicates whether this file is a symbolic link.
        /// </summary>
        public bool IsSymbolicLink { get; set; } = false;

        /// <summary>
        /// The target path for symbolic links. Null for regular files.
        /// </summary>
        public string? SymbolicLinkTarget { get; set; }

        /// <summary>
        /// Indicates that this node is a file (not a directory).
        /// </summary>
        public override bool IsDirectory => false;

        /// <summary>
        /// Initializes a new instance of the VirtualFile class.
        /// </summary>
        public VirtualFile()
        {
            // Set default file permissions (644 - rw-r--r--)
            Permissions = FilePermissions.FromOctal(644);
        }

        /// <summary>
        /// Initializes a new instance of the VirtualFile class with the specified name and path.
        /// </summary>
        /// <param name="name">The name of the file</param>
        /// <param name="fullPath">The full path to the file</param>
        public VirtualFile(string name, string fullPath) : this()
        {
            Name = name;
            FullPath = fullPath;
        }

        /// <summary>
        /// Initializes a new instance of the VirtualFile class with content.
        /// </summary>
        /// <param name="name">The name of the file</param>
        /// <param name="fullPath">The full path to the file</param>
        /// <param name="content">The file content</param>
        public VirtualFile(string name, string fullPath, byte[] content) : this(name, fullPath)
        {
            SetContent(content);
        }

        /// <summary>
        /// Initializes a new instance of the VirtualFile class with text content.
        /// </summary>
        /// <param name="name">The name of the file</param>
        /// <param name="fullPath">The full path to the file</param>
        /// <param name="textContent">The text content of the file</param>
        public VirtualFile(string name, string fullPath, string textContent) : this(name, fullPath)
        {
            SetTextContent(textContent);
        }

        /// <summary>
        /// Creates a symbolic link file.
        /// </summary>
        /// <param name="name">The name of the symbolic link</param>
        /// <param name="fullPath">The full path to the symbolic link</param>
        /// <param name="targetPath">The target path the symbolic link points to</param>
        /// <returns>A new VirtualFile configured as a symbolic link</returns>
        public static VirtualFile CreateSymbolicLink(string name, string fullPath, string targetPath)
        {
            var symlink = new VirtualFile(name, fullPath)
            {
                IsSymbolicLink = true,
                SymbolicLinkTarget = targetPath,
                MimeType = "inode/symlink"
            };
            
            // Symbolic links have 777 permissions but access is determined by the target
            symlink.Permissions = FilePermissions.FromOctal(777);
            
            return symlink;
        }

        /// <summary>
        /// Sets the file content and updates metadata.
        /// </summary>
        /// <param name="content">The new content for the file</param>
        public void SetContent(byte[] content)
        {
            Content = content ?? Array.Empty<byte>();
            Size = Content.Length;
            UpdateModificationTime();
            
            // Try to determine MIME type based on content
            MimeType = DetermineMimeType();
        }

        /// <summary>
        /// Sets the file content from a text string.
        /// </summary>
        /// <param name="textContent">The text content to set</param>
        /// <param name="encoding">The encoding to use (default: UTF-8)</param>
        public void SetTextContent(string textContent, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            SetContent(encoding.GetBytes(textContent ?? string.Empty));
            
            // Set MIME type for text content
            if (MimeType == "application/octet-stream")
            {
                MimeType = "text/plain";
            }
        }

        /// <summary>
        /// Gets the file content as a text string.
        /// </summary>
        /// <param name="encoding">The encoding to use (default: UTF-8)</param>
        /// <returns>The file content as text</returns>
        public string GetTextContent(Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            return encoding.GetString(Content);
        }

        /// <summary>
        /// Appends content to the file.
        /// </summary>
        /// <param name="additionalContent">The content to append</param>
        public void AppendContent(byte[] additionalContent)
        {
            if (additionalContent == null || additionalContent.Length == 0)
                return;

            var newContent = new byte[Content.Length + additionalContent.Length];
            Content.CopyTo(newContent, 0);
            additionalContent.CopyTo(newContent, Content.Length);
            
            SetContent(newContent);
        }

        /// <summary>
        /// Appends text content to the file.
        /// </summary>
        /// <param name="additionalText">The text to append</param>
        /// <param name="encoding">The encoding to use (default: UTF-8)</param>
        public void AppendTextContent(string additionalText, Encoding? encoding = null)
        {
            if (string.IsNullOrEmpty(additionalText))
                return;

            encoding ??= Encoding.UTF8;
            AppendContent(encoding.GetBytes(additionalText));
        }

        /// <summary>
        /// Clears all content from the file.
        /// </summary>
        public void ClearContent()
        {
            SetContent(Array.Empty<byte>());
        }

        /// <summary>
        /// Determines the MIME type based on file extension and content.
        /// </summary>
        private string DetermineMimeType()
        {
            // If it's a symbolic link, return symlink MIME type
            if (IsSymbolicLink)
                return "inode/symlink";

            // Determine MIME type based on file extension
            var extension = System.IO.Path.GetExtension(Name).ToLowerInvariant();
            
            return extension switch
            {
                ".txt" => "text/plain",
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".mp4" => "video/mp4",
                ".mp3" => "audio/mpeg",
                ".zip" => "application/zip",
                ".tar" => "application/x-tar",
                ".gz" => "application/gzip",
                ".sh" => "application/x-sh",
                ".py" => "text/x-python",
                ".cs" => "text/x-csharp",
                ".cpp" or ".cc" => "text/x-c++src",
                ".c" => "text/x-csrc",
                ".h" => "text/x-chdr",
                ".md" => "text/markdown",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Creates a deep copy of this virtual file with all content and metadata.
        /// </summary>
        /// <returns>A new VirtualFile instance that is a copy of this file</returns>
        public override VirtualFileSystemNode Clone()
        {
            var clone = new VirtualFile(Name, FullPath)
            {
                Content = new byte[Content.Length],
                MimeType = MimeType,
                IsSymbolicLink = IsSymbolicLink,
                SymbolicLinkTarget = SymbolicLinkTarget,
                CreatedAt = CreatedAt,
                ModifiedAt = ModifiedAt,
                AccessedAt = AccessedAt,
                Permissions = new FilePermissions(Permissions.ToOctal()),
                Owner = Owner,
                Group = Group,
                Size = Size,
                InodeNumber = InodeNumber,
                LinkCount = LinkCount,
                DeviceId = DeviceId,
                BlockSize = BlockSize,
                Parent = Parent
            };
            
            Content.CopyTo(clone.Content, 0);
            return clone;
        }        /// <summary>
        /// Opens the file for reading as a stream.
        /// </summary>
        /// <returns>A stream for reading the file content</returns>
        public Task<Stream> OpenReadAsync()
        {
            UpdateAccessTime();
            return Task.FromResult<Stream>(new MemoryStream(Content, false));
        }

        /// <summary>
        /// Opens the file for writing as a stream (truncates existing content).
        /// </summary>
        /// <returns>A stream for writing to the file</returns>
        public Task<Stream> OpenWriteAsync()
        {
            var stream = new VirtualFileWriteStream(this, false);
            return Task.FromResult<Stream>(stream);
        }

        /// <summary>
        /// Opens the file for appending as a stream.
        /// </summary>
        /// <returns>A stream for appending to the file</returns>
        public Task<Stream> OpenAppendAsync()
        {
            var stream = new VirtualFileWriteStream(this, true);
            return Task.FromResult<Stream>(stream);
        }

        /// <summary>
        /// Gets the path of this file (alias for FullPath).
        /// </summary>
        public string Path => FullPath;

        /// <summary>
        /// Gets the content of the file.
        /// </summary>
        /// <returns>The file content as byte array</returns>
        public byte[] GetContent()
        {
            UpdateLastAccessTime();
            return Content;
        }

        /// <summary>
        /// Updates the last access time to the current time.
        /// </summary>
        public void UpdateLastAccessTime()
        {
            AccessedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a detailed string representation of this file.
        /// </summary>
        public override string ToString()
        {
            var linkInfo = IsSymbolicLink ? $" -> {SymbolicLinkTarget}" : "";
            return $"{(IsSymbolicLink ? "l" : "-")}{Permissions} {Owner}:{Group} {Size,8} {ModifiedAt:yyyy-MM-dd HH:mm} {Name}{linkInfo}";
        }
    }

    /// <summary>
    /// A stream wrapper for writing to virtual files.
    /// </summary>
    internal class VirtualFileWriteStream : MemoryStream
    {
        private readonly VirtualFile _virtualFile;
        private readonly bool _append;

        public VirtualFileWriteStream(VirtualFile virtualFile, bool append) : base()
        {
            _virtualFile = virtualFile;
            _append = append;

            if (_append && _virtualFile.Content.Length > 0)
            {
                // For append mode, start with existing content
                Write(_virtualFile.Content, 0, _virtualFile.Content.Length);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Update the virtual file content when the stream is disposed
                _virtualFile.SetContent(ToArray());
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            // Update the virtual file content when the stream is disposed
            _virtualFile.SetContent(ToArray());
            await base.DisposeAsync();
        }
    }
}
