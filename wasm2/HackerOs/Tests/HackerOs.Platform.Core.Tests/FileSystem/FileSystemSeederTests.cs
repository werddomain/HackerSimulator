using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.FileSystem;
using HackerOs.Simulation.Abstractions.FileSystem;

namespace HackerOs.Platform.Core.Tests.FileSystem;

public sealed class FileSystemSeederTests
{
    [Fact]
    public async Task Clean_profile_contains_required_system_and_user_directories()
    {
        Fixture fixture = new();

        await fixture.Seeder.SeedAsync("alice", "users");

        string[] paths =
        [
            "/", "/bin", "/etc", "/home", "/tmp", "/var", "/var/log",
            "/home/alice", "/home/alice/Desktop", "/home/alice/Documents",
            "/home/alice/Downloads", "/home/alice/.config",
            "/home/alice/.config/hackeros", "/home/alice/.config/apps"
        ];
        foreach (string path in paths)
        {
            FileSystemResult<FileSystemEntrySnapshot> result = await fixture.Repository.StatAsync(
                new FileSystemStatRequest(VirtualPath.Parse(path)),
                fixture.SystemContext);
            Assert.Equal(FileSystemEntryKind.Directory, result.Value?.Metadata.Kind);
        }

        FileSystemEntrySnapshot home = (await fixture.Repository.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/home/alice")),
            fixture.SystemContext)).Value!;
        Assert.Equal("alice", home.Metadata.OwnerId);
        Assert.Equal("users", home.Metadata.GroupId);
    }

    [Fact]
    public async Task Repeated_seed_preserves_entry_ids_and_revisions()
    {
        Fixture fixture = new();
        await fixture.Seeder.SeedAsync("alice", "users");
        FileSystemEntrySnapshot before = (await fixture.Repository.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/home/alice/Documents")),
            fixture.SystemContext)).Value!;

        await fixture.Seeder.SeedAsync("alice", "users");
        FileSystemEntrySnapshot after = (await fixture.Repository.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/home/alice/Documents")),
            fixture.SystemContext)).Value!;

        Assert.Equal(before.Metadata.Id, after.Metadata.Id);
        Assert.Equal(before.Metadata.Revision, after.Metadata.Revision);
    }

    [Fact]
    public async Task Adding_second_user_preserves_first_user_home()
    {
        Fixture fixture = new();
        await fixture.Seeder.SeedAsync("alice", "users");
        FileSystemEntrySnapshot aliceBefore = (await fixture.Repository.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/home/alice")),
            fixture.SystemContext)).Value!;

        await fixture.Seeder.SeedAsync("bob", "users");
        FileSystemEntrySnapshot aliceAfter = (await fixture.Repository.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/home/alice")),
            fixture.SystemContext)).Value!;
        FileSystemEntrySnapshot bob = (await fixture.Repository.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/home/bob")),
            fixture.SystemContext)).Value!;

        Assert.Equal(aliceBefore.Metadata.Id, aliceAfter.Metadata.Id);
        Assert.Equal("bob", bob.Metadata.OwnerId);
    }

    private sealed class Fixture
    {
        private int _entryId = 1;
        private int _transactionId = 100;
        private readonly DateTimeOffset _now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

        internal Fixture()
        {
            FixedTimeProvider timeProvider = new(_now);
            Repository = new InMemoryFileSystemRepository(
                () => FileSystemEntryId.FromGuid(new Guid(_entryId++, 0, 0, new byte[8])),
                () => new Guid(_transactionId++, 0, 0, new byte[8]),
                timeProvider);
            FileSystemMountRouter router = new(Repository);
            FileSystemService service = new(
                router,
                new FileSystemPathResolver(router),
                new FileSystemAuthorizer(),
                () => new Guid(_transactionId++, 0, 0, new byte[8]));
            Seeder = new FileSystemSeeder(service, timeProvider);
            SystemContext = CreateSystemContext(_now);
        }

        internal InMemoryFileSystemRepository Repository { get; }
        internal FileSystemSeeder Seeder { get; }
        internal FileSystemAuthorizationContext SystemContext { get; }

        private static FileSystemAuthorizationContext CreateSystemContext(DateTimeOffset now)
        {
            AppOperationContext operation = new()
            {
                AppId = "org.hackeros.kernel",
                UserId = "system",
                UserAuthority = AppAuthority.System,
                GrantedCapabilities = new HashSet<string>(AppCapabilities.All, StringComparer.Ordinal),
                IsSystemOperation = true
            };
            return new FileSystemAuthorizationContext(operation, ["system"], now);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}