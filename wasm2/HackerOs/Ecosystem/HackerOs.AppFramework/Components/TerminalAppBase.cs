using System.Reflection;
using BlazorWindowManager.Components;
using HackerOs.AppFramework.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace HackerOs.AppFramework.Components;

/// <summary>
/// Base class for terminal (console) applications in the ecosystem.
/// </summary>
/// <remarks>
/// <para>
/// A terminal application is a fully code-defined component: derive from this
/// class, decorate it with <see cref="AppAttribute"/>, and implement
/// <see cref="OnCommandAsync"/> to react to user input. The base renders a window
/// containing an interactive <see cref="TerminalHost"/> &mdash; no markup file is
/// required, which keeps command line tools terse and focused on behaviour.
/// </para>
/// <example>
/// <code>
/// [App("Echo", Kind = AppKind.Terminal)]
/// public sealed class EchoApp : TerminalAppBase
/// {
///     protected override string Prompt =&gt; "echo&gt; ";
///     protected override Task OnCommandAsync(string command)
///     {
///         WriteLine(command);
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public abstract class TerminalAppBase : WindowBase
{
    private TerminalHost? _host;

    /// <summary>The application metadata declared via <see cref="AppAttribute"/>.</summary>
    protected AppAttribute? AppInfo { get; private set; }

    /// <summary>Number of terminal columns. Override to change the console size.</summary>
    protected virtual int Columns => 90;

    /// <summary>Number of terminal rows. Override to change the console size.</summary>
    protected virtual int Rows => 26;

    /// <summary>The prompt written before each input line.</summary>
    protected virtual string Prompt => "guest@hackeros:~$ ";

    /// <summary>Optional banner written once when the terminal starts.</summary>
    protected virtual string? Banner => null;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        AppInfo = GetType().GetCustomAttribute<AppAttribute>(inherit: false);

        if (AppInfo is not null)
        {
            if (string.IsNullOrWhiteSpace(Title) || Title == "Window")
            {
                Title = AppInfo.Name;
            }

            Icon ??= BuildIconFragment(AppInfo.Icon);
        }

        // Terminal apps default to a compact console footprint.
        InitialWidth ??= 720;
        InitialHeight ??= 460;

        base.OnInitialized();
    }

    /// <summary>Writes raw text to the terminal at the cursor position.</summary>
    protected void Write(string text) => _host?.Write(text);

    /// <summary>Writes a line of text to the terminal.</summary>
    protected void WriteLine(string text = "") => _host?.WriteLine(text);

    /// <summary>Clears the terminal screen.</summary>
    protected void ClearScreen() => _host?.ClearScreen();

    /// <summary>
    /// Called when the user submits a command. Override to implement the shell.
    /// </summary>
    /// <param name="command">The raw command line entered by the user.</param>
    protected abstract Task OnCommandAsync(string command);

    /// <summary>
    /// Called once when the terminal becomes interactive. Override to print a
    /// welcome message or perform start-up work.
    /// </summary>
    protected virtual Task OnStartedAsync() => Task.CompletedTask;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<WindowContent>(0);
        builder.AddComponentParameter(1, nameof(WindowContent.Window), this);
        builder.AddComponentParameter(2, nameof(WindowContent.ChildContent), (RenderFragment)(contentBuilder =>
        {
            contentBuilder.OpenComponent<TerminalHost>(0);
            contentBuilder.AddComponentParameter(1, nameof(TerminalHost.Columns), Columns);
            contentBuilder.AddComponentParameter(2, nameof(TerminalHost.Rows), Rows);
            contentBuilder.AddComponentParameter(3, nameof(TerminalHost.Prompt), Prompt);
            contentBuilder.AddComponentParameter(4, nameof(TerminalHost.Banner), Banner);
            contentBuilder.AddComponentParameter(5, nameof(TerminalHost.OnCommand),
                EventCallback.Factory.Create<string>(this, OnCommandAsync));
            contentBuilder.AddComponentParameter(6, nameof(TerminalHost.OnReady),
                EventCallback.Factory.Create(this, OnStartedAsync));
            contentBuilder.AddComponentReferenceCapture(7, reference => _host = (TerminalHost)reference);
            contentBuilder.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private static RenderFragment BuildIconFragment(string glyph) => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "app-icon-glyph");
        builder.AddContent(2, glyph);
        builder.CloseElement();
    };
}
