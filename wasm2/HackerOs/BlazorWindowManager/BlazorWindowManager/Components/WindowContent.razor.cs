using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorWindowManager.Components;

/// <summary>
/// Component that renders the complete window structure including title bar, controls, and content.
/// This component acts as a wrapper around window content and delegates all window operations
/// to the provided Window parameter.
/// </summary>
public partial class WindowContent : ComponentBase
{
    /// <summary>
    /// The window instance that this content belongs to
    /// </summary>
    [Parameter, EditorRequired]
    public WindowBase Window { get; set; } = null!;

    /// <summary>
    /// Child content to render inside the window
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets the window classes from the parent window
    /// </summary>
    public string GetWindowClasses()
    {
        return Window?.GetWindowClasses() ?? "";
    }

    /// <summary>
    /// Gets the window style from the parent window
    /// </summary>
    public string GetWindowStyle()
    {
        return Window?.GetWindowStyle() ?? "";
    }

    /// <summary>
    /// Delegates window mouse down to parent window
    /// </summary>
    public async Task OnWindowMouseDown(MouseEventArgs e)
    {
        if (Window != null)
        {
            await Window.OnWindowMouseDown(e);
        }
    }

    /// <summary>
    /// Delegates title bar mouse down to parent window
    /// </summary>
    public async Task OnTitleBarMouseDown(MouseEventArgs e)
    {
        if (Window != null)
        {
            await Window.OnTitleBarMouseDown(e);
        }
    }

    /// <summary>
    /// Delegates minimize operation to parent window
    /// </summary>
    public async Task MinimizeWindow()
    {
        if (Window != null)
        {
            await Window.MinimizeWindow();
        }
    }

    /// <summary>
    /// Delegates maximize/restore toggle to parent window
    /// </summary>
    public async Task ToggleMaximize()
    {
        if (Window != null)
        {
            await Window.ToggleMaximize();
        }
    }

    /// <summary>
    /// Delegates close operation to parent window
    /// </summary>
    public async Task CloseWindow()
    {
        if (Window != null)
        {
            await Window.CloseWindow();
        }
    }

    /// <summary>
    /// Delegates resize start to parent window
    /// </summary>
    public async Task StartResize(MouseEventArgs e, string direction)
    {
        if (Window != null)
        {
            await Window.StartResize(e, direction);
        }
    }
}
