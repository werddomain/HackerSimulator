using HackerOs.Game.Abstractions;
using HackerOs.Game.Core;
using Xunit;

namespace HackerOs.Game.Tests;

/// <summary>
/// Unit test suite for Phase 4 Wave 6 (Gameplay Domains).
/// Validates InMemoryGameDomainGateway contract acceptance, objective progress submission,
/// economy payouts, hardware upgrades, and NullGameDomainGateway fallback per ADR 0023.
/// </summary>
public sealed class GameDomainTests
{
    [Fact]
    public async Task NullGameDomainGateway_ReturnsDisabledState()
    {
        IGameDomainGateway nullGateway = new NullGameDomainGateway();
        Assert.False(nullGateway.IsAvailable);

        var stats = await nullGateway.GetPlayerStatsAsync();
        Assert.Equal(0, stats.BalanceCredits);

        var contracts = await nullGateway.GetAvailableContractsAsync();
        Assert.Empty(contracts);
    }

    [Fact]
    public async Task InMemoryGameDomainGateway_SeedsContracts_And_HandlesAcceptance()
    {
        IGameDomainGateway gateway = new InMemoryGameDomainGateway();
        Assert.True(gateway.IsAvailable);

        var contracts = await gateway.GetAvailableContractsAsync();
        Assert.NotEmpty(contracts);

        var first = contracts[0];
        bool accepted = await gateway.AcceptContractAsync(first.ContractId);
        Assert.True(accepted);

        var active = await gateway.GetActiveContractAsync();
        Assert.NotNull(active);
        Assert.Equal(first.ContractId, active.ContractId);
        Assert.Equal(MissionStatus.InProgress, active.Status);
    }

    [Fact]
    public async Task InMemoryGameDomainGateway_CompletesObjectives_And_PaysOutCredits()
    {
        IGameDomainGateway gateway = new InMemoryGameDomainGateway();
        var initialStats = await gateway.GetPlayerStatsAsync();

        var contracts = await gateway.GetAvailableContractsAsync();
        var target = contracts[0];
        await gateway.AcceptContractAsync(target.ContractId);

        foreach (var obj in target.Objectives)
        {
            bool progress = await gateway.SubmitProgressAsync(obj.ObjectiveId);
            Assert.True(progress);
        }

        var active = await gateway.GetActiveContractAsync();
        Assert.Null(active); // Cleared upon completion

        var newStats = await gateway.GetPlayerStatsAsync();
        Assert.Equal(initialStats.BalanceCredits + target.PayoutCredits, newStats.BalanceCredits);
        Assert.Equal(initialStats.ReputationPoints + target.ReputationReward, newStats.ReputationPoints);
        Assert.Equal(1, newStats.CompletedMissionsCount);
    }

    [Fact]
    public async Task InMemoryGameDomainGateway_HandlesHardwareUpgrades()
    {
        IGameDomainGateway gateway = new InMemoryGameDomainGateway();
        var initialHw = await gateway.GetHardwareProfileAsync();

        bool upgraded = await gateway.PurchaseHardwareUpgradeAsync("ram", 1); // 1 * 8 = 8GB RAM, cost 300 credits
        Assert.True(upgraded);

        var newHw = await gateway.GetHardwareProfileAsync();
        Assert.Equal(8, newHw.RamGb);

        var newStats = await gateway.GetPlayerStatsAsync();
        Assert.Equal(500.00m - 300.00m, newStats.BalanceCredits); // 500 - 300 = 200 credits remaining
    }
}
