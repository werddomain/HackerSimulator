using System.Globalization;
using HackerOs.Platform.Core.Processes.Accounting;
using HackerOs.Simulation.Abstractions.Processes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
#if HACKEROS_RESOURCE_ACCOUNTING
using HackerOs.Platform.ResourceAccounting;
#endif

namespace HackerOs.Ecosystem;

/// <summary>
/// Composes the optional resource accounting of ADR 0066: reads the host's
/// <c>HackerOs:ResourceAccounting</c> section once, combines it with what the build compiled in,
/// and installs the common context-switch hook only when at least one measurement is active.
/// </summary>
/// <remarks>
/// Each host calls <see cref="AddHackerOsResourceAccounting(IServiceCollection, IConfiguration?)"/>
/// before <see cref="EcosystemServiceCollectionExtensions.AddHackerOsEcosystem"/>. A host (or a test)
/// that never calls it gets the null accountants and <see cref="ResourceAccountingPlan.Disabled"/>:
/// exactly the simulated behavior of phases 1 to 4.
/// </remarks>
public static class ResourceAccountingServiceCollectionExtensions
{
    /// <summary>Configuration section read by <see cref="ReadSettings"/>.</summary>
    public const string SectionName = "HackerOs:ResourceAccounting";

    /// <summary>
    /// Gets the measurements compiled into this host: none when the build left
    /// <c>HackerOs.Platform.ResourceAccounting</c> out, otherwise its runtime feature switches.
    /// </summary>
    public static ResourceAccountingBuildSupport CompiledSupport =>
#if HACKEROS_RESOURCE_ACCOUNTING
        new(ResourceAccountingFeatures.IsCpuAccountingSupported, ResourceAccountingFeatures.IsMemoryAccountingSupported);
#else
        ResourceAccountingBuildSupport.None;
#endif

    /// <summary>
    /// Reads <c>HackerOs:ResourceAccounting</c> from the host configuration and composes the
    /// accounting it asks for, within what the build allows.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">The host configuration (<c>appsettings.json</c>); <see langword="null"/> reads as an absent section.</param>
    /// <returns>The resolved plan, also registered as a singleton.</returns>
    public static ResourceAccountingPlan AddHackerOsResourceAccounting(this IServiceCollection services, IConfiguration? configuration) =>
        services.AddHackerOsResourceAccounting(ReadSettings(configuration), CompiledSupport, hookInstaller: null);

    /// <summary>
    /// Composes the accounting for explicit settings and build support. Tests use it to cover every
    /// combination without touching the process-wide hook.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="settings">What <c>appsettings.json</c> asks.</param>
    /// <param name="build">What the build compiled in.</param>
    /// <param name="hookInstaller">
    /// Installs the context-switch hook with the given observer and returns its installation;
    /// <see langword="null"/> uses the real hook on <c>HackExecutionContext</c>. Never called when no
    /// measurement is active.
    /// </param>
    /// <returns>The resolved plan, also registered as a singleton.</returns>
    /// <exception cref="InvalidOperationException">A measurement is active but this assembly was built without the accounting project.</exception>
    public static ResourceAccountingPlan AddHackerOsResourceAccounting(
        this IServiceCollection services,
        ResourceAccountingSettings settings,
        ResourceAccountingBuildSupport build,
        Func<IExecutionContextSwitchObserver, IDisposable>? hookInstaller)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        ResourceAccountingPlan plan = ResourceAccountingPlan.Resolve(build, settings);
        services.AddSingleton(plan);

        if (plan.RequiresContextSwitchHook)
        {
#if HACKEROS_RESOURCE_ACCOUNTING
            // Installed now, at composition: no app code has run, so no scope is open. The CPU and
            // memory measurements (ADR 0067, 0068) register themselves on this dispatcher.
            ContextSwitchDispatcher dispatcher = new();
            IDisposable installation = (hookInstaller ?? ContextSwitchHook.Install)(dispatcher);
            services.AddSingleton(dispatcher);
            services.AddSingleton(new ResourceAccountingHookInstallation(installation));
#else
            throw new InvalidOperationException(
                "A resource measurement is active but HackerOs.Platform.ResourceAccounting is not built into this host.");
#endif
        }

        // No measurement is implemented yet (chantiers 5-cpu-accounting, 5-memory-accounting): an
        // active measurement still reads the simulator, and /proc says "simulated".
        services.TryAddSingleton<ICpuTimeAccountant>(NullCpuTimeAccountant.Instance);
        services.TryAddSingleton<IMemoryAccountant>(NullMemoryAccountant.Instance);
        return plan;
    }

    /// <summary>
    /// Reads the <c>HackerOs:ResourceAccounting</c> section. A missing value takes its default
    /// (measurements on, five-second threshold); an unreadable one also does, with a warning line.
    /// </summary>
    /// <param name="configuration">The host configuration, or <see langword="null"/>.</param>
    /// <returns>The settings.</returns>
    public static ResourceAccountingSettings ReadSettings(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return ResourceAccountingSettings.Default;
        }

        IConfigurationSection section = configuration.GetSection(SectionName);
        List<string> warnings = [];
        bool cpu = ReadBoolean(section, "Cpu:Enabled", warnings);
        bool memory = ReadBoolean(section, "Memory:Enabled", warnings);
        TimeSpan threshold = ResourceAccountingSettings.DefaultUnresponsiveThreshold;
        string? thresholdText = section["Cpu:UnresponsiveThresholdMs"];
        if (thresholdText is not null)
        {
            if (int.TryParse(thresholdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int milliseconds) && milliseconds > 0)
            {
                threshold = TimeSpan.FromMilliseconds(milliseconds);
            }
            else
            {
                warnings.Add($"resource accounting: {SectionName}:Cpu:UnresponsiveThresholdMs='{thresholdText}' is not a positive integer; using {threshold.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        return new ResourceAccountingSettings(cpu, memory, threshold, warnings);
    }

    private static bool ReadBoolean(IConfigurationSection section, string key, List<string> warnings)
    {
        string? text = section[key];
        if (text is null)
        {
            return true;
        }

        if (bool.TryParse(text, out bool value))
        {
            return value;
        }

        warnings.Add($"resource accounting: {SectionName}:{key}='{text}' is not true or false; using true");
        return true;
    }
}

/// <summary>
/// The installed context-switch hook, kept alive with the service provider. The OS never removes
/// it: like a kernel's scheduler hook, it lasts until the page reloads.
/// </summary>
/// <param name="Installation">The installation returned by the hook installer.</param>
public sealed record ResourceAccountingHookInstallation(IDisposable Installation);
