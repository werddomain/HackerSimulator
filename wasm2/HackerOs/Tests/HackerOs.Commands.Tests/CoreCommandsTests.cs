using System.Collections.Immutable;
using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.Commands.Ausearch;
using HackerOs.Commands.Cat;
using HackerOs.Commands.Cd;
using HackerOs.Commands.Echo;
using HackerOs.Commands.Exit;
using HackerOs.Commands.Ls;
using HackerOs.Commands.Pwd;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Tests.Support;
using Xunit;

namespace HackerOs.Commands.Tests;

public sealed class CoreCommandsTests
{
    [Fact]
    public async Task PwdCommand_writes_working_directory_and_returns_zero()
    {
        PwdCommand command = new(CreateManifest("pwd", "org.hackeros.cmd.pwd"));
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new(string.Empty);

        TerminalExecutionContext context = CreateContext(
            arguments: [],
            stdout: stdout,
            stderr: stderr,
            stdin: stdin,
            cwd: "/home/user");

        int exitCode = await command.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("/home/user" + Environment.NewLine, stdout.ToString());
    }

    [Fact]
    public async Task EchoCommand_writes_joined_arguments_and_returns_zero()
    {
        EchoCommand command = new(CreateManifest("echo", "org.hackeros.cmd.echo"));
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new(string.Empty);

        TerminalExecutionContext context = CreateContext(
            arguments: ["hello", "world", "foo"],
            stdout: stdout,
            stderr: stderr,
            stdin: stdin,
            cwd: "/");

        int exitCode = await command.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("hello world foo" + Environment.NewLine, stdout.ToString());
    }

    [Fact]
    public async Task CdCommand_changes_the_calling_shell_directory()
    {
        CdCommand command = new(CreateManifest("cd", "org.hackeros.cmd.cd"));
        FakeShellSession shell = new(workingDirectory: "/");
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new(string.Empty);

        int moved = await command.ExecuteAsync(CreateContext(["/home/user"], stdout, stderr, stdin, "/", shell), CancellationToken.None);
        int back = await command.ExecuteAsync(CreateContext(["-"], stdout, stderr, stdin, "/home/user", shell), CancellationToken.None);
        int home = await command.ExecuteAsync(CreateContext([], stdout, stderr, stdin, "/", shell), CancellationToken.None);

        Assert.Equal([0, 0, 0], [moved, back, home]);
        // Only `cd -` prints its target, as in bash.
        Assert.Equal("/" + Environment.NewLine, stdout.ToString());
        Assert.Equal("/home/user", shell.WorkingDirectory);
    }

    [Theory]
    [InlineData(new string[0], 6, 6, "")]
    [InlineData(new[] { "3" }, 3, 3, "")]
    [InlineData(new[] { "256" }, 0, 0, "")]
    [InlineData(new[] { "abc" }, 2, 2, "exit: abc: numeric argument required")]
    public async Task ExitCommand_asks_the_shell_to_exit(string[] arguments, int expectedStatus, int expectedRequest, string error)
    {
        ExitCommand command = new(CreateManifest("exit", "org.hackeros.commands.exit"));
        // A bare `exit` keeps $?: here 6.
        FakeShellSession shell = new() { LastExitStatus = arguments.Length == 0 ? 6 : 0 };
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new(string.Empty);

        int exitCode = await command.ExecuteAsync(CreateContext(arguments, stdout, stderr, stdin, "/", shell), CancellationToken.None);

        Assert.Equal(expectedStatus, exitCode);
        Assert.Equal(expectedRequest, shell.ExitRequested);
        Assert.Equal(error, stderr.ToString().TrimEnd());
    }

