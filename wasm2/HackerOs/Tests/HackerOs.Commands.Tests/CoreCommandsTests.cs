using System.Collections.Immutable;
using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.Commands.Cat;
using HackerOs.Commands.Cd;
using HackerOs.Commands.Echo;
using HackerOs.Commands.Ls;
using HackerOs.Commands.Pwd;
using HackerOs.Platform.Core.ServerConnection;
using HackerOs.Server.Contracts.Proxy;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Network;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.ServerConnection;
using HackerOs.Simulation.Abstractions.Sessions;
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
    public async Task CdCommand_returns_zero_for_valid_directory_and_error_for_missing()
    {
        CdCommand command = new(CreateManifest("cd", "org.hackeros.cmd.cd"));
        using StringWriter stdout = new();
        using StringWriter stderr = new();
        using StringReader stdin = new(string.Empty);

        TerminalExecutionContext validContext = CreateContext(
            arguments: ["/home/user"],
            stdout: stdout,
            stderr: stderr,
            stdin: stdin,
            cwd: "/");

        int validCode = await command.ExecuteAsync(validContext, CancellationToken.None);
        Assert.Equal(0, validCode);
        Assert.Equal("/home/user" + Environment.NewLine, stdout.ToString());

        using StringWriter stdoutMissing = new();
        using StringWriter stderrMissing = new();
        TerminalExecutionContext missingContext = CreateContext(
            arguments: ["/nonexistent"],
            stdout: stdoutMissing,
            stderr: stderrMissing,
            stdin: stdin,
            cwd: "/");

        int missingCode = await command.ExecuteAsync(missingContext, CancellationToken.None);
        Assert.Equal(1, missingCode);
        Assert.Contains("No such file or directory", stderrMissing.ToString());
    }

    [Fact]
    public async Task CatCommand_outputs_file_content_or_error_for_missing_file()
    {
        CatCommand command = new(
            CreateManifest("cat", "org.hackeros.cmd.cat"),
            new NullSimulatedNetworkService(),
            new NeverConnectedServerConnectionService(),
            new UnusedProxyClient());
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

    private static TerminalExecutionContext CreateContext(
        IReadOnlyList<string> arguments,
        TextWriter stdout,
        TextWriter stderr,
        TextReader stdin,
        string cwd)
    {
        AppManifest manifest = CreateManifest("cmd", "org.hackeros.cmd.test");
        LocalUserId userId = LocalUserId.FromGuid(Guid.NewGuid());

        TestAppFileSystemGateway fileSystem = new(userId);
        fileSystem.AddDirectory("/home/user");
        fileSystem.AddFile("/home/user/notes.txt", Encoding.UTF8.GetBytes("Hello HackerOS"));
        fileSystem.AddDirectory("/home/user/.config");

        TestAppExecutionContext app = new(manifest, fileSystem);

        Dictionary<string, string> env = new(StringComparer.Ordinal)
        {
            ["USER"] = "user",
            ["HOME"] = "/home/user"
        };

        return new TerminalExecutionContext(app, arguments, stdin, stdout, stderr, cwd, env);
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
        public IAppSettingsGateway Settings => throw new NotImplementedException();
        public IAppEventGateway Events => throw new NotImplementedException();
        public IAppNotificationGateway Notifications => throw new NotImplementedException();
        public IAppLoggingGateway Logging => throw new NotImplementedException();
        public IAppDiagnosticsGateway Diagnostics => throw new NotImplementedException();
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

    /// <summary>Fake that never recognizes a host, proving VFS-only cat arguments never touch the network.</summary>
    private sealed class NullSimulatedNetworkService : ISimulatedNetworkService
    {
        public SimulatedNavigationResult Navigate(string url, Dictionary<string, Dictionary<string, string>> sessionCookies) =>
            throw new NotSupportedException("Not exercised by these tests.");

        public SimulatedHttpResponse Post(string url, ImmutableDictionary<string, string> formBody, Dictionary<string, Dictionary<string, string>> sessionCookies) =>
            throw new NotSupportedException("Not exercised by these tests.");

        public double? Ping(string hostnameOrIp) => throw new NotSupportedException("Not exercised by these tests.");

        public IReadOnlyList<SimulatedPort> ScanPorts(string hostnameOrIp, int firstPort, int lastPort) =>
            throw new NotSupportedException("Not exercised by these tests.");

        public SimulatedHost? GetHost(string hostnameOrIp) => null;

        public IReadOnlyList<SimulatedHost> AllHosts => throw new NotSupportedException("Not exercised by these tests.");

        public ISimulatedDns Dns => throw new NotSupportedException("Not exercised by these tests.");
    }

    /// <summary>Fake used only to prove the pure-VFS path is unaffected: this device is never connected.</summary>
    private sealed class NeverConnectedServerConnectionService : IServerConnectionService
    {
        public ValueTask<ServerConnectionState?> GetStateAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ServerConnectionState?>(null);

        public Task<ServerConnectionState> ConnectWithNewAccountAsync(
            Uri serverBaseUrl, string username, string password, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by these tests.");

        public Task<ServerConnectionState> ConnectWithExistingAccountAsync(
            Uri serverBaseUrl, string username, string password, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by these tests.");

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public Task<string?> EnsureAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    /// <summary>Fake that always throws: proves the real-network path is never reached in these tests.</summary>
    private sealed class UnusedProxyClient : IProxyClient
    {
        public Task<ProxyHttpResponse> ExecuteHttpRequestAsync(
            Uri serverBaseUrl, string accessToken, ProxyHttpRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The proxy client must not be called by these tests.");

        public Task<ProxyTcpProbeResponse> ExecuteTcpProbeAsync(
            Uri serverBaseUrl, string accessToken, ProxyTcpProbeRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The proxy client must not be called by these tests.");

        public Task<ProxyPolicyResponse> GetPolicyAsync(
            Uri serverBaseUrl, string accessToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The proxy client must not be called by these tests.");
    }
}
