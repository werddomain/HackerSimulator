using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Commands.Rm;
using HackerOs.Tests.Support;
using Xunit;

namespace HackerOs.Commands.Wave5.Tests;

public sealed class RmCommandTests
{
    [Fact]
    public async Task ExecuteAsync_RemovesFile_WhenItExists()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithFile("/home/user/doomed.txt");
        TerminalExecutionContext context = CreateContext(fs, "doomed.txt");

        int exitCode = await new RmCommand(AppDefinitionMapper.ToManifest(new RmApp()))
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.False(fs.Exists("/home/user/doomed.txt"));
    }

    [Fact]
    public async Task ExecuteAsync_ReportsError_WhenTargetMissing()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithDirectory("/home/user");
        TerminalExecutionContext context = CreateContext(fs, "missing.txt");

        int exitCode = await new RmCommand(AppDefinitionMapper.ToManifest(new RmApp()))
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsConflict_WhenEntryRevisionChangesConcurrently()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithFile("/home/user/doomed.txt");
        RaceOnNextMutationFileSystemGateway racing = new(fs, "/home/user/doomed.txt");
        TerminalExecutionContext context = new(
            new MinimalAppExecutionContext(AppDefinitionMapper.ToManifest(new RmApp()), fileSystem: racing),
            ["doomed.txt"],
            new StringReader(string.Empty),
            new StringWriter(),
            new StringWriter(),
            "/home/user",
            new Dictionary<string, string>());

        int exitCode = await new RmCommand(AppDefinitionMapper.ToManifest(new RmApp()))
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.True(fs.Exists("/home/user/doomed.txt"));
        Assert.Contains("RevisionConflict", context.StandardError.ToString());
    }

    private static TerminalExecutionContext CreateContext(
        FakeAppFileSystemGateway fs, params string[] arguments) => new(
        new MinimalAppExecutionContext(AppDefinitionMapper.ToManifest(new RmApp()), fileSystem: fs),
        arguments,
        new StringReader(string.Empty),
        new StringWriter(),
        new StringWriter(),
        "/home/user",
        new Dictionary<string, string>());
}
