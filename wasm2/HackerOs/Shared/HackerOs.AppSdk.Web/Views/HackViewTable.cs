using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace HackerOs.AppSdk.Web.Views;

/// <summary>
/// One view of a site: a Razor component under <c>Views/</c>, by its path without folder nor
/// extension (<c>Home/Index</c> for <c>Views/Home/Index.razor</c>, ADR 0083 §2).
/// </summary>
public sealed class HackViewDescriptor
{
    /// <summary>Initializes a view.</summary>
    /// <param name="path">Path under <c>Views/</c>, without extension (<c>Shared/_Layout</c>).</param>
    /// <param name="viewType">The component the Razor compiler generated for the file.</param>
    public HackViewDescriptor(
        string path,
        [DynamicallyAccessedMembers(HackViewTypes.Component)] Type viewType)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(viewType);
        if (!typeof(IComponent).IsAssignableFrom(viewType))
        {
            throw new ArgumentException($"The view '{path}' ({viewType.FullName}) is not a Razor component.", nameof(viewType));
        }

        Path = path;
        ViewType = viewType;
    }

    /// <summary>Gets the path under <c>Views/</c>, without extension.</summary>
    public string Path { get; }

    /// <summary>Gets the component type.</summary>
    [DynamicallyAccessedMembers(HackViewTypes.Component)]
    public Type ViewType { get; }

    /// <inheritdoc />
    public override string ToString() => $"/Views/{Path}.razor";
}

/// <summary>
/// The views of a site, written at build by the route generator from the <c>.razor</c> files under
/// <c>Views/</c> (ADR 0083 §2): the view engine looks a name up here, no type is searched by
/// reflection at run time.
/// </summary>
public sealed class HackViewTable
{
    private readonly Dictionary<string, HackViewDescriptor> _views;

    /// <summary>Initializes a table.</summary>
    /// <param name="views">The views; paths compare without case.</param>
    public HackViewTable(IEnumerable<HackViewDescriptor> views)
    {
        ArgumentNullException.ThrowIfNull(views);
        _views = new Dictionary<string, HackViewDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (HackViewDescriptor view in views)
        {
            _views[view.Path] = view;
        }

        Views = [.. _views.Values];
    }

    /// <summary>A table with no view (a site without <c>Views/</c>).</summary>
    public static HackViewTable Empty { get; } = new([]);

    /// <summary>Gets every view.</summary>
    public IReadOnlyList<HackViewDescriptor> Views { get; }

    /// <summary>Gets the view at <paramref name="path"/> (<c>Home/Index</c>), or <see langword="null"/>.</summary>
    /// <param name="path">Path under <c>Views/</c>, without extension.</param>
    public HackViewDescriptor? Find(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _views.GetValueOrDefault(path);
    }
}

/// <summary>Trimming annotations shared by the view types.</summary>
internal static class HackViewTypes
{
    /// <summary>What <c>RenderTreeBuilder.OpenComponent(int, Type)</c> requires of a component type.</summary>
    public const DynamicallyAccessedMemberTypes Component = DynamicallyAccessedMemberTypes.All;
}
