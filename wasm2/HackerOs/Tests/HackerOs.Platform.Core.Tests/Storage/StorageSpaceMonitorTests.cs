using HackerOs.App.Abstractions;
using HackerOs.Platform.Core.Notifications;
using HackerOs.Platform.Core.Storage;
using HackerOs.Simulation.Abstractions.Metrics;
using HackerOs.Simulation.Abstractions.Notifications;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Tests.Storage;

/// <summary>Low-space warning and root reserve of ADR 0099, section 2.</summary>
public sealed class StorageSpaceMonitorTests
{
    private const long MiB = 1024 * 1024;

    [Fact]
    public async Task Low_space_warns_the_session_user_once_with_an_action_that_opens_diagnostic()
    {
        Probe probe = new(new StorageCapacity(UsageBytes: 950 * MiB, QuotaBytes: 1000 * MiB));
        InMemoryNotificationQueue queue = new(maxEntriesPerUser: 10);
        AuthenticatedPrincipal alice = Principal("alice");
        await using StorageSpaceMonitor monitor = new(probe, queue, () => alice);

        await monitor.RefreshAsync();
        await monitor.RefreshAsync();

        Assert.True(monitor.IsLowSpace);
        Notification warning = Assert.Single(queue.GetActive(alice.UserId, DateTimeOffset.UtcNow));
        Assert.Equal(NotificationSeverity.Warning, warning.Severity);
        NotificationAction action = Assert.Single(warning.Actions);
        Assert.True(action.TryGetLaunchedApp(out string? appId));
        Assert.Equal(StorageSpaceMonitor.DiagnosticAppId, appId);
    }

    [Fact]
    public async Task A_new_session_user_is_warned_again()
    {
        Probe probe = new(new StorageCapacity(950 * MiB, 1000 * MiB));
        InMemoryNotificationQueue queue = new(maxEntriesPerUser: 10);
        AuthenticatedPrincipal current = Principal("alice");
        await using StorageSpaceMonitor monitor = new(probe, queue, () => current);

        await monitor.RefreshAsync();
        current = Principal("bob");
        await monitor.RefreshAsync();

        Assert.Single(queue.GetActive(current.UserId, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task The_root_reserve_is_five_percent_of_the_quota_at_most_256_mebibytes()
    {
        Probe probe = new(new StorageCapacity(940 * MiB, 1000 * MiB));
        await using StorageSpaceMonitor monitor = new(probe);

        await monitor.RefreshAsync();
        Assert.False(monitor.IsRootReserveReached);
        Assert.True(monitor.IsLowSpace);

        probe.Capacity = new StorageCapacity(951 * MiB, 1000 * MiB);
        await monitor.RefreshAsync();
        Assert.True(monitor.IsRootReserveReached);

        // A 100 GiB quota keeps 256 MiB for root, not 5 GiB.
        probe.Capacity = new StorageCapacity(100L * 1024 * MiB - 300 * MiB, 100L * 1024 * MiB);
        await monitor.RefreshAsync();
        Assert.False(monitor.IsRootReserveReached);
        Assert.Equal(256 * MiB, StorageSpacePolicy.ReserveBytes(100L * 1024 * MiB));
    }

    [Fact]
    public async Task An_unknown_quota_reserves_nothing_and_warns_nobody()
    {
        Probe probe = new(null);
        InMemoryNotificationQueue queue = new(maxEntriesPerUser: 10);
        AuthenticatedPrincipal alice = Principal("alice");
        await using StorageSpaceMonitor monitor = new(probe, queue, () => alice);

        await monitor.RefreshAsync();

        Assert.False(monitor.IsRootReserveReached);
        Assert.False(monitor.IsLowSpace);
        Assert.Null(monitor.LastCapacity);
        Assert.Empty(queue.GetActive(alice.UserId, DateTimeOffset.UtcNow));
    }

    private static AuthenticatedPrincipal Principal(string login)
    {
        LocalGroupId group = LocalGroupId.FromGuid(Guid.NewGuid());
        return new AuthenticatedPrincipal(
            SessionId.FromGuid(Guid.NewGuid()),
            LocalUserId.FromGuid(Guid.NewGuid()),
            LocalLoginName.Parse(login),
            login,
            AppAuthority.User,
            group,
            [group],
            InstallationId.FromGuid(Guid.NewGuid()),
            DeviceId.FromGuid(Guid.NewGuid()),
            DateTimeOffset.UnixEpoch);
    }

    private sealed class Probe(StorageCapacity? capacity) : IStorageCapacityProbe
    {
        public StorageCapacity? Capacity { get; set; } = capacity;

        public ValueTask<StorageCapacity?> GetCapacityAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Capacity);
    }
}
