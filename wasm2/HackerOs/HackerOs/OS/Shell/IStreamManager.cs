using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HackerOs.OS.Shell;

/// <summary>
/// Interface for managing data streams between commands in pipelines
/// </summary>
public interface IStreamManager : IDisposable
{
    /// <summary>
    /// Creates a new pipeline stream for data transfer between commands
    /// </summary>
    /// <param name="streamType">The type of data that will flow through the stream</param>
    /// <param name="bufferSize">The size of the buffer for the stream</param>
    /// <returns>A unique identifier for the created stream</returns>
    string CreateStream(StreamType streamType, int bufferSize = 8192);
    
    /// <summary>
    /// Gets the input stream for reading data
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream</param>
    /// <returns>A stream for reading data</returns>
    Stream GetInputStream(string streamId);
    
    /// <summary>
    /// Gets the output stream for writing data
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream</param>
    /// <returns>A stream for writing data</returns>
    Stream GetOutputStream(string streamId);
    
    /// <summary>
    /// Closes the output side of a stream, signaling no more data will be written
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream</param>
    Task CloseOutputAsync(string streamId);
    
    /// <summary>
    /// Closes both input and output sides of a stream and releases resources
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream</param>
    Task CloseStreamAsync(string streamId);
    
    /// <summary>
    /// Gets the current state of a stream
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream</param>
    /// <returns>The current state of the stream</returns>
    StreamState GetStreamState(string streamId);
    
    /// <summary>
    /// Gets the type of data flowing through a stream
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream</param>
    /// <returns>The type of data in the stream</returns>
    StreamType GetStreamType(string streamId);
    
    /// <summary>
    /// Negotiates the stream type between two commands
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream</param>
    /// <param name="requestedType">The type requested by the consuming command</param>
    /// <returns>The negotiated stream type</returns>
    StreamType NegotiateStreamType(string streamId, StreamType requestedType);
    
    /// <summary>
    /// Reads text data from a stream with proper encoding handling
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream</param>
    /// <param name="encoding">The encoding to use for text conversion</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The text data from the stream</returns>
    Task<string> ReadTextAsync(string streamId, Encoding? encoding = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Writes text data to a stream with proper encoding handling
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream</param>
    /// <param name="text">The text data to write</param>
    /// <param name="encoding">The encoding to use for text conversion</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    Task WriteTextAsync(string streamId, string text, Encoding? encoding = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reads binary data from a stream
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The binary data from the stream</returns>
    Task<byte[]> ReadBinaryAsync(string streamId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Writes binary data to a stream
    /// </summary>
    /// <param name="streamId">The unique identifier of the stream</param>
    /// <param name="data">The binary data to write</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    Task WriteBinaryAsync(string streamId, byte[] data, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets information about all active streams
    /// </summary>
    /// <returns>Dictionary of stream IDs and their information</returns>
    IReadOnlyDictionary<string, StreamInfo> GetActiveStreams();
}

/// <summary>
/// Information about a stream
/// </summary>
public record StreamInfo(
    string Id,
    StreamType Type,
    StreamState State,
    long BytesRead,
    long BytesWritten,
    DateTime CreatedAt,
    DateTime? ClosedAt = null,
    Exception? LastError = null
);
