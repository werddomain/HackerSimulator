using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HackerOs.OS.Shell;
using Xunit;

namespace HackerOs.Tests.Shell;

public class StreamManagerTests : IDisposable
{
    private readonly StreamManager _streamManager;

    public StreamManagerTests()
    {
        _streamManager = new StreamManager();
    }

    public void Dispose()
    {
        _streamManager?.Dispose();
    }

    [Fact]
    public void CreateStream_ShouldReturnUniqueStreamId()
    {
        // Act
        var streamId1 = _streamManager.CreateStream(StreamType.Text);
        var streamId2 = _streamManager.CreateStream(StreamType.Binary);

        // Assert
        Assert.NotNull(streamId1);
        Assert.NotNull(streamId2);
        Assert.NotEqual(streamId1, streamId2);
    }

    [Fact]
    public void GetInputStream_ShouldReturnValidStream()
    {
        // Arrange
        var streamId = _streamManager.CreateStream(StreamType.Text);

        // Act
        var inputStream = _streamManager.GetInputStream(streamId);

        // Assert
        Assert.NotNull(inputStream);
        Assert.True(inputStream.CanRead);
        Assert.False(inputStream.CanWrite);
    }

    [Fact]
    public void GetOutputStream_ShouldReturnValidStream()
    {
        // Arrange
        var streamId = _streamManager.CreateStream(StreamType.Text);

        // Act
        var outputStream = _streamManager.GetOutputStream(streamId);

        // Assert
        Assert.NotNull(outputStream);
        Assert.False(outputStream.CanRead);
        Assert.True(outputStream.CanWrite);
    }

    [Fact]
    public void GetStreamState_ShouldReturnCorrectState()
    {
        // Arrange
        var streamId = _streamManager.CreateStream(StreamType.Text);

        // Act
        var state = _streamManager.GetStreamState(streamId);

        // Assert
        Assert.Equal(StreamState.Ready, state);
    }

    [Fact]
    public void GetStreamType_ShouldReturnCorrectType()
    {
        // Arrange
        var streamId = _streamManager.CreateStream(StreamType.Binary);

        // Act
        var type = _streamManager.GetStreamType(streamId);

        // Assert
        Assert.Equal(StreamType.Binary, type);
    }

    [Fact]
    public async Task WriteTextAsync_ReadTextAsync_ShouldTransferData()
    {
        // Arrange
        var streamId = _streamManager.CreateStream(StreamType.Text);
        var testText = "Hello, World!";

        // Act
        await _streamManager.WriteTextAsync(streamId, testText);
        await _streamManager.CloseOutputAsync(streamId);
        var result = await _streamManager.ReadTextAsync(streamId);

        // Assert
        Assert.Equal(testText, result);
    }

    [Fact]
    public async Task WriteBinaryAsync_ReadBinaryAsync_ShouldTransferData()
    {
        // Arrange
        var streamId = _streamManager.CreateStream(StreamType.Binary);
        var testData = Encoding.UTF8.GetBytes("Binary test data");

        // Act
        await _streamManager.WriteBinaryAsync(streamId, testData);
        await _streamManager.CloseOutputAsync(streamId);
        var result = await _streamManager.ReadBinaryAsync(streamId);

        // Assert
        Assert.Equal(testData, result);
    }

    [Theory]
    [InlineData(StreamType.Text, StreamType.Text, StreamType.Text)]
    [InlineData(StreamType.Binary, StreamType.Binary, StreamType.Binary)]
    [InlineData(StreamType.Text, StreamType.Binary, StreamType.Mixed)]
    [InlineData(StreamType.Binary, StreamType.Text, StreamType.Mixed)]
    [InlineData(StreamType.Mixed, StreamType.Text, StreamType.Mixed)]
    [InlineData(StreamType.Mixed, StreamType.Binary, StreamType.Mixed)]
    public void NegotiateStreamType_ShouldReturnCorrectType(StreamType originalType, StreamType requestedType, StreamType expectedType)
    {
        // Arrange
        var streamId = _streamManager.CreateStream(originalType);

        // Act
        var negotiatedType = _streamManager.NegotiateStreamType(streamId, requestedType);

        // Assert
        Assert.Equal(expectedType, negotiatedType);
    }

    [Fact]
    public void GetActiveStreams_ShouldReturnStreamInfo()
    {
        // Arrange
        var streamId1 = _streamManager.CreateStream(StreamType.Text);
        var streamId2 = _streamManager.CreateStream(StreamType.Binary);

        // Act
        var activeStreams = _streamManager.GetActiveStreams();

        // Assert
        Assert.Equal(2, activeStreams.Count);
        Assert.Contains(streamId1, activeStreams.Keys);
        Assert.Contains(streamId2, activeStreams.Keys);
        Assert.Equal(StreamType.Text, activeStreams[streamId1].Type);
        Assert.Equal(StreamType.Binary, activeStreams[streamId2].Type);
    }

    [Fact]
    public async Task CloseStreamAsync_ShouldRemoveStreamFromActiveStreams()
    {
        // Arrange
        var streamId = _streamManager.CreateStream(StreamType.Text);
        Assert.Single(_streamManager.GetActiveStreams());

        // Act
        await _streamManager.CloseStreamAsync(streamId);

        // Assert
        Assert.Empty(_streamManager.GetActiveStreams());
    }

    [Fact]
    public void GetInputStream_WithInvalidStreamId_ShouldThrowException()
    {
        // Arrange
        var invalidStreamId = "invalid-stream-id";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _streamManager.GetInputStream(invalidStreamId));
    }

    [Fact]
    public void GetOutputStream_WithInvalidStreamId_ShouldThrowException()
    {
        // Arrange
        var invalidStreamId = "invalid-stream-id";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _streamManager.GetOutputStream(invalidStreamId));
    }

    [Fact]
    public async Task StreamData_ThroughInputOutputStreams_ShouldWork()
    {
        // Arrange
        var streamId = _streamManager.CreateStream(StreamType.Text);
        var inputStream = _streamManager.GetInputStream(streamId);
        var outputStream = _streamManager.GetOutputStream(streamId);
        var testData = Encoding.UTF8.GetBytes("Stream test data");

        // Act
        await outputStream.WriteAsync(testData);
        await outputStream.FlushAsync();
        await _streamManager.CloseOutputAsync(streamId);

        var buffer = new byte[testData.Length];
        var bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        Assert.Equal(testData.Length, bytesRead);
        Assert.Equal(testData, buffer);
    }
}
