using HackerOs.App.Abstractions;

namespace HackerOs.Game.Abstractions;

/// <summary>
/// Gateway interface enabling authorized apps (`gameplay.domain.access`) to interact with
/// the Game Domain simulation state (contracts, hardware upgrades, player economy, and security mechanics).
/// Per ADR 0023, if the Game Domain is disabled in the build, <see cref="IsAvailable"/> returns false.
/// </summary>
public interface IGameDomainGateway
{
    /// <summary>Gets whether the Game Domain engine is enabled in this deployment build.</summary>
    bool IsAvailable { get; }

    /// <summary>Gets active player profile stats (balance, reputation, completed count).</summary>
    ValueTask<PlayerProfileStats> GetPlayerStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets active virtual hardware and ISP profile.</summary>
    ValueTask<VirtualHardwareProfile> GetHardwareProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets available mission contracts.</summary>
    ValueTask<IReadOnlyList<MissionContract>> GetAvailableContractsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the player's active contract, if any.</summary>
    ValueTask<MissionContract?> GetActiveContractAsync(CancellationToken cancellationToken = default);

    /// <summary>Accepts a contract by ID.</summary>
    ValueTask<bool> AcceptContractAsync(string contractId, CancellationToken cancellationToken = default);

    /// <summary>Submits progress towards an objective.</summary>
    ValueTask<bool> SubmitProgressAsync(string objectiveId, object? progressData = null, CancellationToken cancellationToken = default);

    /// <summary>Purchases a hardware upgrade if player balance allows.</summary>
    ValueTask<bool> PurchaseHardwareUpgradeAsync(string componentType, int targetTier, CancellationToken cancellationToken = default);
}

/// <summary>Null object fallback implementation when Game Domain is disabled or ungranted.</summary>
public sealed class NullGameDomainGateway : IGameDomainGateway
{
    public bool IsAvailable => false;

    public ValueTask<PlayerProfileStats> GetPlayerStatsAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new PlayerProfileStats(0, 0, 0, 0));

    public ValueTask<VirtualHardwareProfile> GetHardwareProfileAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new VirtualHardwareProfile(1, 4, 1, 10, 0));

    public ValueTask<IReadOnlyList<MissionContract>> GetAvailableContractsAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<MissionContract>>([]);

    public ValueTask<MissionContract?> GetActiveContractAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<MissionContract?>(null);

    public ValueTask<bool> AcceptContractAsync(string contractId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    public ValueTask<bool> SubmitProgressAsync(string objectiveId, object? progressData = null, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    public ValueTask<bool> PurchaseHardwareUpgradeAsync(string componentType, int targetTier, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);
}
