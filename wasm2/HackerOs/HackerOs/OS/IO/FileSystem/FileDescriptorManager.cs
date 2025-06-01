using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HackerOs.IO.FileSystem
{
    /// <summary>
    /// Manages file descriptors and provides file locking mechanisms for the virtual file system.
    /// This class ensures proper resource management and prevents file access conflicts.
    /// </summary>
    public class FileDescriptorManager
    {
        private static readonly Lazy<FileDescriptorManager> _instance = new(() => new FileDescriptorManager());
        private readonly ConcurrentDictionary<long, FileDescriptor> _openDescriptors = new();
        private readonly ConcurrentDictionary<string, List<FileDescriptor>> _fileDescriptorsByPath = new();
        private readonly ConcurrentDictionary<string, FileLock> _fileLocks = new();
        private readonly object _lockObject = new object();

        /// <summary>
        /// Gets the singleton instance of the FileDescriptorManager.
        /// </summary>
        public static FileDescriptorManager Instance => _instance.Value;

        private FileDescriptorManager()
        {
        }

        /// <summary>
        /// Opens a file descriptor for the specified file with the given access and share modes.
        /// </summary>
        /// <param name="file">The virtual file to open</param>
        /// <param name="access">The type of access requested</param>
        /// <param name="share">The type of access other file descriptors can have</param>
        /// <returns>A new file descriptor for the file</returns>
        /// <exception cref="IOException">Thrown when the file cannot be opened due to sharing violations</exception>
        public FileDescriptor OpenFile(VirtualFile file, FileAccess access, FileShare share)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            lock (_lockObject)
            {
                // Check for sharing violations
                ValidateFileAccess(file.Path, access, share);

                var descriptor = new FileDescriptor(file, access, share);
                
                // Track the descriptor
                _openDescriptors[descriptor.FileDescriptorId] = descriptor;
                
                var pathDescriptors = _fileDescriptorsByPath.GetOrAdd(file.Path, _ => new List<FileDescriptor>());
                lock (pathDescriptors)
                {
                    pathDescriptors.Add(descriptor);
                }

                return descriptor;
            }
        }

        /// <summary>
        /// Closes and removes tracking for the specified file descriptor.
        /// </summary>
        /// <param name="descriptor">The file descriptor to close</param>
        public void CloseDescriptor(FileDescriptor descriptor)
        {
            if (descriptor == null)
                return;

            lock (_lockObject)
            {
                _openDescriptors.TryRemove(descriptor.FileDescriptorId, out _);

                if (_fileDescriptorsByPath.TryGetValue(descriptor.File.Path, out var pathDescriptors))
                {
                    lock (pathDescriptors)
                    {
                        pathDescriptors.Remove(descriptor);
                        
                        // Remove empty lists
                        if (pathDescriptors.Count == 0)
                        {
                            _fileDescriptorsByPath.TryRemove(descriptor.File.Path, out _);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets all open file descriptors for a specific file path.
        /// </summary>
        /// <param name="path">The file path to check</param>
        /// <returns>A list of open file descriptors for the specified path</returns>
        public List<FileDescriptor> GetOpenDescriptors(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new List<FileDescriptor>();

            if (_fileDescriptorsByPath.TryGetValue(path, out var pathDescriptors))
            {
                lock (pathDescriptors)
                {
                    return new List<FileDescriptor>(pathDescriptors);
                }
            }

            return new List<FileDescriptor>();
        }

        /// <summary>
        /// Gets all currently open file descriptors.
        /// </summary>
        /// <returns>A list of all open file descriptors</returns>
        public List<FileDescriptor> GetAllOpenDescriptors()
        {
            return _openDescriptors.Values.ToList();
        }

        /// <summary>
        /// Checks if a file is currently open by any descriptor.
        /// </summary>
        /// <param name="path">The file path to check</param>
        /// <returns>true if the file is open; otherwise, false</returns>
        public bool IsFileOpen(string path)
        {
            return GetOpenDescriptors(path).Count > 0;
        }

        /// <summary>
        /// Acquires an exclusive lock on the specified file.
        /// </summary>
        /// <param name="path">The path of the file to lock</param>
        /// <param name="lockType">The type of lock to acquire</param>
        /// <param name="cancellationToken">A cancellation token to cancel the lock operation</param>
        /// <returns>A file lock object that must be disposed to release the lock</returns>
        public async Task<FileLock> AcquireLockAsync(string path, FileLockType lockType, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            while (!cancellationToken.IsCancellationRequested)
            {
                lock (_lockObject)
                {
                    // Check if we can acquire the lock
                    if (CanAcquireLock(path, lockType))
                    {
                        var fileLock = new FileLock(path, lockType, this);
                        _fileLocks[path] = fileLock;
                        return fileLock;
                    }
                }

                // Wait a bit before retrying
                await Task.Delay(10, cancellationToken);
            }

            throw new OperationCanceledException("File lock acquisition was cancelled.", cancellationToken);
        }

        /// <summary>
        /// Releases a file lock.
        /// </summary>
        /// <param name="fileLock">The file lock to release</param>
        internal void ReleaseLock(FileLock fileLock)
        {
            if (fileLock == null)
                return;

            lock (_lockObject)
            {
                _fileLocks.TryRemove(fileLock.Path, out _);
            }
        }

        /// <summary>
        /// Validates that a file can be opened with the specified access and share modes.
        /// </summary>
        /// <param name="path">The path of the file to open</param>
        /// <param name="access">The requested access mode</param>
        /// <param name="share">The requested share mode</param>
        /// <exception cref="IOException">Thrown when the file cannot be opened due to sharing violations</exception>
        private void ValidateFileAccess(string path, FileAccess access, FileShare share)
        {
            var existingDescriptors = GetOpenDescriptors(path);
            
            foreach (var existing in existingDescriptors)
            {
                // Check if existing descriptor allows sharing
                if (existing.Share == FileShare.None)
                {
                    throw new IOException($"The file '{path}' is already open with exclusive access.");
                }

                // Check read access
                if ((access & FileAccess.Read) == FileAccess.Read && 
                    (existing.Share & FileShare.Read) != FileShare.Read)
                {
                    throw new IOException($"The file '{path}' cannot be opened for reading due to sharing restrictions.");
                }

                // Check write access
                if ((access & FileAccess.Write) == FileAccess.Write && 
                    (existing.Share & FileShare.Write) != FileShare.Write)
                {
                    throw new IOException($"The file '{path}' cannot be opened for writing due to sharing restrictions.");
                }

                // Check if new descriptor allows sharing with existing ones
                if (share == FileShare.None && existingDescriptors.Count > 0)
                {
                    throw new IOException($"The file '{path}' cannot be opened with exclusive access because it is already open.");
                }

                // Check if existing descriptor needs read access but new one doesn't allow it
                if ((existing.Access & FileAccess.Read) == FileAccess.Read && 
                    (share & FileShare.Read) != FileShare.Read)
                {
                    throw new IOException($"The file '{path}' cannot be opened because it would deny read access to existing handles.");
                }

                // Check if existing descriptor needs write access but new one doesn't allow it
                if ((existing.Access & FileAccess.Write) == FileAccess.Write && 
                    (share & FileShare.Write) != FileShare.Write)
                {
                    throw new IOException($"The file '{path}' cannot be opened because it would deny write access to existing handles.");
                }
            }
        }

        /// <summary>
        /// Determines if a lock can be acquired for the specified file and lock type.
        /// </summary>
        /// <param name="path">The path of the file to lock</param>
        /// <param name="lockType">The type of lock to acquire</param>
        /// <returns>true if the lock can be acquired; otherwise, false</returns>
        private bool CanAcquireLock(string path, FileLockType lockType)
        {
            // Check if there's already a lock on this file
            if (_fileLocks.TryGetValue(path, out var existingLock))
            {
                // Exclusive locks cannot coexist with any other locks
                if (existingLock.LockType == FileLockType.Exclusive || lockType == FileLockType.Exclusive)
                {
                    return false;
                }

                // Shared locks can coexist with other shared locks
                return lockType == FileLockType.Shared && existingLock.LockType == FileLockType.Shared;
            }

            return true;
        }

        /// <summary>
        /// Gets statistics about currently open file descriptors.
        /// </summary>
        /// <returns>File descriptor statistics</returns>
        public FileDescriptorStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                var stats = new FileDescriptorStatistics
                {
                    TotalOpenDescriptors = _openDescriptors.Count,
                    UniqueFilesOpen = _fileDescriptorsByPath.Count,
                    LockedFiles = _fileLocks.Count
                };

                // Count descriptors by access type
                foreach (var descriptor in _openDescriptors.Values)
                {
                    if ((descriptor.Access & FileAccess.Read) == FileAccess.Read)
                        stats.ReadOnlyDescriptors++;
                    if ((descriptor.Access & FileAccess.Write) == FileAccess.Write)
                        stats.WriteDescriptors++;
                    if (descriptor.Access == FileAccess.ReadWrite)
                        stats.ReadWriteDescriptors++;
                }

                return stats;
            }
        }

        /// <summary>
        /// Closes all open file descriptors. This should only be used during system shutdown.
        /// </summary>
        public void CloseAllDescriptors()
        {
            lock (_lockObject)
            {
                var descriptors = _openDescriptors.Values.ToList();
                
                foreach (var descriptor in descriptors)
                {
                    try
                    {
                        descriptor.Dispose();
                    }
                    catch
                    {
                        // Ignore errors during shutdown
                    }
                }

                _openDescriptors.Clear();
                _fileDescriptorsByPath.Clear();
                _fileLocks.Clear();
            }
        }
    }

    /// <summary>
    /// Represents a file lock that provides exclusive or shared access to a file.
    /// </summary>
    public class FileLock : IDisposable
    {
        private readonly FileDescriptorManager _manager;
        private bool _disposed = false;

        internal FileLock(string path, FileLockType lockType, FileDescriptorManager manager)
        {
            Path = path;
            LockType = lockType;
            AcquiredTime = DateTime.UtcNow;
            _manager = manager;
        }

        /// <summary>
        /// Gets the path of the locked file.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the type of lock.
        /// </summary>
        public FileLockType LockType { get; }

        /// <summary>
        /// Gets the time when this lock was acquired.
        /// </summary>
        public DateTime AcquiredTime { get; }

        /// <summary>
        /// Gets a value indicating whether this lock has been disposed.
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Releases the file lock.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _manager.ReleaseLock(this);
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Returns a string representation of this file lock.
        /// </summary>
        /// <returns>A string representation of this file lock</returns>
        public override string ToString()
        {
            return $"FileLock {{ Path={Path}, LockType={LockType}, AcquiredTime={AcquiredTime} }}";
        }
    }

    /// <summary>
    /// Specifies the type of file lock.
    /// </summary>
    public enum FileLockType
    {
        /// <summary>
        /// A shared lock that allows multiple readers but no writers.
        /// </summary>
        Shared,

        /// <summary>
        /// An exclusive lock that allows only one accessor.
        /// </summary>
        Exclusive
    }

    /// <summary>
    /// Provides statistics about file descriptor usage.
    /// </summary>
    public class FileDescriptorStatistics
    {
        /// <summary>
        /// Gets or sets the total number of open file descriptors.
        /// </summary>
        public int TotalOpenDescriptors { get; set; }

        /// <summary>
        /// Gets or sets the number of unique files that are open.
        /// </summary>
        public int UniqueFilesOpen { get; set; }

        /// <summary>
        /// Gets or sets the number of files that are currently locked.
        /// </summary>
        public int LockedFiles { get; set; }

        /// <summary>
        /// Gets or sets the number of read-only descriptors.
        /// </summary>
        public int ReadOnlyDescriptors { get; set; }

        /// <summary>
        /// Gets or sets the number of write-enabled descriptors.
        /// </summary>
        public int WriteDescriptors { get; set; }

        /// <summary>
        /// Gets or sets the number of read-write descriptors.
        /// </summary>
        public int ReadWriteDescriptors { get; set; }

        /// <summary>
        /// Returns a string representation of these statistics.
        /// </summary>
        /// <returns>A string representation of these statistics</returns>
        public override string ToString()
        {
            return $"FileDescriptorStatistics {{ " +
                   $"Total={TotalOpenDescriptors}, " +
                   $"UniqueFiles={UniqueFilesOpen}, " +
                   $"Locked={LockedFiles}, " +
                   $"ReadOnly={ReadOnlyDescriptors}, " +
                   $"Write={WriteDescriptors}, " +
                   $"ReadWrite={ReadWriteDescriptors} }}";
        }
    }
}
