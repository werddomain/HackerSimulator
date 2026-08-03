namespace HackerOs.Game.Abstractions;

/// <summary>Status of a simulated hacker contract.</summary>
public enum MissionStatus
{
    Available = 0,
    Accepted = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4
}

/// <summary>Type of objective required by a mission contract.</summary>
public enum MissionObjectiveType
{
    DownloadFile = 1,
    UploadFile = 2,
    DeleteFile = 3,
    ExploitSystem = 4,
    WipeLogs = 5,
    PortScan = 6
}

/// <summary>Represents a single objective within a hacker mission contract.</summary>
public sealed record MissionObjective(
    string ObjectiveId,
    string Description,
    MissionObjectiveType Type,
    string TargetHost,
    string TargetPath,
    bool IsCompleted = false);

/// <summary>Represents a simulated mission/contract available on HackMail or DarkNet market.</summary>
public sealed record MissionContract(
    string ContractId,
    string Title,
    string Employer,
    string Description,
    int DifficultyLevel,
    decimal PayoutCredits,
    int ReputationReward,
    string TargetHost,
    IReadOnlyList<MissionObjective> Objectives,
    MissionStatus Status = MissionStatus.Available);

/// <summary>Describes virtual hardware and ISP bandwidth upgrade levels.</summary>
public sealed record VirtualHardwareProfile(
    int CpuTier,
    int RamGb,
    int GpuTier,
    int IspBandwidthMbps,
    decimal MaintenanceCostPerDay);

/// <summary>Describes player economy and reputation stats.</summary>
public sealed record PlayerProfileStats(
    decimal BalanceCredits,
    int ReputationPoints,
    int CompletedMissionsCount,
    int FailedMissionsCount);
