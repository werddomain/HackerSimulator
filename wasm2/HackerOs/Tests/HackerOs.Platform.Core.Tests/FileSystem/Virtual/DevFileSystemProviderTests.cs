using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.FileSystem;
using HackerOs.Simulation.Abstractions.Events;
using HackerOs.Simulation.Abstractions.FileSystem;
using static HackerOs.Platform.Core.Tests.FileSystem.Virtual.VirtualFileSystemFixture;

namespace HackerOs.Platform.Core.Tests.FileSystem.Virtual;

/// <summary>Contract tests for the virtual <c>/dev</c> mount (ADR 0046, section 6).</summary>
public sealed class DevFileSystemProviderTests
{
    private const string Read = AppCapabilities.FileSystemSystemRead;
    private const string Write = AppCapabilities.FileSystemSystemWrite;

    [Fact]
    public async Task Dev_lists_the_four_character_devices()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();

        FileSystemResult<FileSystemDirectorySnapshot> listing = await fixture.Service.EnumerateAsync(
            new FileSystemEnumerateRequest(VirtualPath.Parse("/dev")), fixture.AppContext(null, Read));

        Assert.Equal(["null", "random", "urandom", "zero"], listing.Value!.Entries.Select(static entry => entry.Name.Value));
        Assert.All(listing.Value.Entries, static entry =>
        {
            Assert.Equal(FileSystemEntryKind.CharacterDevice, entry.Metadata.Kind);
            Assert.Equal(0x1B6, entry.Metadata.Permissions.Mode); // 0666
        });
    }

    [Fact]
    public async Task Null_reads_empty_and_silently_absorbs_writes()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        FileSystemAuthorizationContext context = fixture.AppContext(null, Read, Write);
        await using ITopicChannelSubscription<FileSystemChangeEvent> watch = fixture.TopicBus.SubscribeChannel<FileSystemChangeEvent>(
            FileSystemTopics.ForDirectory(VirtualPath.Parse("/dev")), KernelPublisherIdentityForTests);
        long revisionBefore = (await fixture.Service.StatAsync(
            new FileSystemStatRequest(VirtualPath.Parse("/dev/null")), context)).Value!.Metadata.Revision;

        string content = await fixture.ReadTextAsync("/dev/null", context);
        FileSystemMutationResult write = await fixture.Service.WriteAsync(
            new FileSystemWriteRequest(VirtualPath.Parse("/dev/null")), new BytesSource(new byte[10_000]), context);

        Assert.Equal(string.Empty, content);
        Assert.True(write.Succeeded);
        Assert.Equal(revisionBefore, write.Entry!.Metadata.Revision);
        Assert.False(watch.Reader.TryRead(out _));
        Assert.Equal(0, fixture.Root.Mutations);
    }

    [Fact]
    public async Task Zero_is_an_endless_stream_read_in_bounded_blocks()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        FileSystemResult<FileSystemContentReadHandle> read = await fixture.Service.ReadAsync(
            new FileSystemReadRequest(VirtualPath.Parse("/dev/zero")), fixture.AppContext(null, Read));
        await using FileSystemContentReadHandle handle = read.Value!;

        byte[] buffer = Enumerable.Repeat((byte)0xFF, 10_000).ToArray();
        int total = 0;
        while (total < buffer.Length)
        {
            // A single read is deliberately inexact: each one hands out at most one 4 KiB block.
#pragma warning disable CA2022
            int count = await handle.Content.ReadAsync(buffer.AsMemory(total));
#pragma warning restore CA2022
            Assert.InRange(count, 1, 4096);
            total += count;
        }

        Assert.All(buffer, static value => Assert.Equal(0, value));
        Assert.Equal(FileSystemContentKind.Binary, handle.Descriptor.Kind);
    }

    [Fact]
    public async Task Endless_streams_honor_cancellation()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        FileSystemResult<FileSystemContentReadHandle> read = await fixture.Service.ReadAsync(
            new FileSystemReadRequest(VirtualPath.Parse("/dev/random")), fixture.AppContext(null, Read));
        await using FileSystemContentReadHandle handle = read.Value!;
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await handle.Content.ReadExactlyAsync(new byte[16], cancelled.Token));
    }

    [Fact]
    public async Task Urandom_returns_random_bytes()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        FileSystemResult<FileSystemContentReadHandle> read = await fixture.Service.ReadAsync(
            new FileSystemReadRequest(VirtualPath.Parse("/dev/urandom")), fixture.AppContext(null, Read));
        await using FileSystemContentReadHandle handle = read.Value!;

        byte[] first = new byte[64];
        byte[] second = new byte[64];
        await handle.Content.ReadExactlyAsync(first);
        await handle.Content.ReadExactlyAsync(second);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Device_creation_and_removal_are_refused()
    {
        VirtualFileSystemFixture fixture = await CreateSeededAsync();
        FileSystemAuthorizationContext system = fixture.SystemContext();

        FileSystemMutationResult create = await fixture.Service.CreateAsync(
            new FileSystemCreateRequest(VirtualPath.Parse("/dev/sda"), FileSystemEntryKind.CharacterDevice, FileSystemPermissions.FromMode(0x1B6)),
            system);
        FileSystemMutationResult delete = await fixture.Service.DeleteAsync(
            new FileSystemDeleteRequest(VirtualPath.Parse("/dev/null"), 1, 1), system);
        FileSystemMutationResult removeMount = await fixture.Service.DeleteAsync(
            new FileSystemDeleteRequest(VirtualPath.Parse("/dev"), 1, 1, recursive: true), system);

        Assert.Equal(FileSystemErrorCode.PermissionDenied, create.Transaction.Error?.Code);
        Assert.Equal(FileSystemErrorCode.PermissionDenied, delete.Transaction.Error?.Code);
        Assert.Equal(FileSystemErrorCode.ProtectedEntry, removeMount.Transaction.Error?.Code);
        Assert.Equal(0, fixture.Root.Mutations);
    }

    [Fact]
    public async Task Persistent_storage_refuses_character_devices()
    {
        InMemoryFileSystemRepository repository = new();
        VirtualFileSystemFixture fixture = new();

        FileSystemMutationResult result = await repository.CreateAsync(
            new FileSystemCreateRequest(VirtualPath.Parse("/null"), FileSystemEntryKind.CharacterDevice, FileSystemPermissions.FromMode(0x1B6)),
            fixture.SystemContext());

        Assert.Equal(FileSystemErrorCode.UnsupportedOperation, result.Transaction.Error?.Code);
    }

    private static PublisherIdentity KernelPublisherIdentityForTests => HackerOs.Platform.Core.Events.KernelPublisherIdentity.Value;
}
