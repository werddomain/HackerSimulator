using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Represents a mount point in the virtual file system.
    /// Mount points allow different file systems or virtual drives to be attached to specific paths.
    /// </summary>
    public class MountPoint
    {
        /// <summary>
        /// Gets the path where this mount point is attached.
        /// </summary>
        public string MountPath { get; }

        /// <summary>
        /// Gets the source or device being mounted.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets the type of the mount (e.g., "ext4", "tmpfs", "proc", etc.).
        /// </summary>
        public string FileSystemType { get; }

        /// <summary>
        /// Gets the mount options.
        /// </summary>
        public MountOptions Options { get; }

        /// <summary>
        /// Gets the virtual file system instance for this mount point.
        /// </summary>
        public IVirtualFileSystem? FileSystem { get; set; }

        /// <summary>
        /// Gets the time when this mount point was created.
        /// </summary>
        public DateTime MountTime { get; }

        /// <summary>
        /// Gets whether this mount point is currently active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Initializes a new instance of the MountPoint class.
        /// </summary>
        /// <param name="mountPath">The path where the mount point is attached</param>
        /// <param name="source">The source or device being mounted</param>
        /// <param name="fileSystemType">The type of the file system</param>
        /// <param name="options">Mount options</param>
        public MountPoint(string mountPath, string source, string fileSystemType, MountOptions options)
        {
            MountPath = mountPath ?? throw new ArgumentNullException(nameof(mountPath));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            FileSystemType = fileSystemType ?? throw new ArgumentNullException(nameof(fileSystemType));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            MountTime = DateTime.UtcNow;
            IsActive = true;
        }

        /// <summary>
        /// Returns a string representation of this mount point.
        /// </summary>
        public override string ToString()
        {
            return $"{Source} on {MountPath} type {FileSystemType} ({Options})";
        }
    }

    /// <summary>
    /// Represents mount options for a mount point.
    /// </summary>
    public class MountOptions
    {
        /// <summary>
        /// Gets or sets whether the mount is read-only.
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Gets or sets whether the mount allows execution of binaries.
        /// </summary>
        public bool NoExec { get; set; }

        /// <summary>
        /// Gets or sets whether the mount allows device files.
        /// </summary>
        public bool NoDev { get; set; }

        /// <summary>
        /// Gets or sets whether the mount allows setuid binaries.
        /// </summary>
        public bool NoSuid { get; set; }

        /// <summary>
        /// Gets or sets whether to synchronize I/O operations.
        /// </summary>
        public bool Sync { get; set; }

        /// <summary>
        /// Gets or sets whether to use asynchronous I/O.
        /// </summary>
        public bool Async { get; set; }

        /// <summary>
        /// Gets or sets custom mount options.
        /// </summary>
        public Dictionary<string, string> CustomOptions { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Initializes a new instance of the MountOptions class with default values.
        /// </summary>
        public MountOptions()
        {
            ReadOnly = false;
            NoExec = false;
            NoDev = false;
            NoSuid = false;
            Sync = false;
            Async = true;
        }

        /// <summary>
        /// Returns a string representation of the mount options.
        /// </summary>
        public override string ToString()
        {
            var options = new List<string>();
            
            if (ReadOnly) options.Add("ro");
            else options.Add("rw");
            
            if (NoExec) options.Add("noexec");
            if (NoDev) options.Add("nodev");
            if (NoSuid) options.Add("nosuid");
            if (Sync) options.Add("sync");
            if (Async) options.Add("async");
            
            foreach (var custom in CustomOptions)
            {
                if (string.IsNullOrEmpty(custom.Value))
                    options.Add(custom.Key);
                else
                    options.Add($"{custom.Key}={custom.Value}");
            }
            
            return string.Join(",", options);
        }
    }

    /// <summary>
    /// Manages mount points in the virtual file system.
    /// Provides Linux-like mount/umount functionality for different file systems.
    /// </summary>
    public class MountManager
    {
        private readonly Dictionary<string, MountPoint> _mountPoints = new Dictionary<string, MountPoint>();
        private readonly object _lockObject = new object();

        /// <summary>
        /// Gets all active mount points.
        /// </summary>
        public IReadOnlyDictionary<string, MountPoint> MountPoints
        {
            get
            {
                lock (_lockObject)
                {
                    return new Dictionary<string, MountPoint>(_mountPoints);
                }
            }
        }

        /// <summary>
        /// Event raised when a mount point is added.
        /// </summary>
        public event EventHandler<MountPointEventArgs>? MountPointAdded;

        /// <summary>
        /// Event raised when a mount point is removed.
        /// </summary>
        public event EventHandler<MountPointEventArgs>? MountPointRemoved;

        /// <summary>
        /// Initializes a new instance of the MountManager class.
        /// </summary>
        public MountManager()
        {
            InitializeStandardMounts();
        }

        /// <summary>
        /// Mounts a file system at the specified path.
        /// </summary>
        /// <param name="source">The source or device to mount</param>
        /// <param name="mountPath">The path where the file system should be mounted</param>
        /// <param name="fileSystemType">The type of the file system</param>
        /// <param name="options">Mount options</param>
        /// <param name="fileSystem">Optional file system instance to use</param>
        /// <returns>True if the mount was successful; otherwise, false</returns>
        public bool Mount(string source, string mountPath, string fileSystemType, MountOptions? options = null, IVirtualFileSystem? fileSystem = null)
        {
            if (string.IsNullOrEmpty(source))
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrEmpty(mountPath))
                throw new ArgumentNullException(nameof(mountPath));
            if (string.IsNullOrEmpty(fileSystemType))
                throw new ArgumentNullException(nameof(fileSystemType));

            options ??= new MountOptions();
            
            // Normalize the mount path
            mountPath = NormalizePath(mountPath);

            lock (_lockObject)
            {
                // Check if already mounted
                if (_mountPoints.ContainsKey(mountPath))
                {
                    return false; // Already mounted
                }

                var mountPoint = new MountPoint(mountPath, source, fileSystemType, options)
                {
                    FileSystem = fileSystem
                };

                _mountPoints[mountPath] = mountPoint;

                MountPointAdded?.Invoke(this, new MountPointEventArgs(mountPoint));
                return true;
            }
        }

        /// <summary>
        /// Unmounts a file system from the specified path.
        /// </summary>
        /// <param name="mountPath">The path to unmount</param>
        /// <param name="force">Whether to force unmount even if busy</param>
        /// <returns>True if the unmount was successful; otherwise, false</returns>
        public bool Unmount(string mountPath, bool force = false)
        {
            if (string.IsNullOrEmpty(mountPath))
                throw new ArgumentNullException(nameof(mountPath));

            mountPath = NormalizePath(mountPath);

            lock (_lockObject)
            {
                if (!_mountPoints.TryGetValue(mountPath, out var mountPoint))
                {
                    return false; // Not mounted
                }

                // TODO: Check if the mount point is busy (has open files)
                // For now, just remove it

                mountPoint.IsActive = false;
                _mountPoints.Remove(mountPath);

                MountPointRemoved?.Invoke(this, new MountPointEventArgs(mountPoint));
                return true;
            }
        }

        /// <summary>
        /// Gets the mount point for a given path.
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>The mount point that contains the path, or null if none found</returns>
        public MountPoint? GetMountPoint(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            path = NormalizePath(path);

            lock (_lockObject)
            {
                // Find the longest matching mount path
                MountPoint? bestMatch = null;
                string bestMatchPath = "";

                foreach (var mountPoint in _mountPoints.Values)
                {
                    if (path.StartsWith(mountPoint.MountPath))
                    {
                        if (mountPoint.MountPath.Length > bestMatchPath.Length)
                        {
                            bestMatch = mountPoint;
                            bestMatchPath = mountPoint.MountPath;
                        }
                    }
                }

                return bestMatch;
            }
        }

        /// <summary>
        /// Checks if a path is mounted.
        /// </summary>
        /// <param name="mountPath">The path to check</param>
        /// <returns>True if the path is mounted; otherwise, false</returns>
        public bool IsMounted(string mountPath)
        {
            if (string.IsNullOrEmpty(mountPath))
                return false;

            mountPath = NormalizePath(mountPath);

            lock (_lockObject)
            {
                return _mountPoints.ContainsKey(mountPath);
            }
        }

        /// <summary>
        /// Gets information about all mount points in a format similar to /proc/mounts.
        /// </summary>
        /// <returns>A string containing mount information</returns>
        public string GetMountInfo()
        {
            lock (_lockObject)
            {
                var lines = new List<string>();
                
                foreach (var mountPoint in _mountPoints.Values)
                {
                    if (mountPoint.IsActive)
                    {
                        // Format: device mountpoint filesystem options dump pass
                        lines.Add($"{mountPoint.Source} {mountPoint.MountPath} {mountPoint.FileSystemType} {mountPoint.Options} 0 0");
                    }
                }

                return string.Join("\n", lines);
            }
        }

        /// <summary>
        /// Initializes standard Linux mount points.
        /// </summary>
        private void InitializeStandardMounts()
        {
            // Standard virtual file systems
            Mount("proc", "/proc", "proc", new MountOptions { ReadOnly = false, NoExec = true, NoSuid = true, NoDev = true });
            Mount("sysfs", "/sys", "sysfs", new MountOptions { ReadOnly = false, NoExec = true, NoSuid = true, NoDev = true });
            Mount("devtmpfs", "/dev", "devtmpfs", new MountOptions { ReadOnly = false, NoExec = false, NoSuid = true });
            Mount("tmpfs", "/tmp", "tmpfs", new MountOptions { ReadOnly = false, NoExec = false, NoSuid = true, NoDev = true });
            Mount("tmpfs", "/run", "tmpfs", new MountOptions { ReadOnly = false, NoExec = true, NoSuid = true, NoDev = true });
        }

        /// <summary>
        /// Normalizes a path for consistent mount point handling.
        /// </summary>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "/";

            // Remove trailing slashes except for root
            path = path.TrimEnd('/');
            if (string.IsNullOrEmpty(path))
                return "/";

            // Ensure it starts with /
            if (!path.StartsWith('/'))
                path = "/" + path;

            return path;
        }
    }

    /// <summary>
    /// Event arguments for mount point events.
    /// </summary>
    public class MountPointEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the mount point associated with this event.
        /// </summary>
        public MountPoint MountPoint { get; }

        /// <summary>
        /// Initializes a new instance of the MountPointEventArgs class.
        /// </summary>
        /// <param name="mountPoint">The mount point</param>
        public MountPointEventArgs(MountPoint mountPoint)
        {
            MountPoint = mountPoint ?? throw new ArgumentNullException(nameof(mountPoint));
        }
    }
}
