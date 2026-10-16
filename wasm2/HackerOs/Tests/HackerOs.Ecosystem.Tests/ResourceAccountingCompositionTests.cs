using HackerOs.Platform.Core.Processes.Accounting;
using HackerOs.Platform.ResourceAccounting;
using HackerOs.Simulation.Abstractions.Processes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HackerOs.Ecosystem.Tests;

/// <summary>
/// ADR 0066 at service composition: the four build/setting combinations of each measurement, the
/// hook installed only when one is active, and the host settings shipped with each host.
/// </summary>
public sealed class ResourceAccountingCompositionTests
{
    public static TheoryData<bool, bool, bool, bool> AllCombinations()
    {
        TheoryData<bool, bool, bool, bool> data = [];
        foreach (bool buildCpu in new[] { false, true })
        foreach (bool buildMemory in new[] { false, true })
        foreach (bool enabledCpu in new[] { false, true })
        foreach (bool enabledMemory in new[] { false, true })
        {
            data.Add(buildCpu, buildMemory, enabledCpu, enabledMemory);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void The_hook_is_installed_only_when_a_measurement_is_active(bool buildCpu, bool buildMemory, bool enabledCpu, bool enabledMemory)
    {
        ServiceCollection services = new();
        List<IExecutionContextSwitchObserver> installed = [];

        ResourceAccountingPlan plan = services.AddHackerOsResourceAccounting(
            ResourceAccountingSettings.Default with { CpuEnabled = enabledCpu, MemoryEnabled = enabledMemory },
            new ResourceAccountingBuildSupport(buildCpu, buildMemory),
            observer =>
            {
                installed.Add(observer);
                return new NoopDisposable();
            });

        bool anyActive = (buildCpu && enabledCpu) || (buildMemory && enabledMemory);
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Same(plan, provider.GetRequiredService<ResourceAccountingPlan>());
        Assert.Equal(anyActive ? 1 : 0, installed.Count);
        Assert.Equal(anyActive, provider.GetService<ContextSwitchDispatcher>() is not null);
        if (anyActive)
        {
            Assert.Same(provider.GetRequiredService<ContextSwitchDispatcher>(), installed[0]);
        }

        // Nothing measures yet (chantiers 5-cpu-accounting / 5-memory-accounting): every value
        // stays simulated whatever the combination.
        Assert.False(provider.GetRequiredService<ICpuTimeAccountant>().IsMeasuring);
        Assert.False(provider.GetRequiredService<IMemoryAccountant>().IsMeasuring);
        Assert.False(ContextSwitchHook.IsInstalled);
    }

    [Fact]
    public void Everything_off_installs_no_hook_through_the_real_path()
    {
        ServiceCollection services = new();
        IConfiguration configuration = Configuration(new()
        {
            ["HackerOs:ResourceAccounting:Cpu:Enabled"] = "false",
            ["HackerOs:ResourceAccounting:Memory:Enabled"] = "false"
        });

        ResourceAccountingPlan plan = services.AddHackerOsResourceAccounting(configuration);

        Assert.False(plan.RequiresContextSwitchHook);
        Assert.False(ContextSwitchHook.IsInstalled);
        Assert.DoesNotContain(services, static descriptor => descriptor.ServiceType == typeof(ContextSwitchDispatcher));
        Assert.DoesNotContain(services, static descriptor => descriptor.ServiceType == typeof(ResourceAccountingHookInstallation));
    }

    [Fact]
    public void A_host_without_accounting_gets_the_null_accountants_and_the_disabled_plan()
    {
        ServiceCollection services = new();
        services.AddHackerOsEcosystem();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(ResourceAccountingPlan.Disabled, provider.GetRequiredService<ResourceAccountingPlan>());
        Assert.Same(NullCpuTimeAccountant.Instance, provider.GetRequiredService<ICpuTimeAccountant>());
        Assert.Same(NullMemoryAccountant.Instance, provider.GetRequiredService<IMemoryAccountant>());
    }

    [Fact]
    public void Composition_before_the_ecosystem_keeps_its_plan()
    {
        ServiceCollection services = new();
        ResourceAccountingPlan plan = services.AddHackerOsResourceAccounting(
            ResourceAccountingSettings.Default with { CpuEnabled = false, MemoryEnabled = false },
            new ResourceAccountingBuildSupport(true, true),
            hookInstaller: null);
        services.AddHackerOsEcosystem();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(plan, provider.GetRequiredService<ResourceAccountingPlan>());
    }

    [Fact]
    public void A_missing_section_enables_both_measurements()
    {
        ResourceAccountingSettings settings = ResourceAccountingServiceCollectionExtensions.ReadSettings(Configuration([]));

        Assert.True(settings.CpuEnabled);
        Assert.True(settings.MemoryEnabled);
        Assert.Equal(TimeSpan.FromSeconds(5), settings.UnresponsiveThreshold);
        Assert.Empty(settings.Warnings);
    }

    [Fact]
    public void The_section_is_read_once_with_its_values()
    {
        ResourceAccountingSettings settings = ResourceAccountingServiceCollectionExtensions.ReadSettings(Configuration(new()
        {
            ["HackerOs:ResourceAccounting:Cpu:Enabled"] = "false",
            ["HackerOs:ResourceAccounting:Cpu:UnresponsiveThresholdMs"] = "1500",
            ["HackerOs:ResourceAccounting:Memory:Enabled"] = "True"
        }));

        Assert.False(settings.CpuEnabled);
        Assert.True(settings.MemoryEnabled);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), settings.UnresponsiveThreshold);
    }

    [Fact]
    public void Unreadable_values_fall_back_to_their_defaults_with_a_warning()
    {
        ResourceAccountingSettings settings = ResourceAccountingServiceCollectionExtensions.ReadSettings(Configuration(new()
        {
            ["HackerOs:ResourceAccounting:Cpu:Enabled"] = "maybe",
            ["HackerOs:ResourceAccounting:Cpu:UnresponsiveThresholdMs"] = "-4"
        }));

        Assert.True(settings.CpuEnabled);
        Assert.Equal(ResourceAccountingSettings.DefaultUnresponsiveThreshold, settings.UnresponsiveThreshold);
        Assert.Equal(2, settings.Warnings.Count);
        Assert.Contains(settings.Warnings, static line => line.Contains("Cpu:Enabled='maybe'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("OS/HackerOs.Ecosystem/wwwroot/appsettings.json", true)]
    [InlineData("test/test/appsettings.json", true)]
    [InlineData("Server/HackerOs.Server/appsettings.json", false)]
    public void Each_host_ships_its_section(string relativePath, bool enabled)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(SolutionDirectory(), relativePath), optional: false)
            .Build();

        ResourceAccountingSettings settings = ResourceAccountingServiceCollectionExtensions.ReadSettings(configuration);

        Assert.NotNull(configuration.GetSection(ResourceAccountingServiceCollectionExtensions.SectionName).GetChildren().FirstOrDefault());
        Assert.Equal(enabled, settings.CpuEnabled);
        Assert.Equal(enabled, settings.MemoryEnabled);
        Assert.Equal(ResourceAccountingSettings.DefaultUnresponsiveThreshold, settings.UnresponsiveThreshold);
        Assert.Empty(settings.Warnings);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static string SolutionDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HackerOs.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("HackerOs.sln not found above the test output.");
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
