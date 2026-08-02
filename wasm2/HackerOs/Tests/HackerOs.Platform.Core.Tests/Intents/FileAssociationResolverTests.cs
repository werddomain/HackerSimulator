using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Intents;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Simulation.Abstractions;

namespace HackerOs.Platform.Core.Tests.Intents;

/// <summary>
/// Tests for `P1-APP-008`, `P1-APP-008A`, `P1-APP-008B`, and `P1-APP-009`: resolving an
/// <see cref="OpenFileIntent"/> to a target app using the documented precedence order.
/// </summary>
public sealed class FileAssociationResolverTests
{
    [Fact]
    public async Task An_explicit_valid_preferred_app_is_used_as_the_target()
    {
        Fixture fixture = new(NotepadManifest());
        OpenFileIntent intent = new(VirtualPath.Parse("/home/alice/notes.txt"), FileIntentAction.Open, PreferredAppId: "org.hackeros.notepad");

        FileHandlerResolution resolution = await fixture.Resolver.ResolveAsync(intent, fixture.ReadContext);

        Assert.Equal(FileHandlerResolutionStatus.ExplicitTarget, resolution.Status);
        Assert.Equal("org.hackeros.notepad", resolution.AppId);
    }

    [Fact]
    public async Task An_explicit_preferred_app_that_cannot_handle_the_file_is_invalid()
    {
        Fixture fixture = new(NotepadManifest());
        OpenFileIntent intent = new(VirtualPath.Parse("/home/alice/photo.png"), FileIntentAction.Open, PreferredAppId: "org.hackeros.notepad");

        FileHandlerResolution resolution = await fixture.Resolver.ResolveAsync(intent, fixture.ReadContext);

        Assert.Equal(FileHandlerResolutionStatus.TargetInvalid, resolution.Status);
    }

    [Fact]
    public async Task An_explicit_preferred_app_that_is_disabled_is_invalid()
    {
        Fixture fixture = new(NotepadManifest());
        fixture.Disable("org.hackeros.notepad");
        OpenFileIntent intent = new(VirtualPath.Parse("/home/alice/notes.txt"), FileIntentAction.Open, PreferredAppId: "org.hackeros.notepad");

        FileHandlerResolution resolution = await fixture.Resolver.ResolveAsync(intent, fixture.ReadContext);

        Assert.Equal(FileHandlerResolutionStatus.TargetInvalid, resolution.Status);
    }

    [Fact]
    public async Task A_configured_default_from_the_association_document_is_preferred_over_candidates()
    {
        Fixture fixture = new(NotepadManifest(), TextEditorManifest());
        await fixture.ConfigureDefaultAsync(".txt", "org.hackeros.texteditor");
        OpenFileIntent intent = new(VirtualPath.Parse("/home/alice/notes.txt"), FileIntentAction.Open);

        FileHandlerResolution resolution = await fixture.Resolver.ResolveAsync(intent, fixture.ReadContext);

        Assert.Equal(FileHandlerResolutionStatus.ConfiguredDefault, resolution.Status);
        Assert.Equal("org.hackeros.texteditor", resolution.AppId);
    }

    [Fact]
    public async Task A_sole_manifest_declared_candidate_is_used_without_a_configured_default()
    {
        Fixture fixture = new(NotepadManifest());
        OpenFileIntent intent = new(VirtualPath.Parse("/home/alice/notes.txt"), FileIntentAction.Open);

        FileHandlerResolution resolution = await fixture.Resolver.ResolveAsync(intent, fixture.ReadContext);

        Assert.Equal(FileHandlerResolutionStatus.SoleCandidate, resolution.Status);
        Assert.Equal("org.hackeros.notepad", resolution.AppId);
    }

    [Fact]
    public async Task Multiple_candidates_without_a_configured_default_require_a_chooser()
    {
        Fixture fixture = new(NotepadManifest(), TextEditorManifest());
        OpenFileIntent intent = new(VirtualPath.Parse("/home/alice/notes.txt"), FileIntentAction.Open);

        FileHandlerResolution resolution = await fixture.Resolver.ResolveAsync(intent, fixture.ReadContext);

        Assert.Equal(FileHandlerResolutionStatus.ChooserRequired, resolution.Status);
        Assert.Equal(["org.hackeros.notepad", "org.hackeros.texteditor"], resolution.CandidateAppIds);
    }

    [Fact]
    public async Task No_enabled_candidate_returns_no_handler()
    {
        Fixture fixture = new(NotepadManifest());
        OpenFileIntent intent = new(VirtualPath.Parse("/home/alice/photo.png"), FileIntentAction.Open);

        FileHandlerResolution resolution = await fixture.Resolver.ResolveAsync(intent, fixture.ReadContext);

        Assert.Equal(FileHandlerResolutionStatus.NoHandler, resolution.Status);
    }

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

    private sealed class Fixture
    {
        private readonly AppEnablementRegistry _enablement;

        internal Fixture(params AppManifest[] manifests)
        {
            AppCatalogBuildResult catalogResult = AppCatalog.Build(manifests);
            Assert.True(catalogResult.IsSuccess, string.Join(", ", catalogResult.Errors.Select(e => e.Message)));
            AppCatalog catalog = catalogResult.Catalog!;
            _enablement = new AppEnablementRegistry(catalog);
            Settings = new InMemorySettingsDocumentService([FileAssociationSettingsDocuments.CreateDefinition()]);
            Resolver = new FileAssociationResolver(catalog, _enablement, Settings);
            ReadContext = new AppOperationContext
            {
                AppId = "org.hackeros.shell",
                UserId = "alice",
                UserAuthority = AppAuthority.User,
                GrantedCapabilities = new HashSet<string>(StringComparer.Ordinal) { AppCapabilities.FileAssociationsRead },
                IsSystemOperation = false
            };
            WriteContext = ReadContext with
            {
                GrantedCapabilities = new HashSet<string>(StringComparer.Ordinal)
                {
                    AppCapabilities.FileAssociationsRead, AppCapabilities.FileAssociationsWrite
                },
                UserAuthority = AppAuthority.Administrator
            };
        }

        internal InMemorySettingsDocumentService Settings { get; }
        internal FileAssociationResolver Resolver { get; }
        internal AppOperationContext ReadContext { get; }
        internal AppOperationContext WriteContext { get; }

        internal void Disable(string appId) => _enablement.MarkDisabled([appId]);

        internal async Task ConfigureDefaultAsync(string extension, string appId)
        {
            SettingsReadResult read = await Settings.ReadAsync(FileAssociationSettingsDocuments.Path, WriteContext);
            string content = $$"""
                {"schemaVersion":1,"associations":[{"extension":"{{extension}}","appId":"{{appId}}","actions":["open","edit"]}]}
                """;
            SettingsWriteResult write = await Settings.WriteAsync(
                new SettingsWriteRequest(FileAssociationSettingsDocuments.Path, content, read.Document!.Revision), WriteContext);
            Assert.Equal(SettingsWriteStatus.Success, write.Status);
        }
    }
}