    [Fact]
    public async Task ExitCommand_with_too_many_arguments_keeps_the_shell()
    {
        ExitCommand command = new(CreateManifest("exit", "org.hackeros.commands.exit"));
        FakeShellSession shell = new();
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new(string.Empty);

        int exitCode = await command.ExecuteAsync(CreateContext(["1", "2"], stdout, stderr, stdin, "/", shell), CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Null(shell.ExitRequested);
        Assert.Equal("exit: too many arguments", stderr.ToString().TrimEnd());
    }

    [Fact]
    public async Task CdCommand_without_a_shell_fails()
    {
        CdCommand command = new(CreateManifest("cd", "org.hackeros.cmd.cd"));
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new(string.Empty);

        int exitCode = await command.ExecuteAsync(CreateContext(["/tmp"], stdout, stderr, stdin, "/"), CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Equal("cd: no shell session" + Environment.NewLine, stderr.ToString());
    }

    [Fact]
    public async Task CatCommand_outputs_file_content_or_error_for_missing_file()
    {
        CatCommand command = new(CreateManifest("cat", "org.hackeros.cmd.cat"));
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new(string.Empty);

        TerminalExecutionContext validContext = CreateContext(
            arguments: ["/home/user/notes.txt"],
            stdout: stdout,
            stderr: stderr,
            stdin: stdin,
            cwd: "/home/user");

        int validCode = await command.ExecuteAsync(validContext, CancellationToken.None);
        Assert.Equal(0, validCode);
        Assert.Equal("Hello HackerOS" + Environment.NewLine, stdout.ToString());

        using StringWriter stdoutMissing = new();
        using StringWriter stderrMissing = new();
        TerminalExecutionContext missingContext = CreateContext(
            arguments: ["missing.txt"],
            stdout: stdoutMissing,
            stderr: stderrMissing,
            stdin: stdin,
            cwd: "/home/user");

        int missingCode = await command.ExecuteAsync(missingContext, CancellationToken.None);
        Assert.Equal(1, missingCode);
        Assert.Contains("No such file or directory", stderrMissing.ToString());
    }

    [Fact]
    public async Task LsCommand_lists_directory_entries_with_sorting_and_flags()
    {
        LsCommand command = new(CreateManifest("ls", "org.hackeros.cmd.ls"));
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new(string.Empty);

        TerminalExecutionContext context = CreateContext(
            arguments: ["-a"],
            stdout: stdout,
            stderr: stderr,
            stdin: stdin,
            cwd: "/home/user");

        int exitCode = await command.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains(".config", stdout.ToString());
        Assert.Contains("notes.txt", stdout.ToString());
    }

    [Fact]
    public async Task AusearchCommand_filters_audit_records_by_path_and_origin_user()
    {
        // ADR 0100 section 3: ausearch reads auditd-style records from /var/log/audit.log.
        DateTimeOffset at = new(2026, 10, 17, 8, 30, 0, TimeSpan.Zero);
        string hosts = AuditLineFormat.Format(new AuditRecord(
            at, 1, "write", "/etc/hosts", null, "root", "alice", 42, "nano", "org.hackeros.commands.nano", true, null));
        string refused = AuditLineFormat.Format(new AuditRecord(
            at, 2, "create", "/etc/hackeros/apps.config", null, "bob", "bob", 43, "settings", "org.hackeros.settings", false, "PermissionDenied"));
        AusearchCommand command = new(CreateManifest("ausearch", "org.hackeros.commands.ausearch"));
        void Seed(TestAppFileSystemGateway fs)
        {
            fs.AddDirectory("/var");
            fs.AddDirectory("/var/log");
            fs.AddFile("/var/log/audit.log", Encoding.UTF8.GetBytes(hosts + "\n" + refused + "\n"));
        }

        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new(string.Empty);
        int found = await command.ExecuteAsync(
            CreateContext(["-f", "/etc/hosts", "-ul", "alice"], stdout, stderr, stdin, "/", seed: Seed), CancellationToken.None);

        Assert.Equal(0, found);
        Assert.Contains(hosts, stdout.ToString());
        Assert.DoesNotContain("apps.config", stdout.ToString());
        Assert.Contains("time->Sat Oct 17 08:30:00 2026", stdout.ToString());

        using StringWriter failedOut = new();
        using StringWriter failedErr = new();
        int failures = await command.ExecuteAsync(
            CreateContext(["--success", "no"], failedOut, failedErr, stdin, "/", seed: Seed), CancellationToken.None);
        Assert.Equal(0, failures);
        Assert.Contains(refused, failedOut.ToString());

        using StringWriter noneOut = new();
        using StringWriter noneErr = new();
        int none = await command.ExecuteAsync(
            CreateContext(["-ua", "carol"], noneOut, noneErr, stdin, "/", seed: Seed), CancellationToken.None);
        Assert.Equal(1, none);
        Assert.Equal("<no matches>" + Environment.NewLine, noneErr.ToString());
    }

    private static TerminalExecutionContext CreateContext(
        IReadOnlyList<string> arguments,
        TextWriter stdout,
        TextWriter stderr,
        TextReader stdin,
        string cwd,
        IShellSession? shell = null,
        Action<TestAppFileSystemGateway>? seed = null)
    {
        AppManifest manifest = CreateManifest("cmd", "org.hackeros.cmd.test");
        LocalUserId userId = LocalUserId.FromGuid(Guid.NewGuid());

        TestAppFileSystemGateway fileSystem = new(userId);
        fileSystem.AddDirectory("/home/user");
        fileSystem.AddFile("/home/user/notes.txt", Encoding.UTF8.GetBytes("Hello HackerOS"));
        fileSystem.AddDirectory("/home/user/.config");
        seed?.Invoke(fileSystem);

        TestAppExecutionContext app = new(manifest, fileSystem);

        Dictionary<string, string> env = new(StringComparer.Ordinal)
        {
            ["USER"] = "user",
            ["HOME"] = "/home/user"
        };

        return new TerminalExecutionContext(app, arguments, stdin, stdout, stderr, cwd, env, shell: shell);
    }

    private static AppManifest CreateManifest(string name, string id) =>
        new()
        {
            Id = id,
            Name = name,
            Version = "1.0.0",
            PublisherId = "pub.hackeros",
            Description = "Command line tool",
            Kind = AppKind.Terminal,
            EntryPoint = new AppEntryPointManifest("Assembly.dll", "Type"),
            SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
            Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Hidden, []),
            Resources = AppResourceProfileManifest.None,
            Capabilities = [AppCapabilities.FileSystemUserHomeRead, AppCapabilities.FileSystemUserHomeWrite],
            Terminal = new TerminalCommandManifest(name, [], name)
        };

    private sealed class TestAppExecutionContext : IAppExecutionContext
    {
        public TestAppExecutionContext(AppManifest manifest, IAppFileSystemGateway fileSystem)
        {
            Manifest = manifest;
            FileSystem = fileSystem;
            InstanceId = Guid.NewGuid();
            UserId = LocalUserId.FromGuid(Guid.NewGuid()).ToString();
            SessionId = SessionId.FromGuid(Guid.NewGuid());
            ProcessId = ProcessId.FromInt64(1);
        }

        public AppManifest Manifest { get; }
        public Guid InstanceId { get; }
        public string UserId { get; }
        public AppAuthority UserAuthority => AppAuthority.User;
        public IReadOnlySet<string> GrantedCapabilities => new HashSet<string>(Manifest.Capabilities);
        public SessionId SessionId { get; }
        public ProcessId ProcessId { get; }
        public CancellationToken CancellationToken => CancellationToken.None;
        public ICapabilityChecker Capabilities => throw new NotImplementedException();
        public IAppFileSystemGateway FileSystem { get; }
        public IAppEventGateway Events => throw new NotImplementedException();
        public IAppNotificationGateway Notifications => throw new NotImplementedException();
        public IAppLoggingGateway Logging => throw new NotImplementedException();
        public IAppClockGateway Clock => throw new NotImplementedException();
        public IAppProcessGateway Processes => throw new NotImplementedException();
    }

    private sealed class TestAppFileSystemGateway : IAppFileSystemGateway
    {
        private readonly LocalUserId _userId;
        private readonly Dictionary<string, (FileSystemEntryMetadata Metadata, byte[]? Content)> _entries = new(StringComparer.Ordinal);

        public TestAppFileSystemGateway(LocalUserId userId)
        {
            _userId = userId;
            AddDirectory("/");
        }

        public void AddDirectory(string path)
        {
            FileSystemEntryId id = FileSystemEntryId.FromGuid(Guid.NewGuid());
            DateTimeOffset now = DateTimeOffset.UtcNow;
            FileSystemTimestamps ts = new(now, now, now);
            DirectoryMetadata meta = new(
                id,
                _userId.ToString(),
                "users",
                FileSystemPermissions.FromMode(0b111_101_101),
                ts,
                1);

            _entries[path] = (meta, null);
        }

        public void AddFile(string path, byte[] content)
        {
            FileSystemEntryId id = FileSystemEntryId.FromGuid(Guid.NewGuid());
            DateTimeOffset now = DateTimeOffset.UtcNow;
            FileSystemTimestamps ts = new(now, now, now);
            FileMetadata meta = new(
                id,
                _userId.ToString(),
                "users",
                FileSystemPermissions.FromMode(0b110_100_100),
                ts,
                1,
                content.Length,
                "text/plain");

            _entries[path] = (meta, content);
        }

        public ValueTask<FileSystemResult<FileSystemEntrySnapshot>> StatAsync(FileSystemStatRequest request, CancellationToken cancellationToken = default)
        {
            if (_entries.TryGetValue(request.Path.Value, out var entry))
            {
                return ValueTask.FromResult(FileSystemResult<FileSystemEntrySnapshot>.Success(
                    new FileSystemEntrySnapshot(request.Path, entry.Metadata)));
            }

            return ValueTask.FromResult(FileSystemResult<FileSystemEntrySnapshot>.Failure(
                new FileSystemError(FileSystemOperation.Stat, FileSystemErrorCode.NotFound, request.Path)));
        }

        public ValueTask<FileSystemResult<FileSystemDirectorySnapshot>> EnumerateAsync(FileSystemEnumerateRequest request, CancellationToken cancellationToken = default)
        {
            if (!_entries.TryGetValue(request.Path.Value, out var dirEntry) || dirEntry.Metadata.Kind != FileSystemEntryKind.Directory)
            {
                return ValueTask.FromResult(FileSystemResult<FileSystemDirectorySnapshot>.Failure(
                    new FileSystemError(FileSystemOperation.Enumerate, FileSystemErrorCode.NotFound, request.Path)));
            }

            string prefix = request.Path.Value.TrimEnd('/') + "/";
            List<FileSystemDirectoryItem> items = [];

            foreach (var kvp in _entries)
            {
                if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string sub = kvp.Key[prefix.Length..];
                    if (!sub.Contains('/'))
                    {
                        items.Add(new FileSystemDirectoryItem(FileSystemEntryName.Parse(sub), kvp.Value.Metadata));
                    }
                }
            }

            return ValueTask.FromResult(FileSystemResult<FileSystemDirectorySnapshot>.Success(
                new FileSystemDirectorySnapshot(request.Path, 1, items.OrderBy(i => i.Name.Value, StringComparer.Ordinal))));
        }

        public ValueTask<FileSystemResult<FileSystemContentReadHandle>> ReadAsync(FileSystemReadRequest request, CancellationToken cancellationToken = default)
        {
            if (_entries.TryGetValue(request.Path.Value, out var fileEntry) && fileEntry.Metadata.Kind == FileSystemEntryKind.File && fileEntry.Content is not null)
            {
                FileSystemEntrySnapshot snapshot = new(request.Path, fileEntry.Metadata);
                FileSystemContentDescriptor descriptor = FileSystemContentDescriptor.Text();
                MemoryStream stream = new(fileEntry.Content, writable: false);
                FileSystemContentReadHandle handle = new(snapshot, descriptor, stream);

                return ValueTask.FromResult(FileSystemResult<FileSystemContentReadHandle>.Success(handle));
            }

            return ValueTask.FromResult(FileSystemResult<FileSystemContentReadHandle>.Failure(
                new FileSystemError(FileSystemOperation.Read, FileSystemErrorCode.NotFound, request.Path)));
        }

        public ValueTask<FileSystemMutationResult> CreateAsync(FileSystemCreateRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public ValueTask<FileSystemMutationResult> WriteAsync(FileSystemWriteRequest request, IFileSystemContentSource content, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public ValueTask<FileSystemMutationResult> MoveAsync(FileSystemMoveRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public ValueTask<FileSystemMutationResult> CopyAsync(FileSystemCopyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public ValueTask<FileSystemMutationResult> DeleteAsync(FileSystemDeleteRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public ValueTask<FileSystemMutationResult> SetPermissionsAsync(FileSystemSetPermissionsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAppFileSystemGateway WithSelectedHandle(FileSystemSelectedResourceHandle handle) => this;
    }
}
