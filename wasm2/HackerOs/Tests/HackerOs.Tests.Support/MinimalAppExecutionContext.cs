using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Tests.Support;

/// <summary>
/// Minimal <see cref="IAppExecutionContext"/> test double. Only the gateways a test actually
/// passes in are usable; every other gateway throws <see cref="NotSupportedException"/> so a
/// test that reaches an unexpected gateway fails loudly instead of silently succeeding.
/// </summary>
public sealed class MinimalAppExecutionContext(
    AppManifest manifest,
    IAppFileSystemGateway? fileSystem = null,
    IAppSettingsGateway? settings = null,
    IAppEventGateway? events = null,
    IAppNotificationGateway? notifications = null,
    IAppLoggingGateway? logging = null,
    IAppDiagnosticsGateway? diagnostics = null,
    IAppClockGateway? clock = null,
    IAppProcessGateway? processes = null,
    IAppIntentGateway? intents = null,
    ICapabilityChecker? capabilities = null,
    string userId = "user",
    AppAuthority userAuthority = AppAuthority.User,
    SessionId? sessionId = null) : IAppExecutionContext
{
    public AppManifest Manifest { get; } = manifest;
    public Guid InstanceId { get; } = Guid.NewGuid();
    public string UserId { get; } = userId;
    public AppAuthority UserAuthority { get; } = userAuthority;
    public IReadOnlySet<string> GrantedCapabilities { get; } =
        new HashSet<string>(manifest.Capabilities, StringComparer.Ordinal);
    public SessionId SessionId { get; } = sessionId ?? SessionId.FromGuid(Guid.NewGuid());
    public ProcessId ProcessId { get; } = ProcessId.FromInt64(1);
    public CancellationToken CancellationToken => CancellationToken.None;

    public ICapabilityChecker Capabilities =>
        capabilities ?? throw Unsupported(nameof(Capabilities));
    public IAppFileSystemGateway FileSystem { get; } = fileSystem ?? new FakeAppFileSystemGateway();
    public IAppSettingsGateway Settings => settings ?? throw Unsupported(nameof(Settings));
    public IAppEventGateway Events => events ?? throw Unsupported(nameof(Events));
    public IAppNotificationGateway Notifications => notifications ?? throw Unsupported(nameof(Notifications));
    public IAppLoggingGateway Logging => logging ?? throw Unsupported(nameof(Logging));
    public IAppDiagnosticsGateway Diagnostics => diagnostics ?? throw Unsupported(nameof(Diagnostics));
    public IAppClockGateway Clock => clock ?? throw Unsupported(nameof(Clock));
    public IAppProcessGateway Processes => processes ?? throw Unsupported(nameof(Processes));
    public IAppIntentGateway Intents => intents ?? throw Unsupported(nameof(Intents));

    private static NotSupportedException Unsupported(string gatewayName) =>
        new($"'{gatewayName}' was not provided to this test's MinimalAppExecutionContext.");
}
