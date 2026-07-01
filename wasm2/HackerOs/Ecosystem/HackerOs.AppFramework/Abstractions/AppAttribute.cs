namespace HackerOs.AppFramework.Abstractions;

/// <summary>
/// Marks a Blazor component as a self-registering application in the HackerOS
/// developer ecosystem.
/// </summary>
/// <remarks>
/// <para>
/// Any component that derives from <see cref="Components.WindowAppBase"/> or
/// <see cref="Components.TerminalAppBase"/> and is decorated with this attribute
/// is automatically discovered by the <see cref="Registry.AppRegistry"/> at
/// start-up. The registry adds it to the application list so the user can launch
/// it from the start menu and see it on the taskbar &mdash; no manual wiring in
/// <c>Program.cs</c> is required.
/// </para>
/// <para>
/// This is the single extension point of the framework: a developer drops a new
/// component into the project, decorates it with <c>[App]</c>, and it becomes a
/// first-class citizen of the operating system.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AppAttribute : Attribute
{
    /// <summary>
    /// Creates a new application registration.
    /// </summary>
    /// <param name="name">The human readable name shown in the launcher and taskbar.</param>
    public AppAttribute(string name)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Application name is required.", nameof(name))
            : name;
    }

    /// <summary>
    /// The human readable name shown in the launcher, window title bar and taskbar.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// A stable identifier for the application. When omitted the registry derives
    /// one from the component type's full name, guaranteeing uniqueness.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// A short description surfaced as a tooltip in the launcher.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// An icon for the application. Any short string works &mdash; an emoji glyph
    /// keeps the framework dependency free while still looking thematic.
    /// </summary>
    public string Icon { get; set; } = "\U0001F5D4"; // 🗔 window glyph

    /// <summary>
    /// A grouping category used to organise the launcher (for example
    /// <c>System</c>, <c>Development</c> or <c>Games</c>).
    /// </summary>
    public string Category { get; set; } = "Applications";

    /// <summary>
    /// The application version, useful for future package management.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// When <see langword="true"/> the app is hidden from the launcher but can
    /// still be launched programmatically (handy for background/helper apps).
    /// </summary>
    public bool HiddenFromLauncher { get; set; }

    /// <summary>
    /// Ordering hint within a category (lower values appear first).
    /// </summary>
    public int SortOrder { get; set; } = 100;
}
