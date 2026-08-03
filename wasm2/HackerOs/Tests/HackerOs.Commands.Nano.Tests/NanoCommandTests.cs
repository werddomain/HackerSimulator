using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using Xunit;

namespace HackerOs.Commands.Nano.Tests;

public sealed class NanoCommandTests
{
    [Fact]
    public async Task ExecuteAsync_OutputsHeader_ReturnsZero()
    {
        NanoCommand command = new();
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new("");

        TerminalExecutionContext context = new(
            app: null!,
            arguments: ["sample.txt"],
            standardInput: stdin,
            standardOutput: stdout,
            standardError: stderr,
            workingDirectory: "/home/user",
            environment: new Dictionary<string, string>()
        );

        int exitCode = await command.ExecuteAsync(context);

        Assert.Equal(0, exitCode);
        Assert.Contains("[GNU nano]", stdout.ToString());
        Assert.Contains("sample.txt", stdout.ToString());
    }
}
