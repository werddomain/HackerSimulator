using HackerOs.Platform.Core.Shell;
using HackerOs.Windowing.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace HackerOs.Platform.Blazor.Windows;

/// <summary>
/// Draws the window manager's keyboard surfaces (ADR 0095 §3) and connects the browser's key events
/// to <see cref="WindowKeyboardCommands"/> through <c>WindowManagerOverlays.razor.js</c>.
/// </summary>
public sealed partial class WindowManagerOverlays : ComponentBase
{
    private const string ModulePath = "./_content/HackerOs.Platform.Blazor/Windows/WindowManagerOverlays.razor.js";

    private IJSObjectReference? _module;
    private DotNetObjectReference<WindowManagerOverlays>? _selfReference;
    private ElementReference _menuElement;
    private WindowMenuState? _renderedMenu;
    private int _menuIndex;
    private bool _disposed;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    /// <summary>Gets or sets the window manager's keyboard commands.</summary>
    [Parameter, EditorRequired]
    public WindowKeyboardCommands Commands { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Commands.Changed += OnCommandsChanged;
        Commands.ToggleFullScreen ??= ToggleFullScreenAsync;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
                _selfReference = DotNetObjectReference.Create(this);
                await _module.InvokeVoidAsync("attachWindowKeyboard", _selfReference, BindingsForBrowser());
            }
            catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException)
            {
                // No browser (component tests, prerendering): shortcuts are simply not caught.
                _module = null;
            }

            await Commands.StartAsync();
        }

        if (Commands.Menu is { } menu && !ReferenceEquals(menu, _renderedMenu))
        {
            _renderedMenu = menu;
            _menuIndex = 0;
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("placeWindowMenu", _menuElement, menu.X, menu.Y);
            }

            await _menuElement.FocusAsync();
        }
        else if (Commands.Menu is null)
        {
            _renderedMenu = null;
        }
    }

    /// <summary>Runs a shortcut the browser caught (an index into the bindings it was given).</summary>
    /// <param name="action">The bound action.</param>
    [JSInvokable]
    public Task OnShortcutAsync(int action) =>
        Enum.IsDefined((WindowKeyboardAction)action)
            ? Commands.ExecuteAsync((WindowKeyboardAction)action)
            : Task.CompletedTask;

    /// <summary>Handles a key the browser forwarded while the window manager holds the keyboard.</summary>
    [JSInvokable]
    public void OnGrabbedKey(string key, string code, bool control, bool alt, bool shift, bool super) =>
        Commands.HandleGrabbedKey(new WindowKeyInput(key, code, control, alt, shift, super));

    /// <summary>Alt was released after a switch shortcut.</summary>
    [JSInvokable]
    public void OnAltReleased() => Commands.HandleAltReleased();

    /// <summary>The browser entered or left full screen, with or without the keyboard lock.</summary>
    [JSInvokable]
    public void OnHostKeyboardState(bool fullScreen, bool locked) => Commands.SetHostKeyboardState(fullScreen, locked);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        Commands.Changed -= OnCommandsChanged;
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("detachWindowKeyboard");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _selfReference?.Dispose();
    }

    private static string ItemId(WindowId id) => $"window-switcher-{id.Value:N}";

    private static string Initial(string title) =>
        string.IsNullOrWhiteSpace(title) ? "?" : title.Trim()[..1].ToUpperInvariant();

    private object[] BindingsForBrowser() =>
    [
        .. Commands.ActiveBindings.Select(static binding => new
        {
            action = (int)binding.Action,
            ctrl = binding.Chord.Control,
            alt = binding.Chord.Alt,
            shift = binding.Chord.Shift,
            meta = binding.Chord.Super,
            key = binding.Chord.DomKey,
            code = binding.Chord.DomCode,
            holdsAlt = binding.Chord.Alt
                && binding.Action is WindowKeyboardAction.SwitchNext or WindowKeyboardAction.SwitchPrevious,
        }),
    ];

    private string GrabMode() => Commands.Mode switch
    {
        WindowKeyboardMode.Switching => "switching",
        WindowKeyboardMode.Moving => "moving",
        WindowKeyboardMode.Resizing => "resizing",
        _ => "none",
    };

    private void OnCommandsChanged()
    {
        if (_disposed)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            if (_module is not null)
            {
                try
                {
                    await _module.InvokeVoidAsync("updateWindowKeyboard", BindingsForBrowser(), GrabMode());
                }
                catch (JSDisconnectedException)
                {
                }
            }

            StateHasChanged();
        });
    }

    private async Task ToggleFullScreenAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("toggleFullScreen");
        }
    }

    private void CloseMenu() => Commands.CloseMenu();

    private async Task RunMenuItemAsync(WindowMenuItem item)
    {
        if (Commands.Menu is { } menu && item.IsEnabled)
        {
            await Commands.ExecuteForWindowAsync(item.Action, menu.WindowId);
        }
    }

    /// <summary>Arrow keys move the highlight, Enter or Space runs it, Escape closes the menu.</summary>
    private async Task HandleMenuKeyDownAsync(KeyboardEventArgs args)
    {
        if (Commands.Menu is not { } menu)
        {
            return;
        }

        IReadOnlyList<WindowMenuItem> items = Commands.GetMenuItems(menu.WindowId);
        if (items.Count == 0)
        {
            Commands.CloseMenu();
            return;
        }

        switch (args.Key)
        {
            case "Escape":
                Commands.CloseMenu();
                break;
            case "ArrowDown":
                _menuIndex = NextEnabled(items, _menuIndex, +1);
                break;
            case "ArrowUp":
                _menuIndex = NextEnabled(items, _menuIndex, -1);
                break;
            case "Home":
                _menuIndex = NextEnabled(items, -1, +1);
                break;
            case "End":
                _menuIndex = NextEnabled(items, items.Count, -1);
                break;
            case "Enter" or " ":
                await RunMenuItemAsync(items[Math.Clamp(_menuIndex, 0, items.Count - 1)]);
                break;
        }
    }

    private static int NextEnabled(IReadOnlyList<WindowMenuItem> items, int from, int direction)
    {
        for (int step = 1; step <= items.Count; step++)
        {
            int index = ((from + (direction * step)) % items.Count + items.Count) % items.Count;
            if (items[index].IsEnabled)
            {
                return index;
            }
        }

        return Math.Clamp(from, 0, items.Count - 1);
    }
}
