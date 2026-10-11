using HackerOs.App.Abstractions;
using HackerOs.AppSdk;
using HackerOs.AppSdk.Shell;
using HackerOs.Platform.Core.Shell;
using HackerOs.Simulation.Abstractions.Gateways;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Execution;

/// <summary>
/// Trusted <see cref="IAppShellHostGateway"/> (ADR 0056, section 7): checks <c>shell.host</c>,
/// then asks <see cref="ShellHostService"/> to start the shell as a child of the app's process.
/// </summary>
internal sealed class AppShellHostGateway(
    Func<ShellHostService> serviceProvider,
    ICapabilityChecker capabilities,
    AuthenticatedPrincipal principal,
    Func<IAppExecutionContext> caller,
    Func<ProcessRecord, IAppExecutionContext> createShellContext) : IAppShellHostGateway
{
    /// <inheritdoc />
    public Task<IHackShell> StartAsync(HackShellOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        capabilities.Require(AppCapabilities.ShellHost);
        return serviceProvider().StartAsync(caller(), principal, createShellContext, options, cancellationToken);
    }
}
