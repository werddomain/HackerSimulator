using System;
using System.Collections.Generic;
using System.Linq;

namespace HackerOs.IO.FileSystem
{
    /// <summary>
    /// Represents a directory in the virtual file system.
    /// Contains child files and directories, and supports mount points.
    /// </summary>
    public class VirtualDirectory : VirtualFileSystemNode
    {
        /// <summary>
        /// Dictionary of child nodes indexed by name.
        /// Provides O(1) lookup for child nodes.
        /// </summary>
        public Dictionary<string, VirtualFileSystemNode> Children { get; set; } = new Dictionary<string, VirtualFileSystemNode>();

        /// <summary>
        /// Indicates whether this directory is a mount point for another file system.
        /// </summary>
        public bool IsMountPoint { get; set; } = false;

        /// <summary>
        /// The type of file system mounted at this directory (if it's a mount point).
        /// </summary>
        public string? MountedFileSystemType { get; set; }

        /// <summary>
        /// Additional mount options for this mount point.
        /// </summary>
        public Dictionary<string, object> MountOptions { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Indicates that this node is a directory (not a file).
        /// </summary>
        public override bool IsDirectory => true;

        /// <summary>
        /// Gets the number of direct children in this directory.
        /// </summary>
        public int ChildCount => Children.Count;

        /// <summary>
        /// Gets all files in this directory (non-recursive).
        /// </summary>
        public IEnumerable<VirtualFile> Files => 
            Children.Values.OfType<VirtualFile>();

        /// <summary>
        /// Gets all subdirectories in this directory (non-recursive).
        /// </summary>
        public IEnumerable<VirtualDirectory> Directories => 
            Children.Values.OfType<VirtualDirectory>();

        /// <summary>
        /// Initializes a new instance of the VirtualDirectory class.
        /// </summary>
        public VirtualDirectory()
        {
            // Set default directory permissions (755 - rwxr-xr-x)
            Permissions = FilePermissions.FromOctal(755);
        }

        /// <summary>
        /// Initializes a new instance of the VirtualDirectory class with the specified name and path.
        /// </summary>
        /// <param name="name">The name of the directory</param>
        /// <param name="fullPath">The full path to the directory</param>
        public VirtualDirectory(string name, string fullPath) : this()
        {
            Name = name;
            FullPath = fullPath;
        }

        /// <summary>
        /// Adds a child node to this directory.
        /// </summary>
        /// <param name="child">The child node to add</param>
        /// <returns>True if the child was added successfully, false if a child with the same name already exists</returns>
        public bool AddChild(VirtualFileSystemNode child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            if (string.IsNullOrEmpty(child.Name))
                throw new ArgumentException("Child node must have a name", nameof(child));

            if (Children.ContainsKey(child.Name))
                return false;

            child.Parent = this;
            Children[child.Name] = child;
            UpdateModificationTime();
            UpdateSize();
            
            return true;
        }

        /// <summary>
        /// Removes a child node from this directory.
        /// </summary>
        /// <param name="name">The name of the child to remove</param>
        /// <returns>True if the child was removed, false if it didn't exist</returns>
        public bool RemoveChild(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (!Children.ContainsKey(name))
                return false;

            var child = Children[name];
            child.Parent = null;
            Children.Remove(name);
            UpdateModificationTime();
            UpdateSize();
            
            return true;
        }

        /// <summary>
        /// Gets a child node by name.
        /// </summary>
        /// <param name="name">The name of the child to retrieve</param>
        /// <returns>The child node, or null if not found</returns>
        public VirtualFileSystemNode? GetChild(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            Children.TryGetValue(name, out var child);
            return child;
        }

        /// <summary>
        /// Checks if a child with the specified name exists.
        /// </summary>
        /// <param name="name">The name to check</param>
        /// <returns>True if a child with the name exists, false otherwise</returns>
        public bool HasChild(string name)
        {
            return !string.IsNullOrEmpty(name) && Children.ContainsKey(name);
        }

        /// <summary>
        /// Gets all children that match the specified pattern.
        /// Supports simple wildcard matching with * and ?.
        /// </summary>
        /// <param name="pattern">The pattern to match (supports * and ? wildcards)</param>
        /// <param name="includeHidden">Whether to include hidden files (starting with .)</param>
        /// <returns>An enumerable of matching child nodes</returns>
        public IEnumerable<VirtualFileSystemNode> GetChildrenByPattern(string pattern, bool includeHidden = false)
        {
            if (string.IsNullOrEmpty(pattern))
                return Array.Empty<VirtualFileSystemNode>();

            var regex = CreateWildcardRegex(pattern);
            
            return Children.Values.Where(child => 
                (includeHidden || !child.IsHidden) && 
                regex.IsMatch(child.Name));
        }

        /// <summary>
        /// Lists all children, optionally including hidden files.
        /// </summary>
        /// <param name="includeHidden">Whether to include hidden files</param>
        /// <returns>An enumerable of child nodes</returns>
        public IEnumerable<VirtualFileSystemNode> ListChildren(bool includeHidden = false)
        {
            return Children.Values.Where(child => includeHidden || !child.IsHidden);
        }

        /// <summary>
        /// Creates a new file in this directory.
        /// </summary>
        /// <param name="fileName">The name of the file to create</param>
        /// <param name="content">The initial content of the file</param>
        /// <returns>The created file, or null if a file with the same name already exists</returns>
        public VirtualFile? CreateFile(string fileName, byte[]? content = null)
        {
            if (string.IsNullOrEmpty(fileName) || HasChild(fileName))
                return null;

            var filePath = FullPath == "/" ? $"/{fileName}" : $"{FullPath}/{fileName}";
            var file = new VirtualFile(fileName, filePath, content ?? Array.Empty<byte>());
            
            if (AddChild(file))
                return file;
            
            return null;
        }

        /// <summary>
        /// Creates a new subdirectory in this directory.
        /// </summary>
        /// <param name="directoryName">The name of the directory to create</param>
        /// <returns>The created directory, or null if a directory with the same name already exists</returns>
        public VirtualDirectory? CreateDirectory(string directoryName)
        {
            if (string.IsNullOrEmpty(directoryName) || HasChild(directoryName))
                return null;

            var dirPath = FullPath == "/" ? $"/{directoryName}" : $"{FullPath}/{directoryName}";
            var directory = new VirtualDirectory(directoryName, dirPath);
            
            if (AddChild(directory))
                return directory;
            
            return null;
        }

        /// <summary>
        /// Creates a symbolic link in this directory.
        /// </summary>
        /// <param name="linkName">The name of the symbolic link</param>
        /// <param name="targetPath">The path the symbolic link points to</param>
        /// <returns>The created symbolic link, or null if a file with the same name already exists</returns>
        public VirtualFile? CreateSymbolicLink(string linkName, string targetPath)
        {
            if (string.IsNullOrEmpty(linkName) || HasChild(linkName))
                return null;

            var linkPath = FullPath == "/" ? $"/{linkName}" : $"{FullPath}/{linkName}";
            var symlink = VirtualFile.CreateSymbolicLink(linkName, linkPath, targetPath);
            
            if (AddChild(symlink))
                return symlink;
            
            return null;
        }

        /// <summary>
        /// Marks this directory as a mount point.
        /// </summary>
        /// <param name="fileSystemType">The type of file system being mounted</param>
        /// <param name="options">Mount options</param>
        public void SetAsMountPoint(string fileSystemType, Dictionary<string, object>? options = null)
        {
            IsMountPoint = true;
            MountedFileSystemType = fileSystemType;
            MountOptions = options ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Removes the mount point designation from this directory.
        /// </summary>
        public void RemoveMountPoint()
        {
            IsMountPoint = false;
            MountedFileSystemType = null;
            MountOptions.Clear();
        }

        /// <summary>
        /// Updates the size of this directory based on its children.
        /// </summary>
        private void UpdateSize()
        {
            // Directory size is the sum of its direct children metadata
            // (In real file systems, this would be the directory entry size)
            Size = Children.Count * 32; // Rough estimate of directory entry overhead
        }

        /// <summary>
        /// Creates a regex pattern from a wildcard pattern.
        /// </summary>
        private System.Text.RegularExpressions.Regex CreateWildcardRegex(string pattern)
        {
            var escapedPattern = System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".");
            
            return new System.Text.RegularExpressions.Regex($"^{escapedPattern}$", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Creates a deep copy of this directory and all its children.
        /// </summary>
        public override VirtualFileSystemNode Clone()
        {
            var clone = new VirtualDirectory(Name, FullPath)
            {
                CreatedAt = CreatedAt,
                ModifiedAt = ModifiedAt,
                AccessedAt = AccessedAt,
                Permissions = new FilePermissions
                {
                    OwnerRead = Permissions.OwnerRead,
                    OwnerWrite = Permissions.OwnerWrite,
                    OwnerExecute = Permissions.OwnerExecute,
                    GroupRead = Permissions.GroupRead,
                    GroupWrite = Permissions.GroupWrite,
                    GroupExecute = Permissions.GroupExecute,
                    OtherRead = Permissions.OtherRead,
                    OtherWrite = Permissions.OtherWrite,
                    OtherExecute = Permissions.OtherExecute
                },
                Owner = Owner,
                Group = Group,
                Size = Size,
                IsMountPoint = IsMountPoint,
                MountedFileSystemType = MountedFileSystemType,
                MountOptions = new Dictionary<string, object>(MountOptions)
            };

            // Clone all children
            foreach (var child in Children.Values)
            {
                var clonedChild = child.Clone();
                clone.AddChild(clonedChild);
            }

            return clone;
        }

        /// <summary>
        /// Returns a detailed string representation of this directory.
        /// </summary>
        public override string ToString()
        {
            var mountInfo = IsMountPoint ? $" (mounted: {MountedFileSystemType})" : "";
            return $"d{Permissions} {Owner}:{Group} {Size,8} {ModifiedAt:yyyy-MM-dd HH:mm} {Name}/{mountInfo}";
        }
    }
}
