using HackerOs.AppSdk;
using HackerOs.Commands.Mkdir;
using HackerOs.Tests.Support;
using Xunit;

namespace HackerOs.Commands.Wave5.Tests;

public sealed class MkdirCommandTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesDirectory_WhenParentExists()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithDirectory("/home/user");
        TerminalExecutionContext context = CreateContext(fs, "newdir");

        int exitCode = await new MkdirCommand(MkdirCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(fs.Exists("/home/user/newdir"));
    }

    [Fact]
    public async Task ExecuteAsync_ReportsError_WhenParentMissing()
    {
        FakeAppFileSystemGateway fs = new();
        TerminalExecutionContext context = CreateContext(fs, "newdir");

        int exitCode = await new MkdirCommand(MkdirCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.False(fs.Exists("/home/user/newdir"));
    }

    [Fact]
    public async Task ExecuteAsync_ReportsConflict_WhenParentRevisionChangesConcurrently()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithDirectory("/home/user");
        RaceOnNextMutationFileSystemGateway racing = new(fs, "/home/user");
        TerminalExecutionContext context = new(
            new MinimalAppExecutionContext(MkdirCommand.StaticManifest, fileSystem: racing),
            ["newdir"],
            new StringReader(string.Empty),
            new StringWriter(),
            new StringWriter(),
            "/home/user",
            new Dictionary<string, string>());

        int exitCode = await new MkdirCommand(MkdirCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.False(fs.Exists("/home/user/newdir"));
        Assert.Contains("RevisionConflict", context.StandardError.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_MissingOperand_ReturnsError()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithDirectory("/home/user");
        TerminalExecutionContext context = CreateContext(fs);

        int exitCode = await new MkdirCommand(MkdirCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    private static TerminalExecutionContext CreateContext(
        FakeAppFileSystemGateway fs, params string[] arguments) => new(
        new MinimalAppExecutionContext(MkdirCommand.StaticManifest, fileSystem: fs),
        arguments,
        new StringReader(string.Empty),
        new StringWriter(),
        new StringWriter(),
        "/home/user",
        new Dictionary<string, string>());
}
