using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Blazor;
using HackerOs.Platform.Blazor.Dialogs;
using HackerOs.Simulation.Abstractions.FileSystem;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Blazor.Tests;

public sealed class DialogCoordinatorTests
{
    private static readonly SessionId TestSessionId =
        SessionId.FromGuid(Guid.Parse("50000000-0000-0000-0000-000000000002"));

    [Fact]
    public async Task Message_box_dialog_flow_completes_with_user_action()
    {
        FakeFileDialogService fileDialogs = new();
        using DialogCoordinator coordinator = new(fileDialogs);
        TestExecutionContext context = new(TestSessionId);

        Task<MessageBoxDialogResult> messageBoxTask = coordinator.MessageBoxAsync(
            context,
            new MessageBoxDialogRequest
            {
                Title = "Confirmation",
                Content = "Are you sure?",
                DialogType = MessageBoxType.YesNo
            }).AsTask();

        MessageBoxPresentation active = Assert.IsType<MessageBoxPresentation>(coordinator.ActiveRequest);
        Assert.Equal("Confirmation", active.Request.Title);
        Assert.Equal("Are you sure?", active.Request.Content);
        Assert.Equal(MessageBoxType.YesNo, active.Request.DialogType);

        Assert.True(coordinator.SelectMessageBox(active.Id, MessageBoxResult.Yes));

        MessageBoxDialogResult result = await messageBoxTask;
        Assert.Equal(MessageBoxResult.Yes, result.Result);
        Assert.Equal(MessageboxResult.Yes.Value, result.Result);
        Assert.Null(coordinator.ActiveRequest);
    }

    [Fact]
    public async Task Text_input_dialog_flow_completes_with_submitted_value()
    {
        FakeFileDialogService fileDialogs = new();
        using DialogCoordinator coordinator = new(fileDialogs);
        TestExecutionContext context = new(TestSessionId);

        Task<TextInputDialogResult> inputTask = coordinator.TextInputAsync(
            context,
            new TextInputDialogRequest
            {
                Title = "Rename File",
                Content = "Enter new file name:",
                DefaultValue = "document.txt",
                Placeholder = "Name..."
            }).AsTask();

        TextInputPresentation active = Assert.IsType<TextInputPresentation>(coordinator.ActiveRequest);
        Assert.Equal("Rename File", active.Request.Title);
        Assert.Equal("Enter new file name:", active.Request.Content);

        Assert.True(coordinator.SelectTextInput(active.Id, "new_document.txt"));

        TextInputDialogResult result = await inputTask;
        Assert.Equal(TextInputStatus.Submitted, result.Status);
        Assert.Equal("new_document.txt", result.Value);
        Assert.Null(coordinator.ActiveRequest);
    }

    [Fact]
    public async Task File_dialog_methods_delegate_directly_to_file_dialog_service()
    {
        FakeFileDialogService fileDialogs = new();
        using DialogCoordinator coordinator = new(fileDialogs);
        TestExecutionContext context = new(TestSessionId);
        OpenFileDialogRequest request = new();

        OpenFileDialogResult result = await coordinator.OpenFileAsync(context, request);

        Assert.Same(context, fileDialogs.LastContext);
        Assert.Same(request, fileDialogs.LastOpenRequest);
        Assert.Equal(FileDialogStatus.Cancelled, result.Status);
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

    private sealed class TestExecutionContext(SessionId sessionId) : IAppExecutionContext
    {
        public AppManifest Manifest { get; } = new()
        {
            Id = "org.hackeros.test-app",
            Name = "Test App",
            Version = "1.0.0",
            PublisherId = "org.hackeros",
            Description = "Test app",
            Kind = AppKind.Window,
            EntryPoint = new AppEntryPointManifest("Test", "Test"),
            SdkCompatibility = new AppSdkCompatibilityManifest("1.0.0"),
            Presentation = new PresentationManifest("test", AppLaunchVisibility.Visible, []),
            Resources = AppResourceProfileManifest.None
        };

        public Guid InstanceId { get; } = Guid.NewGuid();
        public string UserId => "user";
        public AppAuthority UserAuthority => AppAuthority.User;
        public IReadOnlySet<string> GrantedCapabilities { get; } = new HashSet<string>();
        public SessionId SessionId { get; } = sessionId;
        public ProcessId ProcessId { get; } = ProcessId.FromInt64(10);
        public CancellationToken CancellationToken => CancellationToken.None;

        public ICapabilityChecker Capabilities => throw new NotSupportedException();
        public IAppFileSystemGateway FileSystem => throw new NotSupportedException();
        public IAppSettingsGateway Settings => throw new NotSupportedException();
        public IAppEventGateway Events => throw new NotSupportedException();
        public IAppNotificationGateway Notifications => throw new NotSupportedException();
        public IAppLoggingGateway Logging => throw new NotSupportedException();
        public IAppDiagnosticsGateway Diagnostics => throw new NotSupportedException();
        public IAppClockGateway Clock => throw new NotSupportedException();
        public IAppProcessGateway Processes => throw new NotSupportedException();
    }
}
