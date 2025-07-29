using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HackerOs.OS.Shell;

/// <summary>
/// Represents a bidirectional stream for pipeline data transfer
/// </summary>
internal class PipelineStream : IAsyncDisposable, IDisposable
{
    private readonly MemoryStream _buffer;
    private readonly SemaphoreSlim _readSemaphore;
    private readonly SemaphoreSlim _writeSemaphore;
    private readonly object _stateLock = new();
    
    private StreamState _state = StreamState.Initializing;
    private bool _outputClosed = false;
    private bool _disposed = false;
    
    public string Id { get; }
    public StreamType Type { get; }
    public DateTime CreatedAt { get; }
    public DateTime? ClosedAt { get; private set; }
    public Exception? LastError { get; private set; }
    public int BufferSize { get; }
    
    public long BytesRead { get; private set; }
    public long BytesWritten { get; private set; }
    
    public StreamState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
        private set
        {
            lock (_stateLock)
            {
                _state = value;
            }
        }
    }
    
    public Stream InputStream { get; }
    public Stream OutputStream { get; }

    public PipelineStream(string id, StreamType type, int bufferSize = 8192)
    {
        Id = id;
        Type = type;
        CreatedAt = DateTime.UtcNow;
        BufferSize = bufferSize;
        
        _buffer = new MemoryStream();
        _readSemaphore = new SemaphoreSlim(1, 1);
        _writeSemaphore = new SemaphoreSlim(1, 1);
        
        // Create wrapper streams for input and output
        InputStream = new PipelineInputStream(this);
        OutputStream = new PipelineOutputStream(this);
        
        State = StreamState.Ready;
    }

    public void Close(bool closeInput = true, bool closeOutput = true)
    {
        try
        {
            if (closeOutput && !_outputClosed)
            {
                _outputClosed = true;
                State = StreamState.Closed;
                ClosedAt = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            LastError = ex;
            State = StreamState.Error;
        }
    }

    public async Task CloseOutputAsync()
    {
        await Task.Run(() => Close(false, true));
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
    {
        await _readSemaphore.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            
            if (State == StreamState.Error)
            {
                throw new InvalidOperationException("Stream is in error state", LastError);
            }
            
            State = StreamState.Active;
            
            // Read from the internal buffer
            var bytesRead = await _buffer.ReadAsync(buffer, offset, count, cancellationToken);
            BytesRead += bytesRead;
            
            if (bytesRead == 0 && _outputClosed)
            {
                State = StreamState.Closed;
            }
            
            return bytesRead;
        }
        catch (Exception ex)
        {
            LastError = ex;
            State = StreamState.Error;
            throw;
        }
        finally
        {
            _readSemaphore.Release();
        }
    }

    public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
    {
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            
            if (_outputClosed)
            {
                throw new InvalidOperationException("Cannot write to closed stream");
            }
            
            if (State == StreamState.Error)
            {
                throw new InvalidOperationException("Stream is in error state", LastError);
            }
            
            State = StreamState.Active;
            
            // Write to the internal buffer
            await _buffer.WriteAsync(buffer, offset, count, cancellationToken);
            await _buffer.FlushAsync(cancellationToken);
            BytesWritten += count;
        }
        catch (Exception ex)
        {
            LastError = ex;
            State = StreamState.Error;
            throw;
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        
        try
        {
            Close(true, true);
            
            await _readSemaphore.WaitAsync();
            await _writeSemaphore.WaitAsync();
            
            InputStream?.Dispose();
            OutputStream?.Dispose();
            _buffer?.Dispose();
            _readSemaphore?.Dispose();
            _writeSemaphore?.Dispose();
            
            _disposed = true;
        }
        catch (Exception ex)
        {
            LastError = ex;
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PipelineStream));
        }
    }
}

/// <summary>
/// Input stream wrapper for PipelineStream
/// </summary>
internal class PipelineInputStream : Stream
{
    private readonly PipelineStream _pipelineStream;

    public PipelineInputStream(PipelineStream pipelineStream)
    {
        _pipelineStream = pipelineStream;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return await _pipelineStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() { }
}

/// <summary>
/// Output stream wrapper for PipelineStream
/// </summary>
internal class PipelineOutputStream : Stream
{
    private readonly PipelineStream _pipelineStream;

    public PipelineOutputStream(PipelineStream pipelineStream)
    {
        _pipelineStream = pipelineStream;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _pipelineStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        WriteAsync(buffer, offset, count).Wait();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Flush() { }
}
