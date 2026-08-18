using System.Text;
using HackerOs.App.Abstractions;
using HackerOs.Platform.Core;
using HackerOs.Platform.Core.Events;
using HackerOs.Platform.Core.FileSystem;
using HackerOs.Platform.Core.Policy;
using HackerOs.Simulation.Abstractions;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.Tests.FileSystem;

public sealed class SettingsFileSystemProviderTests
{
    private static readonly VirtualPath AssociationsPath =
        VirtualPath.Parse("/etc/hackeros/file-associations.json");

    [Fact]
    public async Task Filesystem_read_observes_the_canonical_settings_revision()
    {
        Fixture fixture = await Fixture.CreateAsync();

        FileSystemResult<FileSystemContentReadHandle> result = await fixture.Service.ReadAsync(
            new FileSystemReadRequest(AssociationsPath),
            fixture.AdminContext);

        await using FileSystemContentReadHandle handle = result.Value!;
        using StreamReader reader = new(handle.Content, Encoding.UTF8);
        Assert.Equal(Fixture.InitialSettings, await reader.ReadToEndAsync());
        Assert.Equal(1, handle.Entry.Metadata.Revision);
    }

    [Fact]
    public async Task Filesystem_and_direct_writes_share_one_revision_sequence()
    {
        Fixture fixture = await Fixture.CreateAsync();
        FileSystemMutationResult fileWrite = await fixture.Service.WriteAsync(
            new FileSystemWriteRequest(AssociationsPath, 1),
            new TextSource(Fixture.ValidSettings(".txt")),
            fixture.AdminContext);
        SettingsWriteResult directWrite = await fixture.Settings.WriteAsync(
            new SettingsWriteRequest(AssociationsPath, Fixture.ValidSettings(".log"), 2),
            fixture.AdminContext.OperationContext);
        FileSystemResult<FileSystemEntrySnapshot> stat = await fixture.Service.StatAsync(
            new FileSystemStatRequest(AssociationsPath),
            fixture.AdminContext);

        Assert.Equal(2, fileWrite.Entry?.Metadata.Revision);
        Assert.Equal(3, directWrite.Document?.Revision);
        Assert.Equal(3, stat.Value?.Metadata.Revision);
    }

