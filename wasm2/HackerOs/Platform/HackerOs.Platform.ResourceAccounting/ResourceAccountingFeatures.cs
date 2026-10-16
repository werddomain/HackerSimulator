using System.Diagnostics.CodeAnalysis;

namespace HackerOs.Platform.ResourceAccounting;

/// <summary>
/// The build-time switches of ADR 0066, section 2, as seen at run time. Each MSBuild property
/// (<c>HackerOsCpuAccounting</c>, <c>HackerOsMemoryAccounting</c>) becomes a
/// <c>RuntimeHostConfigurationOption</c> through <c>build/HackerOs.ResourceAccounting.targets</c>.
/// </summary>
/// <remarks>
/// The guards are marked <see cref="FeatureSwitchDefinitionAttribute"/>: hosts publish untrimmed
/// today, but the day trimming is enabled, the code behind a switched-off measurement is removed
/// even while the other one keeps this assembly in the binary. A switch that is absent (unit tests,
/// a host that does not import the targets) reads as <see langword="true"/>: the code is present.
/// </remarks>
public static class ResourceAccountingFeatures
{
    /// <summary>Runtime configuration name of the CPU switch.</summary>
    public const string CpuSwitchName = "HackerOs.ResourceAccounting.Cpu.IsSupported";

    /// <summary>Runtime configuration name of the memory switch.</summary>
    public const string MemorySwitchName = "HackerOs.ResourceAccounting.Memory.IsSupported";

    /// <summary>Gets whether the CPU measurement (ADR 0067) is compiled into this host.</summary>
    [FeatureSwitchDefinition(CpuSwitchName)]
    public static bool IsCpuAccountingSupported =>
        !AppContext.TryGetSwitch(CpuSwitchName, out bool supported) || supported;

    /// <summary>Gets whether the memory measurement (ADR 0068) is compiled into this host.</summary>
    [FeatureSwitchDefinition(MemorySwitchName)]
    public static bool IsMemoryAccountingSupported =>
        !AppContext.TryGetSwitch(MemorySwitchName, out bool supported) || supported;
}
