namespace HackerOs.MobileShell.Blazor;

/// <summary>
/// Dispatches the three Mobile system-navigation actions (Triangle/Back, Circle/Home,
/// Square/Recent, per docs/mobile-interface-platform-plan.md §7.2/§7.3/§7.4/§7.5). The host owns
/// how each is fulfilled — e.g. Back may consult a dialog stack, an app's declared
/// <c>IAppBackHandler</c> (<c>MOB-012</c>, not yet implemented), or a navigation stack
/// (<c>MOB-011</c>, not yet implemented) before falling back to Home. This bar only dispatches the
/// request; it never decides what Back/Home/Recent mean.
/// </summary>
public interface IMobileNavigationCommands
{
    /// <summary>Requests Back navigation, per the ordered semantics in plan §7.3.</summary>
    void RequestBack();

    /// <summary>Requests a return to the Mobile home surface, per plan §7.4.</summary>
    void RequestHome();

    /// <summary>Requests the Recent-surfaces view, per plan §7.5.</summary>
    void RequestRecent();
}

/// <summary>
/// Host-supplied labels for the navigation bar's accessible button names, so a host isn't forced
/// to accept hardcoded English strings (mirrors <c>HackerOs.Taskbar.Blazor</c>'s <c>TaskbarOptions</c>).
/// </summary>
public sealed record MobileSystemNavigationBarOptions
{
    /// <summary>Gets the accessible label for the Back (triangle) button.</summary>
    public string BackLabel { get; init; } = "Back";

    /// <summary>Gets the accessible label for the Home (circle) button.</summary>
    public string HomeLabel { get; init; } = "Home";

    /// <summary>Gets the accessible label for the Recent (square) button.</summary>
    public string RecentLabel { get; init; } = "Recent";
}