    [Fact]
    public async Task Projection_mount_precedes_an_ordinary_shadow_file()
    {
        Fixture fixture = await Fixture.CreateAsync(createOrdinaryShadow: true);

        FileSystemResult<FileSystemContentReadHandle> result = await fixture.Service.ReadAsync(
            new FileSystemReadRequest(AssociationsPath),
            fixture.AdminContext);

        await using FileSystemContentReadHandle handle = result.Value!;
        using StreamReader reader = new(handle.Content, Encoding.UTF8);
        Assert.Equal(Fixture.InitialSettings, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Invalid_projected_write_is_atomic_and_preserves_revision()
    {
        Fixture fixture = await Fixture.CreateAsync();

        FileSystemMutationResult result = await fixture.Service.WriteAsync(
            new FileSystemWriteRequest(AssociationsPath, 1),
            new TextSource("{ invalid"),
            fixture.AdminContext);
        SettingsReadResult direct = await fixture.Settings.ReadAsync(
            AssociationsPath,
            fixture.AdminContext.OperationContext);

        Assert.Equal(FileSystemErrorCode.InvalidContent, result.Transaction.Error?.Code);
        Assert.Equal(1, direct.Document?.Revision);
        Assert.Equal(Fixture.InitialSettings, direct.Document?.Content);
    }

    private sealed class Fixture
    {
        internal const string InitialSettings = """
            {"schemaVersion":1,"associations":[]}
            """;

        private int _entryId = 1;
        private int _transactionId = 100;

        private Fixture()
        {
            Definition = new SettingsDocumentDefinition(
                AssociationsPath,
                HackerOs.Simulation.Abstractions.Settings.SettingsDocumentKey.ForOsAdmin("file-associations"),
                InitialSettings,
                "application/json",
                AppCapabilities.FileAssociationsRead,
                AppCapabilities.FileAssociationsWrite,
                AppAuthority.User,
                AppAuthority.Administrator,
                new FileAssociationSettingsValidator());
            Settings = new InMemorySettingsDocumentService([Definition]);
            Projection = new SettingsFileProjection(Settings);
            Repository = new InMemoryFileSystemRepository(
                () => FileSystemEntryId.FromGuid(new Guid(_entryId++, 0, 0, new byte[8])),
                () => new Guid(_transactionId++, 0, 0, new byte[8]),
                new FixedTimeProvider());
            AdminContext = CreateAdminContext();
        }

        internal SettingsDocumentDefinition Definition { get; }
        internal InMemorySettingsDocumentService Settings { get; }
        internal SettingsFileProjection Projection { get; }
        internal InMemoryFileSystemRepository Repository { get; }
        internal FileSystemAuthorizationContext AdminContext { get; }
        internal FileSystemService Service { get; private set; } = null!;

        internal static async Task<Fixture> CreateAsync(bool createOrdinaryShadow = false)
        {
            Fixture fixture = new();
            FileSystemMutationResult etc = await fixture.Repository.CreateAsync(
                Directory("/etc", 1),
                fixture.AdminContext);
            if (createOrdinaryShadow)
            {
                FileSystemMutationResult hackeros = await fixture.Repository.CreateAsync(
                    Directory("/etc/hackeros", etc.Entry!.Metadata.Revision),
                    fixture.AdminContext);
                FileSystemMutationResult file = await fixture.Repository.CreateAsync(
                    new FileSystemCreateRequest(
                        AssociationsPath,
                        FileSystemEntryKind.File,
                        FileSystemPermissions.FromMode(0x01A4),
                        hackeros.Entry!.Metadata.Revision),
                    fixture.AdminContext);
                await fixture.Repository.WriteAsync(
                    new FileSystemWriteRequest(AssociationsPath, file.Entry!.Metadata.Revision),
                    new TextSource("shadow"),
                    fixture.AdminContext);
            }

            SettingsFileSystemProvider settingsProvider = new(
                fixture.Projection,
                [fixture.Definition],
                VirtualPath.Parse("/etc/hackeros"),
                () => new Guid(fixture._transactionId++, 0, 0, new byte[8]),
                new FixedTimeProvider());
            FileSystemMountRouter router = new(fixture.Repository,
            [
                new FileSystemMount(VirtualPath.Parse("/etc/hackeros"), settingsProvider)
            ]);
            fixture.Service = new FileSystemService(
                router,
                new FileSystemPathResolver(router),
                new FileSystemAuthorizer(),
                new InMemoryTopicMessageBus(new CapabilityGrantRepository()),
                () => new Guid(fixture._transactionId++, 0, 0, new byte[8]));
            return fixture;
        }

        internal static string ValidSettings(string extension) => $$"""
            {"schemaVersion":1,"associations":[{"extension":"{{extension}}","appId":"org.hackeros.text-editor","actions":["open"]}]}
            """;

        private static FileSystemCreateRequest Directory(string path, long parentRevision) =>
            new(
                VirtualPath.Parse(path),
                FileSystemEntryKind.Directory,
                FileSystemPermissions.FromMode(0x01FF),
                parentRevision);

        private static FileSystemAuthorizationContext CreateAdminContext()
        {
            AppOperationContext operation = new()
            {
                AppId = "org.hackeros.settings",
                UserId = "admin",
                UserAuthority = AppAuthority.Administrator,
                GrantedCapabilities = new HashSet<string>(AppCapabilities.All, StringComparer.Ordinal)
            };
            return new FileSystemAuthorizationContext(
                operation,
                ["administrators"],
                new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero));
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class TextSource(string content) : IFileSystemContentSource
    {
        private readonly byte[] _content = Encoding.UTF8.GetBytes(content);
        public FileSystemContentDescriptor Descriptor { get; } =
            FileSystemContentDescriptor.Text("application/json");
        public long? Length => _content.LongLength;
        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Stream>(new MemoryStream(_content, writable: false));
    }
}