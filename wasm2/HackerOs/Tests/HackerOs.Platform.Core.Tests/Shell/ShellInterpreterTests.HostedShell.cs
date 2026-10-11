using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.AppSdk.IO;
using HackerOs.AppSdk.Shell;
using HackerOs.Platform.Core.Elevation;
using HackerOs.Platform.Core.Shell;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;

namespace HackerOs.Platform.Core.Tests.Shell;

/// <summary>
/// Shells hosted through the SDK (ADR 0056, section 7): <c>HackShell</c>, capability
/// <c>shell.host</c>, one <c>hsh</c> process per shell, credentials read by the terminal and
/// verified by the OS.
/// </summary>
public sealed partial class ShellInterpreterTests
{
    [Fact]
    public async Task Starting_a_shell_requires_shell_host()
    {
        Fixture fixture = await Fixture.CreateAsync();

        HackUnauthorizedAccessException denied = await Assert.ThrowsAsync<HackUnauthorizedAccessException>(
            () => HackShell.StartAsync(fixture.Host));

        Assert.Equal(AppCapabilities.ShellHost, denied.RequiredCapability);
        Assert.DoesNotContain(fixture.Manager.GetActiveProcesses(), p => p.AppId == ShellHostService.ShellAppId);
    }

    [Fact]
    public async Task A_hosted_shell_is_a_child_process_of_the_terminal_and_the_parent_of_its_commands()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Grant(AppCapabilities.ShellHost);

        await using IHackShell shell = await HackShell.StartAsync(fixture.Host);
        (int status, string stdout, _) = await RunHostedAsync(shell, "echo hi | upper");

