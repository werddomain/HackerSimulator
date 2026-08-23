using HackerOs.Windowing.Abstractions;

namespace HackerOs.MobileShell.Blazor;

/// <summary>
/// One entry in the Mobile Recent surface (plan §7.5, <c>MOB-011</c>): title and icon only — there
/// is no thumbnail/preview-capture mechanism anywhere in the codebase yet, so a safe preview is
/// achieved by omission rather than by an active protection flag.
/// </summary>
public sealed record MobileRecentItem(WindowId Id, string Title, string? IconAssetPath, bool IsFocused);

/// <summary>
/// Host-supplied labels for the Recent surface's accessible text, mirroring
/// <see cref="MobileSystemNavigationBarOptions"/> so a host isn't forced to accept hardcoded
/// English strings.
/// </summary>
public sealed record MobileRecentSurfaceOptions
{
    /// <summary>Gets the accessible dialog title.</summary>
    public string Title { get; init; } = "Recent";

    /// <summary>Gets the message shown when no instance is open.</summary>
    public string EmptyMessage { get; init; } = "No recent apps";

    /// <summary>Gets the prefix combined with an item's title for its close button's accessible name.</summary>
    public string CloseLabelPrefix { get; init; } = "Close";
}
