using HackerOs.MobileShell.Blazor;
using HackerOs.Windowing.Abstractions;
using HackerOs.Windowing.Core;

namespace HackerOs.Platform.Blazor.Shell;

/// <summary>
/// Adapts <see cref="WindowRuntime"/> to <see cref="IMobileNavigationCommands"/> for
/// <see cref="MobileShell"/> (<c>MOB-009</c>/<c>MOB-010</c> scaffold). <see cref="RequestBack"/> and
/// <see cref="RequestRecent"/> are documented no-ops until <c>MOB-011</c> (navigation stack, Recent
/// surface) and <c>MOB-012</c> (<c>IAppBackHandler</c>) exist to give them real semantics.
/// </summary>
public sealed class MobileNavigationCommandsAdapter(WindowRuntime windowRuntime) : IMobileNavigationCommands
{
    /// <inheritdoc />
    public void RequestBack()
    {
        // No-op until MOB-011/MOB-012 exist. Per plan §7.3's own ordered sequence, "at Home with no
        // history, do nothing and produce a discreet accessible no-op" is itself a valid terminal
        // outcome — this is that outcome for every request until the earlier steps exist to
        // pre-empt it.
    }

    /// <inheritdoc />
    public void RequestHome()
    {
        // Hides the active surface without terminating its process, per plan §7.4. Reuses the same
        // Minimize mechanism SingleSurfacePresentationPolicy already uses to hide background
        // windows; a subsequent app-icon tap restores the same instance via the ordinary Restore
        // command (MOB-009 does not add a distinct "Mobile home screen" surface yet).
        WindowId? primaryId = SingleSurfacePresentationPolicy.SelectPrimary(windowRuntime.Windows);
        if (primaryId is WindowId id)
        {
            windowRuntime.Apply(new MinimizeWindowCommand(id));
        }
    }

    /// <inheritdoc />
    public void RequestRecent()
    {
        // No-op until MOB-011 (Recent surface) exists.
    }
}
