namespace HackerOs.AppFramework.Abstractions;

/// <summary>
/// Identifies the base surface a registered application is built on.
/// This lets the ecosystem shell reason about an application without needing
/// to know its concrete component type.
/// </summary>
public enum AppKind
{
    /// <summary>
    /// A classic windowed application that derives from
    /// <see cref="Components.WindowAppBase"/>.
    /// </summary>
    Window,

    /// <summary>
    /// A text/console application that derives from
    /// <see cref="Components.TerminalAppBase"/>.
    /// </summary>
    Terminal
}
