using HackerOs.AppSdk;
using HackerOs.Commands.Chmod;
using HackerOs.Tests.Support;
using Xunit;

namespace HackerOs.Commands.Wave5.Tests;

public sealed class ChmodCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ChangesPermissions_WhenTargetExists()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithFile("/home/user/script.sh", "echo hi");
        TerminalExecutionContext context = CreateContext(fs, "755", "script.sh");

        int exitCode = await new ChmodCommand(ChmodCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsError_WhenTargetMissing()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithDirectory("/home/user");
        TerminalExecutionContext context = CreateContext(fs, "755", "missing.sh");

        int exitCode = await new ChmodCommand(ChmodCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsConflict_WhenTargetRevisionChangesConcurrently()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithFile("/home/user/script.sh", "echo hi");
        RaceOnNextMutationFileSystemGateway racing = new(fs, "/home/user/script.sh");
        TerminalExecutionContext context = new(
            new MinimalAppExecutionContext(ChmodCommand.StaticManifest, fileSystem: racing),
            ["755", "script.sh"],
            new StringReader(string.Empty),
            new StringWriter(),
            new StringWriter(),
            "/home/user",
            new Dictionary<string, string>());

        int exitCode = await new ChmodCommand(ChmodCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("RevisionConflict", context.StandardError.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_InvalidMode_ReturnsError()
    {
        FakeAppFileSystemGateway fs = new FakeAppFileSystemGateway().WithFile("/home/user/script.sh");
        TerminalExecutionContext context = CreateContext(fs, "notoctal", "script.sh");

        int exitCode = await new ChmodCommand(ChmodCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    private static TerminalExecutionContext CreateContext(
        FakeAppFileSystemGateway fs, params string[] arguments) => new(
        new MinimalAppExecutionContext(ChmodCommand.StaticManifest, fileSystem: fs),
        arguments,
        new StringReader(string.Empty),
        new StringWriter(),
        new StringWriter(),
        "/home/user",
        new Dictionary<string, string>());
}
