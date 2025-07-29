using HackerOs.OS.HSystem.Text;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HackerOs.OS.IO.FileSystem
{
    /// <summary>
    /// Represents a file descriptor that provides access to a virtual file with specific access modes and permissions.
    /// File descriptors are used to manage open files and provide controlled access to file operations.
    /// </summary>
    public class FileDescriptor : IDisposable
    {
        private readonly VirtualFile _file;
        private readonly FileAccess _access;
        private readonly FileShare _share;
        private readonly object _lockObject = new object();
        private bool _disposed = false;
        private long _position = 0;

        /// <summary>
        /// Initializes a new instance of the FileDescriptor class.
        /// </summary>
        /// <param name="file">The virtual file this descriptor refers to</param>
        /// <param name="access">The type of access requested</param>
        /// <param name="share">The type of access other file descriptors can have</param>
        internal FileDescriptor(VirtualFile file, FileAccess access, FileShare share)
        {
            _file = file ?? throw new ArgumentNullException(nameof(file));
            _access = access;
            _share = share;
            FileDescriptorId = Interlocked.Increment(ref _nextId);
            OpenTime = DateTime.UtcNow;
        }

        private static long _nextId = 0;

        /// <summary>
        /// Gets the unique identifier for this file descriptor.
        /// </summary>
        public long FileDescriptorId { get; }

        /// <summary>
        /// Gets the virtual file this descriptor refers to.
        /// </summary>
        public VirtualFile File => _file;

        /// <summary>
        /// Gets the access mode for this file descriptor.
        /// </summary>
        public FileAccess Access => _access;

        /// <summary>
        /// Gets the share mode for this file descriptor.
        /// </summary>
        public FileShare Share => _share;

        /// <summary>
        /// Gets the time when this file descriptor was opened.
        /// </summary>
        public DateTime OpenTime { get; }

        /// <summary>
        /// Gets or sets the current position within the file.
        /// </summary>
        public long Position
        {
            get
            {
                ThrowIfDisposed();
                lock (_lockObject)
                {
                    return _position;
                }
            }
            set
            {
                ThrowIfDisposed();
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Position cannot be negative.");

                lock (_lockObject)
                {
                    _position = value;
                }
            }
        }

        /// <summary>
        /// Gets the length of the file.
        /// </summary>
        public long Length
        {
            get
            {
                ThrowIfDisposed();
                return _file.Size;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the file descriptor can read.
        /// </summary>
        public bool CanRead => !_disposed && (_access & FileAccess.Read) == FileAccess.Read;

        /// <summary>
        /// Gets a value indicating whether the file descriptor can write.
        /// </summary>
        public bool CanWrite => !_disposed && (_access & FileAccess.Write) == FileAccess.Write;

        /// <summary>
        /// Gets a value indicating whether the file descriptor can seek.
        /// </summary>
        public bool CanSeek => !_disposed;

        /// <summary>
        /// Gets a value indicating whether this file descriptor is disposed.
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Reads data from the file at the current position.
        /// </summary>
        /// <param name="buffer">The buffer to read data into</param>
        /// <param name="offset">The offset in the buffer to start writing data</param>
        /// <param name="count">The maximum number of bytes to read</param>
        /// <returns>The number of bytes actually read</returns>
        public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            if (!CanRead)
                throw new InvalidOperationException("File descriptor does not have read access.");

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            lock (_lockObject)
            {
                if (_position >= _file.Size)
                    return 0; // End of file

                var content = _file.GetContent();
                var bytesToRead = Math.Min(count, (int)Math.Min(content.Length - _position, int.MaxValue));
                
                if (bytesToRead <= 0)
                    return 0;

                Array.Copy(content, _position, buffer, offset, bytesToRead);
                _position += bytesToRead;
                
                // Update last access time
                _file.UpdateLastAccessTime();
                
                return bytesToRead;
            }
        }

        /// <summary>
        /// Writes data to the file at the current position.
        /// </summary>
        /// <param name="buffer">The buffer containing data to write</param>
        /// <param name="offset">The offset in the buffer to start reading data</param>
        /// <param name="count">The number of bytes to write</param>
        public async Task WriteAsync(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            if (!CanWrite)
                throw new InvalidOperationException("File descriptor does not have write access.");

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            lock (_lockObject)
            {
                var content = _file.GetContent();
                var newSize = Math.Max(content.Length, _position + count);
                
                // Resize content array if necessary
                if (newSize > content.Length)
                {
                    var newContent = new byte[newSize];
                    Array.Copy(content, newContent, content.Length);
                    content = newContent;
                }

                // Write the data
                Array.Copy(buffer, offset, content, _position, count);
                _position += count;

                // Update the file content
                _file.SetContent(content);
            }
        }

        /// <summary>
        /// Sets the position within the file.
        /// </summary>
        /// <param name="offset">The offset to seek to</param>
        /// <param name="origin">The origin of the seek operation</param>
        /// <returns>The new position within the file</returns>
        public long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            if (!CanSeek)
                throw new InvalidOperationException("File descriptor does not support seeking.");

            lock (_lockObject)
            {
                long newPosition = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => _position + offset,
                    SeekOrigin.End => _file.Size + offset,
                    _ => throw new ArgumentException("Invalid seek origin.", nameof(origin))
                };

                if (newPosition < 0)
                    throw new ArgumentOutOfRangeException(nameof(offset), "Seek position cannot be negative.");

                _position = newPosition;
                return _position;
            }
        }

        /// <summary>
        /// Flushes any buffered data to the underlying storage.
        /// </summary>
        public async Task FlushAsync()
        {
            ThrowIfDisposed();
            // In our virtual file system, changes are immediately persistent
            // This method is provided for compatibility with standard file operations
            await Task.CompletedTask;
        }

        /// <summary>
        /// Sets the length of the file.
        /// </summary>
        /// <param name="value">The new length of the file</param>
        public void SetLength(long value)
        {
            ThrowIfDisposed();
            if (!CanWrite)
                throw new InvalidOperationException("File descriptor does not have write access.");

            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "File length cannot be negative.");

            lock (_lockObject)
            {
                var content = _file.GetContent();
                
                if (value == content.Length)
                    return; // No change needed

                var newContent = new byte[value];
                
                if (value > 0)
                {
                    var copyLength = Math.Min(content.Length, value);
                    Array.Copy(content, newContent, copyLength);
                }

                _file.SetContent(newContent);

                // Adjust position if it's beyond the new end of file
                if (_position > value)
                    _position = value;
            }
        }

        /// <summary>
        /// Reads all text from the file using the specified encoding.
        /// </summary>
        /// <param name="encoding">The encoding to use for reading text</param>
        /// <returns>The text content of the file</returns>
        public async Task<string> ReadAllTextAsync(Encoding? encoding = null)
        {
            ThrowIfDisposed();
            if (!CanRead)
                throw new InvalidOperationException("File descriptor does not have read access.");

            encoding ??= Encoding.UTF8;
            
            lock (_lockObject)
            {
                var content = _file.GetContent();
                _file.UpdateLastAccessTime();
                return encoding.GetString(content);
            }
        }

        /// <summary>
        /// Writes all text to the file using the specified encoding.
        /// </summary>
        /// <param name="text">The text to write</param>
        /// <param name="encoding">The encoding to use for writing text</param>
        public async Task WriteAllTextAsync(string text, Encoding? encoding = null)
        {
            ThrowIfDisposed();
            if (!CanWrite)
                throw new InvalidOperationException("File descriptor does not have write access.");

            encoding ??= Encoding.UTF8;
            text ??= string.Empty;

            lock (_lockObject)
            {
                var content = encoding.GetBytes(text);
                _file.SetContent(content);
                _position = content.Length;
            }
        }

        /// <summary>
        /// Throws an ObjectDisposedException if this file descriptor has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileDescriptor));
        }

        /// <summary>
        /// Releases all resources used by the FileDescriptor.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the FileDescriptor and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Remove this descriptor from any tracking collections
                    FileDescriptorManager.Instance.CloseDescriptor(this);
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Returns a string representation of this file descriptor.
        /// </summary>
        /// <returns>A string representation of this file descriptor</returns>
        public override string ToString()
        {
            return $"FileDescriptor {{ Id={FileDescriptorId}, File={_file.Path}, Access={_access}, Share={_share}, Position={_position} }}";
        }
    }

    /// <summary>
    /// Specifies the access level for a file.
    /// </summary>
    [Flags]
    public enum FileAccess
    {
        /// <summary>
        /// Read access to the file.
        /// </summary>
        Read = 1,

        /// <summary>
        /// Write access to the file.
        /// </summary>
        Write = 2,

        /// <summary>
        /// Read and write access to the file.
        /// </summary>
        ReadWrite = Read | Write
    }

    /// <summary>
    /// Specifies how other file descriptors can access the same file.
    /// </summary>
    [Flags]
    public enum FileShare
    {
        /// <summary>
        /// No sharing. Any request to open the file will fail until the file is closed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Allows subsequent opening of the file for reading.
        /// </summary>
        Read = 1,

        /// <summary>
        /// Allows subsequent opening of the file for writing.
        /// </summary>
        Write = 2,

        /// <summary>
        /// Allows subsequent opening of the file for reading or writing.
        /// </summary>
        ReadWrite = Read | Write,

        /// <summary>
        /// Allows subsequent deleting of the file.
        /// </summary>
        Delete = 4
    }

    /// <summary>
    /// Specifies the position in a stream to use for seeking.
    /// </summary>
    public enum SeekOrigin
    {
        /// <summary>
        /// Specifies the beginning of a stream.
        /// </summary>
        Begin = 0,

        /// <summary>
        /// Specifies the current position within a stream.
        /// </summary>
        Current = 1,

        /// <summary>
        /// Specifies the end of a stream.
        /// </summary>
        End = 2
    }
}
