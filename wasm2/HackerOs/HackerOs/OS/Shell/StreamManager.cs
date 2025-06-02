using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HackerOs.OS.Shell;

/// <summary>
/// Manages data streams between commands in pipelines
/// </summary>
public class StreamManager : IStreamManager
{
    private readonly ConcurrentDictionary<string, PipelineStream> _streams = new();
    private readonly object _disposeLock = new();
    private bool _disposed = false;

    /// <inheritdoc />
    public string CreateStream(StreamType streamType, int bufferSize = 8192)
    {
        ThrowIfDisposed();
        
        var streamId = Guid.NewGuid().ToString();
        var pipelineStream = new PipelineStream(streamId, streamType, bufferSize);
        
        if (!_streams.TryAdd(streamId, pipelineStream))
        {
            pipelineStream.Dispose();
            throw new InvalidOperationException($"Failed to create stream with ID: {streamId}");
        }
        
        return streamId;
    }

    /// <inheritdoc />
    public Stream GetInputStream(string streamId)
    {
        ThrowIfDisposed();
        
        if (!_streams.TryGetValue(streamId, out var pipelineStream))
        {
            throw new ArgumentException($"Stream not found: {streamId}", nameof(streamId));
        }
        
        return pipelineStream.InputStream;
    }

    /// <inheritdoc />
    public Stream GetOutputStream(string streamId)
    {
        ThrowIfDisposed();
        
        if (!_streams.TryGetValue(streamId, out var pipelineStream))
        {
            throw new ArgumentException($"Stream not found: {streamId}", nameof(streamId));
        }
        
        return pipelineStream.OutputStream;
    }

    /// <inheritdoc />
    public async Task CloseOutputAsync(string streamId)
    {
        ThrowIfDisposed();
        
        if (_streams.TryGetValue(streamId, out var pipelineStream))
        {
            await pipelineStream.CloseOutputAsync();
        }
    }

    /// <inheritdoc />
    public async Task CloseStreamAsync(string streamId)
    {
        if (_streams.TryRemove(streamId, out var pipelineStream))
        {
            await pipelineStream.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public StreamState GetStreamState(string streamId)
    {
        ThrowIfDisposed();
        
        if (!_streams.TryGetValue(streamId, out var pipelineStream))
        {
            throw new ArgumentException($"Stream not found: {streamId}", nameof(streamId));
        }
        
        return pipelineStream.State;
    }

    /// <inheritdoc />
    public StreamType GetStreamType(string streamId)
    {
        ThrowIfDisposed();
        
        if (!_streams.TryGetValue(streamId, out var pipelineStream))
        {
            throw new ArgumentException($"Stream not found: {streamId}", nameof(streamId));
        }
        
        return pipelineStream.Type;
    }

    /// <inheritdoc />
    public StreamType NegotiateStreamType(string streamId, StreamType requestedType)
    {
        ThrowIfDisposed();
        
        if (!_streams.TryGetValue(streamId, out var pipelineStream))
        {
            throw new ArgumentException($"Stream not found: {streamId}", nameof(streamId));
        }
          // Simple negotiation logic - prefer text over binary, mixed as fallback
        return (pipelineStream.Type, requestedType) switch
        {
            (StreamType.Text, StreamType.Text) => StreamType.Text,
            (StreamType.Binary, StreamType.Binary) => StreamType.Binary,
            (StreamType.Mixed, _) => StreamType.Mixed,
            (_, StreamType.Mixed) => StreamType.Mixed,
            (StreamType.Text, StreamType.Binary) => StreamType.Mixed,
            (StreamType.Binary, StreamType.Text) => StreamType.Mixed,
            _ => pipelineStream.Type
        };
    }

    /// <inheritdoc />
    public async Task<string> ReadTextAsync(string streamId, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!_streams.TryGetValue(streamId, out var pipelineStream))
        {
            throw new ArgumentException($"Stream not found: {streamId}", nameof(streamId));
        }
        
        encoding ??= Encoding.UTF8;
        
        using var reader = new StreamReader(pipelineStream.InputStream, encoding, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task WriteTextAsync(string streamId, string text, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!_streams.TryGetValue(streamId, out var pipelineStream))
        {
            throw new ArgumentException($"Stream not found: {streamId}", nameof(streamId));
        }
        
        encoding ??= Encoding.UTF8;
        var bytes = encoding.GetBytes(text);
        
        await pipelineStream.OutputStream.WriteAsync(bytes, cancellationToken);
        await pipelineStream.OutputStream.FlushAsync(cancellationToken);
    }    /// <inheritdoc />
    public async Task<byte[]> ReadBinaryAsync(string streamId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!_streams.TryGetValue(streamId, out var pipelineStream))
        {
            throw new ArgumentException($"Stream not found: {streamId}", nameof(streamId));
        }
        
        using var memoryStream = new MemoryStream();
        await pipelineStream.InputStream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    /// <inheritdoc />
    public async Task WriteBinaryAsync(string streamId, byte[] data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!_streams.TryGetValue(streamId, out var pipelineStream))
        {
            throw new ArgumentException($"Stream not found: {streamId}", nameof(streamId));
        }
        
        await pipelineStream.OutputStream.WriteAsync(data, cancellationToken);
        await pipelineStream.OutputStream.FlushAsync(cancellationToken);
    }    /// <inheritdoc />
    public IReadOnlyDictionary<string, StreamInfo> GetActiveStreams()
    {
        ThrowIfDisposed();
        
        var result = new Dictionary<string, StreamInfo>();
        
        foreach (var (streamId, pipelineStream) in _streams)
        {
            result[streamId] = new StreamInfo(
                streamId,
                pipelineStream.Type,
                pipelineStream.State,
                pipelineStream.BytesRead,
                pipelineStream.BytesWritten,
                pipelineStream.CreatedAt,
                pipelineStream.ClosedAt,
                pipelineStream.LastError
            );
        }
        
        return result;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        
        lock (_disposeLock)
        {
            if (_disposed) return;
            
            // Close all streams
            foreach (var (_, pipelineStream) in _streams)
            {
                try
                {
                    pipelineStream.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
            
            _streams.Clear();
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(StreamManager));
        }
    }
}
