using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using System.Text;
using Xunit;

namespace HackerOs.Commands.Nano.Tests;

public sealed class NanoCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithoutFullScreen_ReturnsUnsupportedError()
    {
        NanoCommand command = new(NanoCommand.StaticManifest);
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new("");

        TerminalExecutionContext context = new(
            app: new StubAppExecutionContext(NanoCommand.StaticManifest),
            arguments: ["sample.txt"],
            standardInput: stdin,
            standardOutput: stdout,
            standardError: stderr,
            workingDirectory: "/home/user",
            environment: new Dictionary<string, string>()
        );

        int exitCode = await command.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Contains("does not provide full-screen editing", stderr.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_EditsSavesAndAlwaysLeavesAlternateScreen()
    {
        TestFileSystemGateway fileSystem = new("hello", "/home/user/sample.txt");
        TestFullScreenTerminal terminal = new(
            new TerminalKeyEvent(TerminalKey.End),
            new TerminalKeyEvent(TerminalKey.Character, "!"),
            new TerminalKeyEvent(TerminalKey.Character, "o", Control: true),
            new TerminalKeyEvent(TerminalKey.Character, "x", Control: true));
        TerminalExecutionContext context = CreateContext(fileSystem, terminal, "sample.txt");

        int exitCode = await new NanoCommand(NanoCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("hello!", fileSystem.WrittenContent);
        Assert.True(terminal.Entered);
        Assert.True(terminal.Left);
        Assert.NotEmpty(terminal.Frames);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationStillLeavesAlternateScreen()
    {
        TestFileSystemGateway fileSystem = new("hello", "/home/user/sample.txt");
        TestFullScreenTerminal terminal = new() { ThrowCancellationOnRead = true };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new NanoCommand(NanoCommand.StaticManifest).ExecuteAsync(
                CreateContext(fileSystem, terminal, "sample.txt"), CancellationToken.None));

        Assert.True(terminal.Entered);
        Assert.True(terminal.Left);
    }

    [Fact]
    public async Task ExecuteAsync_SaveAsCreatesAndWritesTheSelectedVirtualPath()
    {
        TestFileSystemGateway fileSystem = new("hello", "/home/user/sample.txt");
        TestFullScreenTerminal terminal = new(
            new TerminalKeyEvent(TerminalKey.End),
            new TerminalKeyEvent(TerminalKey.Character, "!"),
            new TerminalKeyEvent(TerminalKey.Character, "o", Control: true, Shift: true),
            new TerminalKeyEvent(TerminalKey.Character, "copy.txt"),
            new TerminalKeyEvent(TerminalKey.Enter),
            new TerminalKeyEvent(TerminalKey.Character, "x", Control: true));

        int exitCode = await new NanoCommand(NanoCommand.StaticManifest).ExecuteAsync(
            CreateContext(fileSystem, terminal, "sample.txt"), CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("/home/user/copy.txt", fileSystem.CreatedPath);
        Assert.Equal("/home/user/copy.txt", fileSystem.WrittenPath);
        Assert.Equal("hello!", fileSystem.WrittenContent);
    }

    [Fact]
    public async Task ExecuteAsync_ReadCapabilityDenied_FailsBeforeTakingAlternateScreen()
    {
        TestFileSystemGateway fileSystem = new(
            string.Empty, "/home/user/denied.txt", denyRead: true);
        TestFullScreenTerminal terminal = new();
        TerminalExecutionContext context = CreateContext(fileSystem, terminal, "denied.txt");

        int exitCode = await new NanoCommand(NanoCommand.StaticManifest)
            .ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("CapabilityDenied", context.StandardError.ToString());
        Assert.False(terminal.Entered);
    }

    private static TerminalExecutionContext CreateContext(
        IAppFileSystemGateway fileSystem,
        IFullScreenTerminalSession? terminal,
        params string[] arguments) => new(
            new StubAppExecutionContext(NanoCommand.StaticManifest, fileSystem),
            arguments,
            new StringReader(string.Empty),
            new StringWriter(),
            new StringWriter(),
            "/home/user",
            new Dictionary<string, string>(),
            terminal);

    private sealed class StubAppExecutionContext(
        AppManifest manifest,
        IAppFileSystemGateway? fileSystem = null) : IAppExecutionContext
    {
        public AppManifest Manifest { get; } = manifest;
        public Guid InstanceId { get; } = Guid.NewGuid();
        public string UserId => "user";
        public AppAuthority UserAuthority => AppAuthority.User;
        public IReadOnlySet<string> GrantedCapabilities { get; } =
            new HashSet<string>(manifest.Capabilities, StringComparer.Ordinal);
        public SessionId SessionId { get; } = SessionId.FromGuid(Guid.NewGuid());
        public ProcessId ProcessId { get; } = ProcessId.FromInt64(1);
        public CancellationToken CancellationToken => CancellationToken.None;
        public ICapabilityChecker Capabilities => throw new NotSupportedException();
        public IAppFileSystemGateway FileSystem { get; } = fileSystem ?? new TestFileSystemGateway(string.Empty, "/unused");
        public IAppSettingsGateway Settings => throw new NotSupportedException();
        public IAppEventGateway Events => throw new NotSupportedException();
        public IAppNotificationGateway Notifications => throw new NotSupportedException();
        public IAppLoggingGateway Logging => throw new NotSupportedException();
        public IAppDiagnosticsGateway Diagnostics => throw new NotSupportedException();
        public IAppClockGateway Clock => throw new NotSupportedException();
        public IAppProcessGateway Processes => throw new NotSupportedException();
    }

    private sealed class TestFullScreenTerminal(params TerminalKeyEvent[] keys) : IFullScreenTerminalSession
    {
        private readonly Queue<TerminalKeyEvent> _keys = new(keys);
        public TerminalViewport Viewport { get; } = TerminalViewport.Create(80, 24);
        public bool Entered { get; private set; }
        public bool Left { get; private set; }
        public bool ThrowCancellationOnRead { get; init; }
        public List<TerminalScreenFrame> Frames { get; } = [];
        public ValueTask EnterAlternateScreenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Entered = true;
            return ValueTask.CompletedTask;
        }
        public ValueTask RenderAsync(TerminalScreenFrame frame, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Frames.Add(frame);
            return ValueTask.CompletedTask;
        }
        public ValueTask<TerminalKeyEvent> ReadKeyAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowCancellationOnRead)
                throw new OperationCanceledException(cancellationToken);
            return ValueTask.FromResult(_keys.Dequeue());
        }
        public ValueTask LeaveAlternateScreenAsync(CancellationToken cancellationToken = default)
        {
            Left = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestFileSystemGateway(
        string initialContent,
        string expectedPath,
        bool denyRead = false) : IAppFileSystemGateway
    {
        private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
        public string? WrittenContent { get; private set; }
        public string? WrittenPath { get; private set; }
        public string? CreatedPath { get; private set; }

        public ValueTask<FileSystemResult<FileSystemContentReadHandle>> ReadAsync(
            FileSystemReadRequest request, CancellationToken cancellationToken = default)
        {
            Assert.Equal(expectedPath, request.Path.Value);
            if (denyRead)
            {
                return ValueTask.FromResult(FileSystemResult<FileSystemContentReadHandle>.Failure(
                    new FileSystemError(
                        FileSystemOperation.Read, FileSystemErrorCode.CapabilityDenied, request.Path)));
            }
            byte[] bytes = Encoding.UTF8.GetBytes(initialContent);
            return ValueTask.FromResult(FileSystemResult<FileSystemContentReadHandle>.Success(
                new FileSystemContentReadHandle(Snapshot(request.Path, 1, bytes.Length),
                    FileSystemContentDescriptor.Text(), new MemoryStream(bytes, writable: false))));
        }

        public async ValueTask<FileSystemMutationResult> WriteAsync(
            FileSystemWriteRequest request, IFileSystemContentSource content,
            CancellationToken cancellationToken = default)
        {
            await using Stream stream = await content.OpenReadAsync(cancellationToken);
            using StreamReader reader = new(stream, Encoding.UTF8);
            WrittenContent = await reader.ReadToEndAsync(cancellationToken);
            WrittenPath = request.Path.Value;
            return new FileSystemMutationResult(
                FileSystemTransactionResult.Committed(Guid.NewGuid(), [Snapshot(request.Path, 2, Encoding.UTF8.GetByteCount(WrittenContent)).Metadata.Id]),
                Snapshot(request.Path, 2, Encoding.UTF8.GetByteCount(WrittenContent)));
        }

        public ValueTask<FileSystemResult<FileSystemEntrySnapshot>> StatAsync(
            FileSystemStatRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(FileSystemResult<FileSystemEntrySnapshot>.Success(
                DirectorySnapshot(request.Path, 1)));
        public ValueTask<FileSystemMutationResult> CreateAsync(
            FileSystemCreateRequest request, CancellationToken cancellationToken = default)
        {
            CreatedPath = request.Path.Value;
            FileSystemEntrySnapshot entry = Snapshot(request.Path, 1, 0);
            return ValueTask.FromResult(new FileSystemMutationResult(
                FileSystemTransactionResult.Committed(Guid.NewGuid(), [entry.Metadata.Id]), entry));
        }
        public ValueTask<FileSystemResult<FileSystemDirectorySnapshot>> EnumerateAsync(FileSystemEnumerateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<FileSystemMutationResult> MoveAsync(FileSystemMoveRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<FileSystemMutationResult> CopyAsync(FileSystemCopyRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<FileSystemMutationResult> DeleteAsync(FileSystemDeleteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<FileSystemMutationResult> SetPermissionsAsync(FileSystemSetPermissionsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAppFileSystemGateway WithSelectedHandle(FileSystemSelectedResourceHandle handle) => this;

        private FileSystemEntrySnapshot Snapshot(VirtualPath path, long revision, long length) => new(
            path,
            new FileMetadata(FileSystemEntryId.FromGuid(Guid.NewGuid()), "user", "users",
                FileSystemPermissions.FromMode(0b110_100_100),
                new FileSystemTimestamps(_now, _now, _now), revision, length, "text/plain"));

        private FileSystemEntrySnapshot DirectorySnapshot(VirtualPath path, long revision) => new(
            path,
            new DirectoryMetadata(FileSystemEntryId.FromGuid(Guid.NewGuid()), "user", "users",
                FileSystemPermissions.FromMode(0b111_101_101),
                new FileSystemTimestamps(_now, _now, _now), revision));
    }
}
