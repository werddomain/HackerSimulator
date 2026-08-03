using HackerOs.Game.Abstractions;

namespace HackerOs.Game.Core;

/// <summary>
/// In-memory implementation of the Game Domain simulation state machine (`P4-W6-001` through `P4-W6-005`).
/// Manages player economy, contracts, hardware upgrade tree, security mechanics, and state persistence.
/// </summary>
public sealed class InMemoryGameDomainGateway : IGameDomainGateway
{
    private readonly object _lock = new();

    private PlayerProfileStats _stats = new(
        BalanceCredits: 500.00m,
        ReputationPoints: 10,
        CompletedMissionsCount: 0,
        FailedMissionsCount: 0);

    private VirtualHardwareProfile _hardware = new(
        CpuTier: 1,
        RamGb: 8,
        GpuTier: 1,
        IspBandwidthMbps: 50,
        MaintenanceCostPerDay: 5.00m);

    private readonly List<MissionContract> _contracts = [];
    private string? _activeContractId;

    public bool IsAvailable => true;

    public InMemoryGameDomainGateway()
    {
        SeedDefaultContracts();
    }

    private void SeedDefaultContracts()
    {
        _contracts.Add(new MissionContract(
            ContractId: "contract-001",
            Title: "Probe MegaCorp Gateway",
            Employer: "Anonymous Client",
            Description: "Perform a detailed port scan on megacorp.com (192.168.1.10) to identify open services.",
            DifficultyLevel: 1,
            PayoutCredits: 250.00m,
            ReputationReward: 15,
            TargetHost: "megacorp.com",
            Objectives: [
                new MissionObjective("obj-001-1", "Scan ports on megacorp.com", MissionObjectiveType.PortScan, "megacorp.com", "/"),
            ]));

        _contracts.Add(new MissionContract(
            ContractId: "contract-002",
            Title: "Exfiltrate DarkNet Research",
            Employer: "Cipher",
            Description: "Locate and download secret_research.dat from darknet-market.org.",
            DifficultyLevel: 2,
            PayoutCredits: 750.00m,
            ReputationReward: 40,
            TargetHost: "darknet-market.org",
            Objectives: [
                new MissionObjective("obj-002-1", "Download secret_research.dat", MissionObjectiveType.DownloadFile, "darknet-market.org", "/files/secret_research.dat")
            ]));

        _contracts.Add(new MissionContract(
            ContractId: "contract-003",
            Title: "Wipe Security Audit Logs",
            Employer: "GhostSec",
            Description: "Connect to cryptobank.com and wipe access logs to cover tracks.",
            DifficultyLevel: 3,
            PayoutCredits: 1500.00m,
            ReputationReward: 75,
            TargetHost: "cryptobank.com",
            Objectives: [
                new MissionObjective("obj-003-1", "Wipe server access log", MissionObjectiveType.WipeLogs, "cryptobank.com", "/var/log/auth.log")
            ]));
    }

    public ValueTask<PlayerProfileStats> GetPlayerStatsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock) return ValueTask.FromResult(_stats);
    }

    public ValueTask<VirtualHardwareProfile> GetHardwareProfileAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock) return ValueTask.FromResult(_hardware);
    }

    public ValueTask<IReadOnlyList<MissionContract>> GetAvailableContractsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var list = _contracts.Where(c => c.Status == MissionStatus.Available).ToList();
            return ValueTask.FromResult<IReadOnlyList<MissionContract>>(list);
        }
    }

    public ValueTask<MissionContract?> GetActiveContractAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var active = _contracts.FirstOrDefault(c => c.ContractId == _activeContractId);
            return ValueTask.FromResult(active);
        }
    }

    public ValueTask<bool> AcceptContractAsync(string contractId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var target = _contracts.FirstOrDefault(c => c.ContractId == contractId);
            if (target is null || target.Status != MissionStatus.Available) return ValueTask.FromResult(false);

            _activeContractId = contractId;
            int idx = _contracts.IndexOf(target);
            _contracts[idx] = target with { Status = MissionStatus.InProgress };
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<bool> SubmitProgressAsync(string objectiveId, object? progressData = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_activeContractId is null) return ValueTask.FromResult(false);

            var active = _contracts.FirstOrDefault(c => c.ContractId == _activeContractId);
            if (active is null) return ValueTask.FromResult(false);

            var updatedObjs = active.Objectives.Select(o => o.ObjectiveId == objectiveId ? o with { IsCompleted = true } : o).ToList();
            bool allComplete = updatedObjs.All(o => o.IsCompleted);

            int idx = _contracts.IndexOf(active);
            _contracts[idx] = active with
            {
                Objectives = updatedObjs,
                Status = allComplete ? MissionStatus.Completed : MissionStatus.InProgress
            };

            if (allComplete)
            {
                _stats = _stats with
                {
                    BalanceCredits = _stats.BalanceCredits + active.PayoutCredits,
                    ReputationPoints = _stats.ReputationPoints + active.ReputationReward,
                    CompletedMissionsCount = _stats.CompletedMissionsCount + 1
                };
                _activeContractId = null;
            }

            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<bool> PurchaseHardwareUpgradeAsync(string componentType, int targetTier, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            decimal cost = targetTier * 300.00m;
            if (_stats.BalanceCredits < cost) return ValueTask.FromResult(false);

            _stats = _stats with { BalanceCredits = _stats.BalanceCredits - cost };

            _hardware = componentType.ToLowerInvariant() switch
            {
                "cpu" => _hardware with { CpuTier = targetTier },
                "ram" => _hardware with { RamGb = targetTier * 8 },
                "gpu" => _hardware with { GpuTier = targetTier },
                "isp" => _hardware with { IspBandwidthMbps = targetTier * 100 },
                _ => _hardware
            };

            return ValueTask.FromResult(true);
        }
    }
}
