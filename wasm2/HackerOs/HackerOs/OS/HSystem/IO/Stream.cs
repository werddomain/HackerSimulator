using System;

namespace HackerOs.OS.HSystem.IO
{
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

    /// <summary>
    /// Provides a generic view of a sequence of bytes in the HackerOS system.
    /// This is a virtual implementation that wraps or extends .NET's System.IO.Stream.
    /// </summary>
    public abstract class HStream : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        public abstract bool CanRead { get; }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        public abstract bool CanSeek { get; }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        public abstract bool CanWrite { get; }

        /// <summary>
        /// Gets the length in bytes of the stream.
        /// </summary>
        public abstract long Length { get; }

        /// <summary>
        /// Gets or sets the position within the current stream.
        /// </summary>
        public abstract long Position { get; set; }

        /// <summary>
        /// Reads data from the stream.
        /// </summary>
        public abstract int Read(byte[] buffer, int offset, int count);

        /// <summary>
        /// Writes data to the stream.
        /// </summary>
        public abstract void Write(byte[] buffer, int offset, int count);

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        public abstract long Seek(long offset, SeekOrigin origin);

        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        public abstract void SetLength(long value);

        /// <summary>
        /// Flushes any buffered data to the underlying storage.
        /// </summary>
        public abstract void Flush();

        /// <summary>
        /// Releases all resources used by the Stream.
        /// </summary>
        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the Stream and optionally releases the managed resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            // Base implementation does nothing
        }
    }

    /// <summary>
    /// Exception thrown when an I/O error occurs.
    /// </summary>
    public class IOException : Exception
    {
        public IOException() : base() { }
        public IOException(string message) : base(message) { }
        public IOException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when an invalid operation is attempted on a directory.
    /// </summary>
    public class DirectoryNotFoundException : IOException
    {
        public DirectoryNotFoundException() : base() { }
        public DirectoryNotFoundException(string message) : base(message) { }
        public DirectoryNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when a file is not found.
    /// </summary>
    public class FileNotFoundException : IOException
    {
        public FileNotFoundException() : base() { }
        public FileNotFoundException(string message) : base(message) { }
        public FileNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }
}
