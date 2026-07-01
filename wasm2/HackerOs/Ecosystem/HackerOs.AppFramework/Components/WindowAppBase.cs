using System.Reflection;
using BlazorWindowManager.Components;
using HackerOs.AppFramework.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace HackerOs.AppFramework.Components;

/// <summary>
/// Base class for windowed applications in the ecosystem.
/// </summary>
/// <remarks>
/// <para>
/// Deriving from this class (and decorating the component with
/// <see cref="AppAttribute"/>) is all that is required to publish a new windowed
/// application. The base wires up the window title and icon from the attribute so
/// the taskbar and title bar are populated automatically.
/// </para>
/// <para>
/// A derived <c>.razor</c> component should place its markup inside a
/// <see cref="WindowContent"/> element bound to <c>this</c>:
/// <code>
/// @inherits WindowAppBase
/// &lt;WindowContent Window="this"&gt;
///     &lt;!-- your UI --&gt;
/// &lt;/WindowContent&gt;
/// </code>
/// </para>
/// </remarks>
public abstract class WindowAppBase : WindowBase
{
    /// <summary>The application metadata declared via <see cref="AppAttribute"/>.</summary>
    protected AppAttribute? AppInfo { get; private set; }

    /// <summary>The icon glyph declared for this application.</summary>
    protected string AppIcon => AppInfo?.Icon ?? "\U0001F5D4";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        AppInfo = GetType().GetCustomAttribute<AppAttribute>(inherit: false);

        if (AppInfo is not null)
        {
            // Only override the default title; respect a title supplied explicitly
            // via window parameters.
            if (string.IsNullOrWhiteSpace(Title) || Title == "Window")
            {
                Title = AppInfo.Name;
            }

            Icon ??= BuildIconFragment(AppInfo.Icon);
        }

        base.OnInitialized();
    }

    private static RenderFragment BuildIconFragment(string glyph) => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "app-icon-glyph");
        builder.AddContent(2, glyph);
        builder.CloseElement();
    };
}