        Assert.Equal(0, status);
        Assert.Equal("HI\n", stdout);
        Assert.True(fixture.Manager.TryGetActive(ProcessId.FromInt64(shell.ProcessId), out ProcessRecord process));
        Assert.Equal(ShellHostService.ShellAppId, process.AppId);
        Assert.Equal(fixture.Host.ProcessId, process.ParentPid);
        ProcessRecord echo = fixture.Manager.GetHistory().Last(p => p.AppId == "org.test.echo");
        Assert.Equal(process.Pid, echo.ParentPid);
        Assert.Equal("alice@hackeros:~$", shell.Prompt);
    }

    [Fact]
    public async Task Each_hosted_shell_has_its_own_directory_and_exit_status()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Grant(AppCapabilities.ShellHost);
        await fixture.MakeDirectoryAsync("/home/alice/docs");

        await using IHackShell first = await HackShell.StartAsync(fixture.Host);
        await using IHackShell second = await HackShell.StartAsync(fixture.Host, new HackShellOptions { WorkingDirectory = "docs" });
        await using IHackShell fallback = await HackShell.StartAsync(fixture.Host, new HackShellOptions { WorkingDirectory = "/missing" });
        await RunHostedAsync(first, "cd /home");
        await RunHostedAsync(second, "exitwith 4");
        (_, string where, _) = await RunHostedAsync(second, "where");

        Assert.Equal("/home", first.WorkingDirectory);
        Assert.Equal("/home/alice/docs", second.WorkingDirectory);
        Assert.Equal("/home/alice/docs /home/alice/docs /home/alice\n", where);
        Assert.Equal("/home/alice", fallback.WorkingDirectory);
        Assert.Equal("/home/alice", fixture.Host.Environment.CurrentDirectory);
        Assert.Equal(0, first.ExitStatus);
        Assert.Equal("alice@hackeros:~/docs$", second.Prompt);
    }

    [Fact]
    public async Task Disposing_a_hosted_shell_ends_its_process_and_its_running_commands()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Grant(AppCapabilities.ShellHost);
        IHackShell shell = await HackShell.StartAsync(fixture.Host);
        await RunHostedAsync(shell, "exitwith 3");

        Task<(int, string, string)> running = RunHostedAsync(shell, "hang");
        await WaitUntilAsync(() => fixture.Manager.GetActiveProcesses().Any(p => p.AppId == "org.test.hang"));
        await shell.DisposeAsync();
        await running.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(shell.HasExited);
        ProcessRecord ended = fixture.Manager.GetHistory().Single(p => p.AppId == ShellHostService.ShellAppId);
        Assert.Equal(3, ended.ExitCode);
        ProcessRecord hang = fixture.Manager.GetHistory().Single(p => p.AppId == "org.test.hang");
        Assert.Equal(ProcessExitReason.DependencyStop, hang.ExitReason);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => RunHostedAsync(shell, "echo late"));
        Assert.True(fixture.Manager.TryGetActive(fixture.Host.ProcessId, out _));
    }

    [Fact]
    public async Task Sudo_in_a_hosted_shell_reads_credentials_from_the_terminal_and_the_os_verifies_them()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Grant(AppCapabilities.ShellHost);
        RecordingPrompt prompt = new() { Credential = new HackCredential("admin", "wrong") };
        await using IHackShell shell = await HackShell.StartAsync(fixture.Host, new HackShellOptions { CredentialPrompt = prompt });

        (int refused, _, string refusedError) = await RunHostedAsync(shell, "sudo amroot");
        prompt.Credential = new HackCredential("admin", FakeElevationService.Password);
        (int accepted, string acceptedOutput, _) = await RunHostedAsync(shell, "sudo amroot");

        Assert.Equal(1, refused);
        Assert.Equal("sudo: authentication failed\n", refusedError);
        Assert.Equal(["Sorry, try again."], prompt.Lines);
        Assert.Equal(0, accepted);
        Assert.Equal("root\n", acceptedOutput);
        Assert.Equal(["Run as root: amroot", "Run as root: amroot"], prompt.Requests.Select(r => r.Description));
    }

    [Fact]
    public async Task Without_a_credential_prompt_sudo_needs_a_terminal()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Grant(AppCapabilities.ShellHost);
        await using IHackShell shell = await HackShell.StartAsync(fixture.Host);

        (int status, _, string stderr) = await RunHostedAsync(shell, "sudo amroot");

        Assert.Equal(1, status);
        Assert.Equal("sudo: a terminal is required to read the password\n", stderr);
    }

    [Fact]
    public async Task A_hosted_shell_reads_standard_input_and_keeps_its_history()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Grant(AppCapabilities.ShellHost);
        await using IHackShell shell = await HackShell.StartAsync(fixture.Host);
        StringWriter stdout = new() { NewLine = "\n" };

        int status = await shell.ExecuteAsync("upper", new StringReader("from the keyboard\n"), stdout, TextWriter.Null);
        shell.AddHistory("upper");
        shell.AddHistory("ls");

        Assert.Equal(0, status);
        Assert.Equal("FROM THE KEYBOARD\n", stdout.ToString());
        Assert.Equal(["upper", "ls"], shell.History);
        Assert.Equal("ls", shell.NavigateHistory(-1));
        Assert.Equal("upper", shell.NavigateHistory(-1));
    }

    [Theory]
    [InlineData("ec", 0, 2, new[] { "echo" }, "echo ")]
    [InlineData("ex", 0, 2, new[] { "exitwith", "export" }, "ex")]
    [InlineData("e", 0, 1, new[] { "echo", "env", "exitwith", "export" }, "e")]
    [InlineData("echo a | up", 9, 2, new[] { "upper" }, "echo a | upper ")]
    [InlineData("exitwith 0 &&ec", 13, 2, new[] { "echo" }, "exitwith 0 &&echo ")]
    [InlineData("echo ec", 5, 0, new string[0], "echo ec")]
    [InlineData("EC", 0, 2, new string[0], "EC")]
    public async Task Tab_completes_command_names_in_command_position(
        string line, int wordStart, int wordLength, string[] candidates, string completed)
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Grant(AppCapabilities.ShellHost);
        await using IHackShell shell = await HackShell.StartAsync(fixture.Host);

        HackShellCompletion completion = await shell.CompleteAsync(line, line.Length);

        Assert.Equal(candidates, completion.Candidates);
        if (candidates.Length > 0)
        {
            Assert.Equal(wordStart, completion.WordStart);
            Assert.Equal(wordLength, completion.WordLength);
        }

        Assert.Equal(completed, completion.ApplyTo(line, out int cursor));
        Assert.Equal(completed.Length, cursor);
    }

    [Fact]
    public async Task Static_aliases_are_completed_too()
    {
        Fixture fixture = await Fixture.CreateAsync();
        fixture.Grant(AppCapabilities.ShellHost);
        await using IHackShell shell = await HackShell.StartAsync(fixture.Host);

        HackShellCompletion completion = await shell.CompleteAsync("ma", 2);

        Assert.Equal(["man"], completion.Candidates);
    }

    private static async Task<(int Status, string Stdout, string Stderr)> RunHostedAsync(IHackShell shell, string line)
    {
        StringWriter stdout = new() { NewLine = "\n" };
        StringWriter stderr = new() { NewLine = "\n" };
        int status = await shell.ExecuteAsync(line, null, stdout, stderr);
        return (status, stdout.ToString(), stderr.ToString());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    /// <summary>A terminal's credential prompt that answers with a preset credential and records what it was asked.</summary>
    private sealed class RecordingPrompt : HackCredentialPrompt
    {
        public HackCredential? Credential { get; set; }

        public List<HackCredentialRequest> Requests { get; } = [];

        public List<string> Lines { get; } = [];

        public override Task<HackCredential?> ReadCredentialAsync(HackCredentialRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Credential);
        }

        public override Task WriteLineAsync(string text, HackPromptLineKind kind, CancellationToken cancellationToken = default)
        {
            Lines.Add(text);
            return Task.CompletedTask;
        }
    }

    /// <summary>The OS side of the check: accepts <c>admin</c> with <see cref="Password"/> and mints a real ticket.</summary>
    private sealed class FakeElevationService(InMemoryElevationTicketStore tickets) : IElevationService
    {
        public const string Password = "s3cret";

        public Task<ElevationRequestResult> RequestSudoAsync(string adminLoginName, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(Issue(adminLoginName, password, ElevationScope.SudoRoot));

        public Task<ElevationRequestResult> RequestCapabilityApprovalAsync(
            string adminLoginName, string password, string capability, VirtualPath? path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Issue(adminLoginName, password, ElevationScope.CapabilityApproval));

        private ElevationRequestResult Issue(string login, string password, ElevationScope scope)
        {
            if (login != "admin" || password != Password)
            {
                return new ElevationRequestResult(false, null, null, "Authentication failed.");
            }

            ElevationTicket ticket = tickets.Issue(scope, "admin-id", login);
            return new ElevationRequestResult(true, ticket.Token, login, null);
        }
    }
}
