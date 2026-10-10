using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.Events;
using HackerOs.Platform.Core.Execution;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Notifications;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;
using HackerOs.Simulation.Abstractions.Settings;
using Xunit;

namespace HackerOs.Samples.TerminalApp.Tests;

public sealed class SampleTerminalAppTests
{
    private static readonly AppManifest Manifest = new()
    {
        SchemaVersion = 1,
        Id = "org.hackeros.samples.terminal-app",
        Name = "Sample Terminal App",
        Version = "1.0.0",
        PublisherId = "pub.hackeros",
        Description = "Sample Terminal command application",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Samples.TerminalApp.dll", "HackerOs.Samples.TerminalApp.SampleTerminalApp"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("utilities", AppLaunchVisibility.Visible, []),
        Terminal = new TerminalCommandManifest("sample-cmd", ["sample"], "Executes sample SDK terminal command"),
        Capabilities = [AppCapabilities.FileSystemUserHomeRead, AppCapabilities.FileSystemUserHomeWrite],
        Resources = AppResourceProfileManifest.None
    };

    [Fact]
    public void Manifest_HasTerminalKindAndCommandMetadata()
    {
        SampleTerminalApp app = new(Manifest);
        Assert.Equal(AppKind.Terminal, app.Manifest.Kind);
        Assert.NotNull(app.Manifest.Terminal);
        Assert.Equal("sample-cmd", app.Manifest.Terminal.Name);
    }

    [Fact]
    public async Task ExecuteAsync_WritesOutputAndReturnsZero()
    {
        SampleTerminalApp app = new(Manifest);
        StringWriter stdOut = new();
        StringWriter stdErr = new();
        TestExecutionContext appCtx = new(Manifest);
        TerminalExecutionContext termCtx = new(
            appCtx,
            ["Hello SDK 1.0"],
            TextReader.Null,
            stdOut,
            stdErr,
            "/home/user-1",
            new Dictionary<string, string>());

        int exitCode = await app.ExecuteAsync(termCtx, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Hello SDK 1.0", stdOut.ToString());
    }

    private sealed class TestExecutionContext : IAppExecutionContext
    {
        public TestExecutionContext(AppManifest manifest)
        {
            Manifest = manifest;
            Logging = new TestLoggingGateway();
        }

        public AppManifest Manifest { get; }
        public Guid InstanceId { get; } = Guid.NewGuid();
        public string UserId => "user-1";
        public AppAuthority UserAuthority => AppAuthority.User;
        public IReadOnlySet<string> GrantedCapabilities { get; } = new HashSet<string>(StringComparer.Ordinal);
        public SessionId SessionId { get; } = SessionId.FromGuid(Guid.NewGuid());
        public ProcessId ProcessId { get; } = ProcessId.FromInt64(100);
        public CancellationToken CancellationToken => CancellationToken.None;

        public ICapabilityChecker Capabilities => throw new NotImplementedException();
        public IAppFileSystemGateway FileSystem => throw new NotImplementedException();
        public IAppSettingsGateway Settings => throw new NotImplementedException();
        public IAppEventGateway Events => throw new NotImplementedException();
        public IAppNotificationGateway Notifications => throw new NotImplementedException();
        public IAppLoggingGateway Logging { get; }
        public IAppClockGateway Clock => throw new NotImplementedException();
        public IAppProcessGateway Processes => throw new NotImplementedException();
    }

    private sealed class TestLoggingGateway : IAppLoggingGateway
    {
        public void Log(DiagnosticSeverity severity, string message, IReadOnlyDictionary<string, string>? properties = null)
        {
        }
    }
}
