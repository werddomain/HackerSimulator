using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorWindowManager.Components;

/// <summary>
/// Simplified Window component that provides an easy-to-use interface for developers.
/// This component wraps WindowBase and WindowContent to provide a clean, declarative API.
/// 
/// <para>
/// <strong>Simple Usage:</strong>
/// <code>
/// &lt;Window WindowTitle="My Application"&gt;
///     &lt;p&gt;Your content goes here&lt;/p&gt;
/// &lt;/Window&gt;
/// </code>
/// </para>
/// 
/// <para>
/// <strong>With Icon:</strong>
/// <code>
/// &lt;Window WindowTitle="My Application" WindowIcon="myIcon"&gt;
///     &lt;p&gt;Your content goes here&lt;/p&gt;
/// &lt;/Window&gt;
/// 
/// @code {
///     private RenderFragment myIcon = @&lt;span&gt;🖥️&lt;/span&gt;;
/// }
/// </code>
/// </para>
/// </summary>
public partial class Window : WindowBase
{
    /// <summary>
    /// The title displayed in the window's title bar.
    /// This is the primary way to set the window title.
    /// </summary>
    [Parameter]
    public string WindowTitle { get; set; } = "Window";

    /// <summary>
    /// Optional icon content for the window's title bar.
    /// Can be an emoji, HTML element, or any RenderFragment.
    /// </summary>
    /// <example>
    /// <code>
    /// private RenderFragment myIcon = @&lt;span&gt;🖥️&lt;/span&gt;;
    /// // or
    /// private RenderFragment myIcon = @&lt;i class="fas fa-window"&gt;&lt;/i&gt;;
    /// </code>
    /// </example>
    [Parameter]
    public RenderFragment? WindowIcon { get; set; }

    /// <summary>
    /// Called when the component parameters are set.
    /// Ensures title and icon are synchronized with the base class.
    /// </summary>
    protected override void OnParametersSet()
    {
        // Synchronize title
        if (!string.IsNullOrEmpty(WindowTitle) && Title != WindowTitle)
        {
            Title = WindowTitle;
        }

        // Synchronize icon
        if (WindowIcon != null && Icon != WindowIcon)
        {
            Icon = WindowIcon;
        }

        base.OnParametersSet();
    }
}
