using HackerOs.App.Abstractions;
using HackerOs.Platform.Blazor.Windows;
using HackerOs.Platform.Core.Lifecycle;
using HackerOs.Platform.Core.Shell;
using HackerOs.Simulation.Abstractions.Processes;
using HackerOs.Windowing.Core;
using HackerOs.Windowing.Abstractions;

namespace HackerOs.Platform.Blazor.Shell;

/// <summary>
/// Drives the controlled platform-switch sequence, per docs/mobile-interface-platform-plan.md §6.3
/// (<c>MOB-008</c>): confirm every open window's dirty state, stop each window's owning instance
/// with the typed <see cref="ProcessExitReason.PlatformChanged"/> reason and remove its window, then
/// persist the new preference through <see cref="UiPlatformPreferenceService"/>. That service's
/// <see cref="UiPlatformPreferenceService.Changed"/> event is what actually swaps the rendered shell
/// (see <c>App.razor</c>) — this coordinator only clears the way and commits the choice.
/// </summary>
/// <remarks>
/// Deliberately out of scope for this slice: restarting <see cref="AppKind.Service"/> instances
/// whose contract requires it (§6.3 step 5's "ou les redémarrer si leur contrat l'exige") — no
/// service in this codebase yet declares such a requirement, so every running service is left
/// untouched. Re-resolving the launcher/file-association/intent candidate lists against the new
/// active platform (§6.3 step 8) is also deferred: no shipped app manifest declares a Mobile-only
/// entry point yet (only the <c>HackerOs.Samples.PlatformApp</c> demo, which is desktop+mobile
/// shared), so every app that can launch today launches identically regardless of active platform.
/// </remarks>
public sealed class PlatformShellSwitchCoordinator(
    WindowRuntime windowRuntime,
    AppLifecycleOrchestrator orchestrator,
    WindowCloseGuardRegistry closeGuards,
    UiPlatformPreferenceService platformPreference)
{
    /// <summary>
    /// Requests an explicit platform choice. When it would change the active platform, every open
    /// window is confirmed and then stopped before the choice is persisted.
    /// </summary>
    /// <returns><see langword="false"/> when a dirty window rejected the confirmation and nothing was changed.</returns>
    public async Task<bool> RequestExplicitAsync(AppPlatformId targetPlatform, CancellationToken cancellationToken = default)
    {
        if (platformPreference.Current.ActivePlatform == targetPlatform)
        {
            // No window disruption needed for a same-platform re-selection (e.g. Auto -> explicit
            // Desktop while already active on Desktop); still persist the explicit override itself.
            await platformPreference.SetExplicitAsync(targetPlatform, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (!await ClearWindowsAsideAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await platformPreference.SetExplicitAsync(targetPlatform, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Requests reverting to automatic platform detection. Every open window is confirmed and
    /// stopped first, since the resulting active platform is not knowable in advance without
    /// re-running environment detection.
    /// </summary>
    /// <returns><see langword="false"/> when a dirty window rejected the confirmation and nothing was changed.</returns>
    public async Task<bool> RequestAutoAsync(CancellationToken cancellationToken = default)
    {
        if (platformPreference.Current.SelectionSource == UiPlatformSelectionSource.Auto)
        {
            return true;
        }

        if (!await ClearWindowsAsideAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await platformPreference.ClearToAutoAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> ClearWindowsAsideAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<WindowRuntimeState> windows = windowRuntime.Windows;
        foreach (WindowRuntimeState window in windows)
        {
            if (!await closeGuards.ConfirmCloseAsync(window.Id, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        foreach (WindowRuntimeState window in windows)
        {
            await orchestrator.StopAsync(
                AppInstanceId.FromGuid(window.OwnerInstanceId.Value),
                ProcessExitReason.PlatformChanged).ConfigureAwait(false);
            windowRuntime.Apply(new ForceWindowCloseCommand(window.Id));
        }

        return true;
    }
}
