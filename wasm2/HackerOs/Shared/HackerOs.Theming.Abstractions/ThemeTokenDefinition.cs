namespace HackerOs.Theming.Abstractions;

/// <summary>
/// Value grammar of one theme token (ADR 0103 §2.3). Every value read from a user theme file is
/// checked against its token's type before it may reach the page, so a theme stays "data only".
/// </summary>
public enum ThemeTokenType
{
    /// <summary>A CSS color: <c>#rgb</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>, <c>rgb()</c>/<c>hsl()</c> or a named color.</summary>
    Color,

    /// <summary>A CSS length: <c>0</c> or a number with <c>px</c>, <c>rem</c>, <c>em</c>, <c>%</c>, <c>vh</c> or <c>vw</c>.</summary>
    Length,

    /// <summary>A duration (<c>120ms</c>, <c>0.2s</c>), optionally followed by an easing keyword.</summary>
    Duration,

    /// <summary>A comma-separated font family list of quoted names or plain identifiers.</summary>
    FontFamily,

    /// <summary>A plain decimal number (font weight, opacity).</summary>
    Number
}

/// <summary>Editor groups of the theme tokens, in the order the Theme Editor shows them.</summary>
public enum ThemeTokenGroup
{
    /// <summary>Surfaces, text, borders, accent, status and desktop colors.</summary>
    Colors,

    /// <summary>Font families, sizes and weights.</summary>
    Typography,

    /// <summary>Corner radii, control heights and gaps.</summary>
    Spacing,

    /// <summary>Window frame, title bar and window buttons.</summary>
    Windows,

    /// <summary>Taskbar, its buttons and the launcher.</summary>
    Taskbar,

    /// <summary>Transition durations.</summary>
    Animations
}

/// <summary>Describes one CSS custom property a user theme may set (ADR 0103 §2.3).</summary>
/// <param name="Name">CSS custom property name, such as <c>--hos-accent</c>.</param>
/// <param name="Type">Value grammar enforced when a theme file is read.</param>
/// <param name="Group">Editor group.</param>
/// <param name="Label">Short human-readable label.</param>
/// <param name="Description">What the token changes on screen; used by the editor's help and documentation.</param>
/// <param name="Example">A valid sample value.</param>
/// <param name="RgbCompanion">
/// Name of a companion token holding the same color as an <c>r, g, b</c> triplet (for
/// <c>rgb(var(--x-rgb) / 20%)</c> mixes), derived automatically when this color is set; otherwise
/// <see langword="null"/>.
/// </param>
public sealed record ThemeTokenDefinition(
    string Name,
    ThemeTokenType Type,
    ThemeTokenGroup Group,
    string Label,
    string Description,
    string Example,
    string? RgbCompanion = null);
