using HackerOs.AppSdk;
using HackerOs.Commands.Cp;
using HackerOs.Tests.Support;
using Xunit;

namespace HackerOs.Commands.Wave5.Tests;

public sealed class CpCommandTests
{
    [Fact]
    public async Task ExecuteAsync_CopiesFile_WhenSourceAndDestinationParentExist()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithFile("/home/user/source.txt", "hello");
        TerminalExecutionContext context = CreateContext(fs, "source.txt", "copy.txt");

        int exitCode = await new CpCommand(CpCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(fs.Exists("/home/user/source.txt"));
        Assert.Equal("hello", fs.ContentOf("/home/user/copy.txt"));
    }

    [Fact]
    public async Task ExecuteAsync_ReportsError_WhenSourceMissing()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithDirectory("/home/user");
        TerminalExecutionContext context = CreateContext(fs, "missing.txt", "copy.txt");

        int exitCode = await new CpCommand(CpCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.False(fs.Exists("/home/user/copy.txt"));
    }

    [Fact]
    public async Task ExecuteAsync_ReportsConflict_WhenSourceRevisionChangesConcurrently()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithFile("/home/user/source.txt", "hello");
        RaceOnNextMutationFileSystemGateway racing = new(fs, "/home/user/source.txt");
        TerminalExecutionContext context = new(
            new MinimalAppExecutionContext(CpCommand.StaticManifest, fileSystem: racing),
            ["source.txt", "copy.txt"],
            new StringReader(string.Empty),
            new StringWriter(),
            new StringWriter(),
            "/home/user",
            new Dictionary<string, string>());

        int exitCode = await new CpCommand(CpCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.False(fs.Exists("/home/user/copy.txt"));
        Assert.Contains("RevisionConflict", context.StandardError.ToString());
    }

    private static TerminalExecutionContext CreateContext(
        FakeAppFileSystemGateway fs, params string[] arguments) => new(
        new MinimalAppExecutionContext(CpCommand.StaticManifest, fileSystem: fs),
        arguments,
        new StringReader(string.Empty),
        new StringWriter(),
        new StringWriter(),
        "/home/user",
        new Dictionary<string, string>());
}
