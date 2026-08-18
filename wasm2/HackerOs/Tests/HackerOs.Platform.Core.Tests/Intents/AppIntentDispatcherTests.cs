using HackerOs.App.Abstractions;
using HackerOs.App.Abstractions.Policy;
using HackerOs.AppSdk;
using HackerOs.Platform.Core.Diagnostics;
using HackerOs.Platform.Core.Discovery;
using HackerOs.Platform.Core.Events;
using HackerOs.Platform.Core.Execution;
using HackerOs.Platform.Core.FileSystem;
using HackerOs.Platform.Core.Intents;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Platform.Core.Notifications;
using HackerOs.Platform.Core.Policy;
using HackerOs.Platform.Core.Processes;
using HackerOs.Platform.Core.Sessions;
using HackerOs.Platform.Core.Time;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.Diagnostics;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Tests.Intents;

/// <summary>
/// Tests for `P1-APP-007`: capability-gated dispatch of launch, open-file, execute-command, and
/// deferred-UI intents.
/// </summary>
public sealed class AppIntentDispatcherTests
{
    [Fact]
    public async Task Launching_another_app_is_denied_without_the_apps_launch_capability()
    {
        Fixture fixture = new(EchoManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        AppIntentRequest request = new(Guid.NewGuid(), "org.hackeros.shell", principal.UserId.ToString(), new LaunchAppIntent("org.hackeros.echo", []));

        AppIntentDispatchResult result = await fixture.Dispatcher.DispatchAsync(request, principal);

        Assert.Equal(AppIntentDispatchStatus.CapabilityDenied, result.Status);
    }

    [Fact]
    public async Task Launching_another_app_succeeds_once_the_caller_holds_apps_launch()
    {
        Fixture fixture = new(EchoManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        fixture.Grant("org.hackeros.shell", principal, AppCapabilities.AppsLaunch);
        AppIntentRequest request = new(Guid.NewGuid(), "org.hackeros.shell", principal.UserId.ToString(), new LaunchAppIntent("org.hackeros.echo", ["a"]));

        AppIntentDispatchResult result = await fixture.Dispatcher.DispatchAsync(request, principal);

        Assert.Equal(AppIntentDispatchStatus.Dispatched, result.Status);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Execute_command_intent_resolves_a_terminal_app_by_its_command_name_and_runs_it()
    {
        Fixture fixture = new(EchoManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        fixture.Grant("org.hackeros.shell", principal, AppCapabilities.AppsLaunch);
        AppIntentRequest request = new(Guid.NewGuid(), "org.hackeros.shell", principal.UserId.ToString(), new ExecuteCommandIntent("echo hi there"));

        AppIntentDispatchResult result = await fixture.Dispatcher.DispatchAsync(request, principal);

        Assert.Equal(AppIntentDispatchStatus.Dispatched, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"hi there{Environment.NewLine}", result.StandardOutput);
    }

    [Fact]
    public async Task Execute_command_intent_resolves_a_terminal_app_by_its_alias()
    {
        Fixture fixture = new(EchoManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        fixture.Grant("org.hackeros.shell", principal, AppCapabilities.AppsLaunch);
        AppIntentRequest request = new(Guid.NewGuid(), "org.hackeros.shell", principal.UserId.ToString(), new ExecuteCommandIntent("say hello"));

        AppIntentDispatchResult result = await fixture.Dispatcher.DispatchAsync(request, principal);

        Assert.Equal(AppIntentDispatchStatus.Dispatched, result.Status);
        Assert.Equal($"hello{Environment.NewLine}", result.StandardOutput);
    }

    [Fact]
    public async Task Execute_command_passes_full_screen_session_through_dispatch_and_lifecycle()
    {
        Fixture fixture = new(FullScreenManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        fixture.Grant("org.hackeros.shell", principal, AppCapabilities.AppsLaunch);
        TestFullScreenTerminal screen = new(new TerminalKeyEvent(TerminalKey.Character, "k"));
        AppIntentRequest request = new(
            Guid.NewGuid(), "org.hackeros.shell", principal.UserId.ToString(),
            new ExecuteCommandIntent("screen"));

        AppIntentDispatchResult result = await fixture.Dispatcher.DispatchAsync(
            request, principal, screen, CancellationToken.None);

        Assert.Equal(AppIntentDispatchStatus.Dispatched, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"k{Environment.NewLine}", result.StandardOutput);
        Assert.True(screen.Entered);
        Assert.True(screen.Left);
        Assert.NotNull(screen.Frame);
    }

    [Fact]
    public async Task Cancelling_full_screen_command_returns_shell_exit_130_and_restores_screen()
    {
        Fixture fixture = new(FullScreenManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        fixture.Grant("org.hackeros.shell", principal, AppCapabilities.AppsLaunch);
        TestFullScreenTerminal screen = new();
        using CancellationTokenSource cancellation = new();
        AppIntentRequest request = new(
            Guid.NewGuid(), "org.hackeros.shell", principal.UserId.ToString(),
            new ExecuteCommandIntent("screen wait"));

        ValueTask<AppIntentDispatchResult> pending = fixture.Dispatcher.DispatchAsync(
            request, principal, screen, cancellation.Token);
        await screen.EnteredSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        AppIntentDispatchResult result = await pending;

        Assert.Equal(AppIntentDispatchStatus.Dispatched, result.Status);
        Assert.Equal(130, result.ExitCode);
        Assert.True(screen.Left);
    }

    [Fact]
    public async Task An_unknown_command_name_returns_not_found()
    {
        Fixture fixture = new(EchoManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        fixture.Grant("org.hackeros.shell", principal, AppCapabilities.AppsLaunch);
        AppIntentRequest request = new(Guid.NewGuid(), "org.hackeros.shell", principal.UserId.ToString(), new ExecuteCommandIntent("nope"));

        AppIntentDispatchResult result = await fixture.Dispatcher.DispatchAsync(request, principal);

        Assert.Equal(AppIntentDispatchStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Open_file_intent_resolves_the_sole_candidate_and_launches_it()
    {
        Fixture fixture = new(NotepadManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        fixture.Grant("org.hackeros.shell", principal, AppCapabilities.AppsLaunch);
        AppIntentRequest request = new(
            Guid.NewGuid(), "org.hackeros.shell", principal.UserId.ToString(),
            new OpenFileIntent(VirtualPath.Parse("/home/alice/notes.txt"), FileIntentAction.Open));

        AppIntentDispatchResult result = await fixture.Dispatcher.DispatchAsync(request, principal);

        Assert.Equal(AppIntentDispatchStatus.Dispatched, result.Status);
        Assert.NotNull(result.Process);
    }

    [Fact]
    public async Task Open_file_intent_with_multiple_candidates_requires_a_chooser()
    {
        Fixture fixture = new(NotepadManifest(), TextEditorManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        fixture.Grant("org.hackeros.shell", principal, AppCapabilities.AppsLaunch);
        AppIntentRequest request = new(
            Guid.NewGuid(), "org.hackeros.shell", principal.UserId.ToString(),
            new OpenFileIntent(VirtualPath.Parse("/home/alice/notes.txt"), FileIntentAction.Open));

        AppIntentDispatchResult result = await fixture.Dispatcher.DispatchAsync(request, principal);

        Assert.Equal(AppIntentDispatchStatus.ChooserRequired, result.Status);
        Assert.Equal(2, result.CandidateAppIds!.Count);
    }

    [Fact]
    public async Task Open_file_intent_for_a_directory_resolves_org_hackeros_file_explorer_as_the_seeded_default_and_launches_it()
    {
        // Phase 5 (INT-006/INT-009): FileAssociationSettingsDocuments.EmptyDocumentContent seeds
        // org.hackeros.file-explorer as the protected default handler for inode/directory, so with the
        // real seeded association document (this fixture always uses CreateDefinition(), never a
        // synthetic one) an unpreferenced directory-open intent resolves as ConfiguredDefault — matching
        // the same end-to-end path FV-009's FileView.ActivateItemAsync(NewWindow) exercises when it calls
        // IAppIntentGateway.OpenFileAsync(path, mediaType: "inode/directory").
        Fixture fixture = new(DirectoryHandlerManifest("org.hackeros.file-explorer"));
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        fixture.Grant("org.hackeros.shell", principal, AppCapabilities.AppsLaunch);
        AppIntentRequest request = new(
            Guid.NewGuid(), "org.hackeros.shell", principal.UserId.ToString(),
            new OpenFileIntent(VirtualPath.Parse("/home/alice/Documents"), FileIntentAction.Open, MediaType: "inode/directory"));

        AppIntentDispatchResult result = await fixture.Dispatcher.DispatchAsync(request, principal);

        Assert.Equal(AppIntentDispatchStatus.Dispatched, result.Status);
        Assert.NotNull(result.Process);
    }

    [Fact]
    public async Task Reveal_file_and_show_settings_intents_dispatch_without_requiring_any_capability()
    {
        Fixture fixture = new(NotepadManifest());
        AuthenticatedPrincipal principal = await fixture.LoginAsync();
        AppIntentRequest reveal = new(
            Guid.NewGuid(), "org.hackeros.shell", principal.UserId.ToString(),
            new RevealFileIntent(VirtualPath.Parse("/home/alice/notes.txt")));
        AppIntentRequest showSettings = new(
            Guid.NewGuid(), "org.hackeros.shell", principal.UserId.ToString(),
            new ShowAppSettingsIntent("org.hackeros.notepad"));

        AppIntentDispatchResult revealResult = await fixture.Dispatcher.DispatchAsync(reveal, principal);
        AppIntentDispatchResult settingsResult = await fixture.Dispatcher.DispatchAsync(showSettings, principal);

        Assert.Equal(AppIntentDispatchStatus.Dispatched, revealResult.Status);
        Assert.Equal(AppIntentDispatchStatus.Dispatched, settingsResult.Status);
    }

    private static AppManifest EchoManifest() => new()
    {
        Id = "org.hackeros.echo",
        Name = "Echo",
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "Echoes its arguments.",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest("HackerOs.Platform.Core.Tests", typeof(EchoTerminalApp).FullName!),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("echo", ["say"], "echo [text]")
    };

    private static AppManifest NotepadManifest() => new()
    {
        Id = "org.hackeros.notepad",
        Name = "Notepad",
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "A simple text editor.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest("HackerOs.Platform.Core.Tests", "org.hackeros.notepad.EntryPoint"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        FileHandlers = [new FileHandlerManifest("text/plain", [".txt"], ["open", "edit"])]
    };

    private static AppManifest DirectoryHandlerManifest(string appId) => new()
    {
        Id = appId,
        Name = appId,
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "A directory-capable Window app for Phase 5 tests.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest("HackerOs.Platform.Core.Tests", $"{appId}.EntryPoint"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        FileHandlers = [new FileHandlerManifest("inode/directory", [], ["open"])]
    };

    private static AppManifest FullScreenManifest() => new()
    {
        Id = "org.hackeros.screen",
        Name = "Screen",
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "Exercises full-screen command dispatch.",
        Kind = AppKind.Terminal,
        EntryPoint = new AppEntryPointManifest(
            "HackerOs.Platform.Core.Tests", typeof(FullScreenTerminalApp).FullName!),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Hidden, []),
        Resources = AppResourceProfileManifest.None,
        Terminal = new TerminalCommandManifest("screen", [], "screen [wait]")
    };

    private static AppManifest TextEditorManifest() => new()
    {
        Id = "org.hackeros.texteditor",
        Name = "Text Editor",
        Version = "1.0.0",
        PublisherId = "org.hackeros",
        Description = "An advanced text editor.",
        Kind = AppKind.Window,
        EntryPoint = new AppEntryPointManifest("HackerOs.Platform.Core.Tests", "org.hackeros.texteditor.EntryPoint"),
        SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
        Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
        Resources = AppResourceProfileManifest.None,
        FileHandlers = [new FileHandlerManifest("text/plain", [".txt"], ["open", "edit"])]
    };

    private sealed class EchoTerminalApp(AppManifest manifest) : TerminalAppBase(manifest)
    {
        public override async ValueTask<int> ExecuteAsync(TerminalExecutionContext context, CancellationToken cancellationToken)
        {
            await context.StandardOutput.WriteLineAsync(string.Join(' ', context.Arguments).AsMemory(), cancellationToken);
            return 0;
        }
    }

    private sealed class FullScreenTerminalApp(AppManifest manifest) : TerminalAppBase(manifest)
    {
        public override async ValueTask<int> ExecuteAsync(
            TerminalExecutionContext context, CancellationToken cancellationToken)
        {
            IFullScreenTerminalSession screen = context.FullScreen
                ?? throw new InvalidOperationException("A full-screen session is required.");
            await screen.EnterAlternateScreenAsync(CancellationToken.None);
            try
            {
                await screen.RenderAsync(
                    new TerminalScreenFrame(["screen"], new TerminalCursor(0, 0)),
                    cancellationToken);
                if (context.Arguments.Contains("wait", StringComparer.Ordinal))
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return 0;
                }

                TerminalKeyEvent key = await screen.ReadKeyAsync(cancellationToken);
                await context.StandardOutput.WriteLineAsync(key.Text);
                return 0;
            }
            finally
            {
                await screen.LeaveAlternateScreenAsync(CancellationToken.None);
            }
        }
    }

    private sealed class TestFullScreenTerminal(params TerminalKeyEvent[] keys) : IFullScreenTerminalSession
    {
        private readonly Queue<TerminalKeyEvent> _keys = new(keys);
        public TerminalViewport Viewport { get; } = TerminalViewport.Create(80, 24);
        public bool Entered { get; private set; }
        public bool Left { get; private set; }
        public TerminalScreenFrame? Frame { get; private set; }
        public TaskCompletionSource EnteredSignal { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask EnterAlternateScreenAsync(CancellationToken cancellationToken = default)
        {
            Entered = true;
            EnteredSignal.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public ValueTask RenderAsync(
            TerminalScreenFrame frame, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Frame = frame;
            return ValueTask.CompletedTask;
        }

        public ValueTask<TerminalKeyEvent> ReadKeyAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_keys.Dequeue());
        }

        public ValueTask LeaveAlternateScreenAsync(CancellationToken cancellationToken = default)
        {
            Left = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Fixture
    {
        private int _entryId = 1;
        private int _transactionId = 100;
        private readonly DateTimeOffset _now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        private readonly LocalLoginName _aliceLoginName;

        internal Fixture(params AppManifest[] manifests)
        {
            FixedTimeProvider timeProvider = new(_now);
            InMemoryFileSystemRepository repository = new(
                () => FileSystemEntryId.FromGuid(new Guid(_entryId++, 0, 0, new byte[8])),
                () => new Guid(_transactionId++, 0, 0, new byte[8]),
                timeProvider);
            FileSystemMountRouter router = new(repository);
            Grants = new CapabilityGrantRepository(() => _now);
            InMemoryTopicMessageBus topicBus = new(Grants);
            FileSystemService fileSystem = new(
                router, new FileSystemPathResolver(router), new FileSystemAuthorizer(), topicBus,
                () => new Guid(_transactionId++, 0, 0, new byte[8]));
            FileSystemSeeder seeder = new(fileSystem, timeProvider);

            InMemoryLocalUserRepository users = new(() => _now);
            InMemoryLocalGroupRepository groups = new();
            InMemoryEventBus eventBus = new();
            BoundedAuditLog auditLog = new(maxEntries: 100);

            LocalGroup group = groups.CreateGroup(LocalLoginName.Parse("users"));
            _aliceLoginName = LocalLoginName.Parse("alice");
            users.CreateUser(
                _aliceLoginName, "Alice", AppAuthority.User, group.Id,
                credential: LocalPasswordHasher.Create("hunter2", iterations: 100));

            Session = new LocalSessionService(
                users, seeder, eventBus, auditLog,
                InstallationId.FromGuid(Guid.NewGuid()), DeviceId.FromGuid(Guid.NewGuid()), () => _now);

            ManualSimulationClock clock = new(_now, TimeSpan.FromSeconds(1));
            Manager = new InMemoryProcessManager(clock, Session, eventBus);
            InMemoryNotificationQueue notifications = new(maxEntriesPerUser: 20);
            BoundedDiagnosticSink diagnostics = new(maxEntries: 100);
            Settings = new InMemorySettingsDocumentService([FileAssociationSettingsDocuments.CreateDefinition()]);
            AppExecutionContextFactory contextFactory = new(
                Grants, fileSystem, Settings, eventBus, topicBus, notifications, diagnostics, clock, Manager);

            AppCatalogBuildResult catalogResult = AppCatalog.Build(manifests);
            Assert.True(catalogResult.IsSuccess, string.Join(", ", catalogResult.Errors.Select(e => e.Message)));
            AppCatalog catalog = catalogResult.Catalog!;

            Dictionary<string, System.Reflection.Assembly> hostAssemblies = new(StringComparer.Ordinal)
            {
                ["HackerOs.Platform.Core.Tests"] = typeof(Fixture).Assembly
            };
            AppDiscoveryResult discovery = AppEntryPointDiscovery.Discover(catalog, hostAssemblies);

            Dictionary<string, AppDescriptor> descriptors = new(StringComparer.Ordinal);
            foreach (AppManifest manifest in manifests)
            {
                descriptors[manifest.Id] = manifest.Kind == AppKind.Window
                    ? new AppDescriptor(manifest, typeof(object), typeof(object).Assembly)
                    : discovery.Descriptors![manifest.Id];
            }

            AppEnablementRegistry enablement = new(catalog);
            Orchestrator = new AppLifecycleOrchestrator(catalog, descriptors, enablement, Manager, Grants, contextFactory, Settings);
            FileAssociationResolver resolver = new(catalog, enablement, Settings);
            Dispatcher = new AppIntentDispatcher(Orchestrator, catalog, enablement, resolver, Grants);
        }

        internal LocalSessionService Session { get; }
        internal InMemoryProcessManager Manager { get; }
        internal CapabilityGrantRepository Grants { get; }
        internal InMemorySettingsDocumentService Settings { get; }
        internal AppLifecycleOrchestrator Orchestrator { get; }
        internal AppIntentDispatcher Dispatcher { get; }

        internal Task<AuthenticatedPrincipal> LoginAsync() => Session.LoginAsync(_aliceLoginName, "hunter2");

        internal void Grant(string appId, AuthenticatedPrincipal principal, string capability) =>
            Grants.Grant(appId, principal.UserId.ToString(), capability, CapabilityGrantSource.UserApproval, AppAuthority.Administrator);

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
