using System.Reflection;
using HackerOs.AppFramework.Abstractions;
using HackerOs.AppFramework.Components;

namespace HackerOs.AppFramework.Registry;

/// <summary>
/// An immutable description of a discovered application. Produced by the
/// <see cref="AppRegistry"/> from a component type decorated with
/// <see cref="AppAttribute"/>.
/// </summary>
public sealed class AppDescriptor
{
    internal AppDescriptor(Type componentType, AppAttribute attribute, AppKind kind)
    {
        ComponentType = componentType;
        Kind = kind;
        Name = attribute.Name;
        Id = string.IsNullOrWhiteSpace(attribute.Id)
            ? componentType.FullName ?? componentType.Name
            : attribute.Id!;
        Description = attribute.Description;
        Icon = attribute.Icon;
        Category = attribute.Category;
        Version = attribute.Version;
        HiddenFromLauncher = attribute.HiddenFromLauncher;
        SortOrder = attribute.SortOrder;
    }

    /// <summary>The concrete Blazor component type that implements the app.</summary>
    public Type ComponentType { get; }

    /// <summary>Whether the app is window- or terminal-based.</summary>
    public AppKind Kind { get; }

    /// <summary>Stable unique identifier.</summary>
    public string Id { get; }

    /// <summary>Human readable name.</summary>
    public string Name { get; }

    /// <summary>Short description / tooltip.</summary>
    public string Description { get; }

    /// <summary>Icon glyph.</summary>
    public string Icon { get; }

    /// <summary>Launcher category.</summary>
    public string Category { get; }

    /// <summary>App version string.</summary>
    public string Version { get; }

    /// <summary>Whether the app is hidden from the launcher UI.</summary>
    public bool HiddenFromLauncher { get; }

    /// <summary>Ordering hint within a category.</summary>
    public int SortOrder { get; }

    /// <summary>
    /// Attempts to build an <see cref="AppDescriptor"/> from a type. Returns
    /// <see langword="null"/> when the type is not a valid, discoverable app.
    /// </summary>
    internal static AppDescriptor? TryCreate(Type type)
    {
        if (type.IsAbstract || !type.IsClass)
        {
            return null;
        }

        var attribute = type.GetCustomAttribute<AppAttribute>(inherit: false);
        if (attribute is null)
        {
            return null;
        }

        AppKind kind;
        if (typeof(TerminalAppBase).IsAssignableFrom(type))
        {
            kind = AppKind.Terminal;
        }
        else if (typeof(WindowAppBase).IsAssignableFrom(type))
        {
            kind = AppKind.Window;
        }
        else
        {
            // Decorated with [App] but not built on a supported base class.
            throw new InvalidOperationException(
                $"Type '{type.FullName}' is decorated with [App] but does not derive from " +
                $"{nameof(WindowAppBase)} or {nameof(TerminalAppBase)}.");
        }

        return new AppDescriptor(type, attribute, kind);
    }
}
