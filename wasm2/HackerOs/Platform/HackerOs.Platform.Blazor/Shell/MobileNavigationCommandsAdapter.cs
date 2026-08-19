using HackerOs.AppSdk.Blazor;
using HackerOs.MobileShell.Blazor;
using HackerOs.Platform.Blazor.Windows;
using HackerOs.Windowing.Abstractions;
using HackerOs.Windowing.Core;

namespace HackerOs.Platform.Blazor.Shell;

/// <summary>
/// Adapts <see cref="WindowRuntime"/> to <see cref="IMobileNavigationCommands"/> for
/// <see cref="MobileShell"/> (<c>MOB-009</c>/<c>MOB-010</c> scaffold). <see cref="RequestRecent"/>
/// is a documented no-op until <c>MOB-011</c> (navigation stack, Recent surface) exists to give it
/// real semantics; <see cref="RequestBack"/> now covers step 2 of plan §7.3's ordered sequence
/// (<c>MOB-012</c>'s <see cref="IAppBackHandler"/>) — steps 3-5 still await <c>MOB-011</c>.
/// </summary>
public sealed class MobileNavigationCommandsAdapter(WindowRuntime windowRuntime, AppBackHandlerRegistry backHandlers) : IMobileNavigationCommands
{
    /// <inheritdoc />
    public async void RequestBack()
    {
        // async void is deliberate here: IMobileNavigationCommands.RequestBack is a fire-and-forget
        // UI command dispatch (the triangle button's onclick), matching MobileSystemNavigationBar's
        // existing synchronous IMobileNavigationCommands contract.
        WindowId? primaryId = SingleSurfacePresentationPolicy.SelectPrimary(windowRuntime.Windows);
        if (primaryId is not WindowId id)
        {
            return;
        }

        // Only step 2 of plan §7.3's ordered Back sequence — the app's declared IAppBackHandler.
        // Steps 3-5 (navigation stack, close-and-go-Home fallback) require MOB-011, out of scope
        // here; a NotHandled/no-handler result is a valid no-op per §7.3 step 5.
        await backHandlers.NavigateBackAsync(id, new AppBackRequest(AppBackSource.SystemNavigationBar));
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
