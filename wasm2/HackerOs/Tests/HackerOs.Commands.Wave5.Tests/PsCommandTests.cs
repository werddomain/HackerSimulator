using HackerOs.AppSdk;
using HackerOs.Commands.Ps;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Tests.Support;
using Xunit;

namespace HackerOs.Commands.Wave5.Tests;

public sealed class PsCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly SessionId CurrentSession = SessionId.FromGuid(Guid.NewGuid());
    private static readonly SessionId OtherSession = SessionId.FromGuid(Guid.NewGuid());

    [Fact]
    public async Task ExecuteAsync_ListsRealProcesses_WithoutHardcodedRows()
    {
        FakeAppProcessGateway processes = new FakeAppProcessGateway()
            .WithProcess(ProcessRecordFactory.Running(
                7, "org.hackeros.text-editor", CurrentSession, Now.AddMinutes(-5), Now.AddMinutes(-5)));
        TerminalExecutionContext context = CreateContext(processes);

        int exitCode = await new PsCommand(PsCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        string output = context.StandardOutput.ToString()!;
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("init", output);
        Assert.DoesNotContain("hackeros-shell", output);
        Assert.Contains("org.hackeros.text-editor", output);
        Assert.Contains("00:05:00", output);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultsToCurrentSession_UnlessAllUsersRequested()
    {
        FakeAppProcessGateway processes = new FakeAppProcessGateway()
            .WithProcess(ProcessRecordFactory.Running(1, "own-session-app", CurrentSession, Now, Now))
            .WithProcess(ProcessRecordFactory.Running(2, "other-session-app", OtherSession, Now, Now));

        TerminalExecutionContext defaultContext = CreateContext(processes);
        int defaultExitCode = await new PsCommand(PsCommand.StaticManifest)
            .ExecuteAsync(defaultContext, CancellationToken.None);
        string defaultOutput = defaultContext.StandardOutput.ToString()!;
        Assert.Equal(0, defaultExitCode);
        Assert.Contains("own-session-app", defaultOutput);
        Assert.DoesNotContain("other-session-app", defaultOutput);

        TerminalExecutionContext allContext = CreateContext(processes, "-a");
        await new PsCommand(PsCommand.StaticManifest).ExecuteAsync(allContext, CancellationToken.None);
        string allOutput = allContext.StandardOutput.ToString()!;
        Assert.Contains("own-session-app", allOutput);
        Assert.Contains("other-session-app", allOutput);
    }

    private static TerminalExecutionContext CreateContext(
        FakeAppProcessGateway processes, params string[] arguments) => new(
        new MinimalAppExecutionContext(
            PsCommand.StaticManifest,
            processes: processes,
            clock: new FakeAppClockGateway(Now),
            sessionId: CurrentSession),
        arguments,
        new StringReader(string.Empty),
        new StringWriter(),
        new StringWriter(),
        "/home/user",
        new Dictionary<string, string>());
}
