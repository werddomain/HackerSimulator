using System.Reflection;
using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Blazor;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.AppSdk.Blazor.Tests;

public sealed class WindowAppBaseTests
{
    [Theory]
    [InlineData("OnInitialized")]
    [InlineData("OnInitializedAsync")]
    [InlineData("OnParametersSet")]
    [InlineData("OnParametersSetAsync")]
    [InlineData("ShouldRender")]
    [InlineData("OnAfterRender")]
    [InlineData("OnAfterRenderAsync")]
    public void Framework_lifecycle_methods_are_sealed(string methodName)
    {
        MethodInfo? method = typeof(WindowAppBase).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.True(method.IsFinal);
    }

    [Fact]
    public async Task App_post_render_hook_runs_through_the_sealed_lifecycle()
    {
        TestWindowApp app = CreateWindowApp();

        await app.InvokeAfterRenderAsync(firstRender: true);

        Assert.True(app.AfterRenderCalled);
    }

    [Fact]
    public async Task Framework_post_render_setup_runs_before_the_app_hook()
    {
        List<string> calls = [];
        TestWindowApp app = CreateWindowApp();
        app.AfterRenderCallback = () => calls.Add("app");
        PropertyInfo lifecycleProperty = typeof(WindowAppBase).GetProperty(
            "FrameworkLifecycle",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        lifecycleProperty.SetValue(app, new TestFrameworkLifecycle(calls));

        await app.InvokeAfterRenderAsync(firstRender: true);

        Assert.Equal(["framework", "app"], calls);
    }

    [Fact]
    public void Window_base_rejects_a_non_window_manifest()
    {
        TestWindowApp app = CreateWindowApp(AppKind.Service);

        Assert.Throws<InvalidOperationException>(app.InvokeInitialized);
    }

    [Fact]
    public async Task File_dialog_helpers_delegate_with_the_bound_app_context()
    {
        FakeFileDialogService dialogs = new();
        TestWindowApp app = CreateWindowApp(dialogs: dialogs);
        OpenFileDialogRequest request = new()
        {
            InitialDirectory = VirtualPath.Parse("/home/user"),
            AllowMultiple = true
        };

        OpenFileDialogResult result = await app.InvokeOpenFileAsync(request);

        Assert.Same(app.AppContext, dialogs.LastContext);
        Assert.Same(request, dialogs.LastOpenRequest);
        Assert.Equal(FileDialogStatus.Cancelled, result.Status);
    }

    private static TestWindowApp CreateWindowApp(
        AppKind kind = AppKind.Window,
        IFileDialogService? dialogs = null)
    {
        AppManifest manifest = new()
        {
            Id = "org.hackeros.test-window",
            Name = "Test Window",
            Version = "1.0.0",
            PublisherId = "org.hackeros",
            Description = "Window App SDK test component.",
            Kind = kind,
            EntryPoint = new AppEntryPointManifest("HackerOs.AppSdk.Blazor.Tests", "TestWindowApp"),
            SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
            Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
            Resources = AppResourceProfileManifest.None
        };

        TestWindowApp app = new(dialogs ?? new FakeFileDialogService());
        app.BindContext(new TestExecutionContext(manifest));
        return app;
    }

    private sealed class TestWindowApp : WindowAppBase
    {
        public TestWindowApp(IFileDialogService dialogs)
        {
            FileDialogs = dialogs;
        }

        public bool AfterRenderCalled { get; private set; }

        public Action? AfterRenderCallback { get; set; }

        public void BindContext(IAppExecutionContext context) => AppContext = context;

        public void InvokeInitialized() => base.OnInitialized();

        public Task InvokeAfterRenderAsync(bool firstRender) => base.OnAfterRenderAsync(firstRender);

        public ValueTask<OpenFileDialogResult> InvokeOpenFileAsync(OpenFileDialogRequest request) =>
            base.OpenFileAsync(request);

        protected override Task OnAppAfterRenderAsync(bool firstRender)
        {
            AfterRenderCalled = firstRender;
            AfterRenderCallback?.Invoke();
            return Task.CompletedTask;
        }

    }

    private sealed class TestFrameworkLifecycle(List<string> calls) : IWindowAppFrameworkLifecycle
    {
        public Task OnAfterRenderAsync(WindowAppBase app, bool firstRender)
        {
            _ = app;
            _ = firstRender;
            calls.Add("framework");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFileDialogService : IFileDialogService
    {
        public IAppExecutionContext? LastContext { get; private set; }

        public OpenFileDialogRequest? LastOpenRequest { get; private set; }

        public ValueTask<OpenFileDialogResult> OpenFileAsync(
            IAppExecutionContext context,
            OpenFileDialogRequest request,
            CancellationToken cancellationToken = default)
        {
            LastContext = context;
            LastOpenRequest = request;
            return ValueTask.FromResult(new OpenFileDialogResult(FileDialogStatus.Cancelled, []));
        }

        public ValueTask<SaveFileDialogResult> SaveFileAsync(
            IAppExecutionContext context,
            SaveFileDialogRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new SaveFileDialogResult(FileDialogStatus.Cancelled, null));

        public ValueTask<SelectFolderDialogResult> SelectFolderAsync(
            IAppExecutionContext context,
            SelectFolderDialogRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new SelectFolderDialogResult(FileDialogStatus.Cancelled, null));
    }

    private sealed class TestExecutionContext(AppManifest manifest) : IAppExecutionContext
    {
        public AppManifest Manifest { get; } = manifest;

        public Guid InstanceId { get; } = Guid.NewGuid();

        public string UserId => "user";

        public AppAuthority UserAuthority => AppAuthority.User;

        public IReadOnlySet<string> GrantedCapabilities { get; } = new HashSet<string>();

        public SessionId SessionId { get; } = SessionId.FromGuid(Guid.NewGuid());

        public ProcessId ProcessId { get; } = ProcessId.FromInt64(1);

        public CancellationToken CancellationToken => CancellationToken.None;

        public ICapabilityChecker Capabilities => throw new NotSupportedException();

        public IAppFileSystemGateway FileSystem => throw new NotSupportedException();

        public IAppSettingsGateway Settings => throw new NotSupportedException();

        public IAppEventGateway Events => throw new NotSupportedException();

        public IAppNotificationGateway Notifications => throw new NotSupportedException();

        public IAppLoggingGateway Logging => throw new NotSupportedException();

        public IAppClockGateway Clock => throw new NotSupportedException();

        public IAppProcessGateway Processes => throw new NotSupportedException();
    }
}