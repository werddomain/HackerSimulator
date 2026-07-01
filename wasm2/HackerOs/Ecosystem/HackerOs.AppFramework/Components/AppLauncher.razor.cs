using HackerOs.AppFramework.Registry;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HackerOs.AppFramework.Components;

/// <summary>
/// The start-menu style launcher. It lists every application discovered by the
/// <see cref="AppRegistry"/> grouped by category and launches the selected app
/// (which in turn appears on the taskbar via the window manager).
/// </summary>
public partial class AppLauncher : ComponentBase, IAsyncDisposable
{
    private ElementReference _root;
    private IJSObjectReference? _module;
    private DotNetObjectReference<AppLauncher>? _selfRef;
    private bool _open;

    /// <summary>The glyph shown on the launcher button.</summary>
    [Parameter] public string LogoGlyph { get; set; } = "\u26A1"; // ⚡

    /// <summary>The text shown on the launcher button.</summary>
    [Parameter] public string Label { get; set; } = "START";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Registry.AppsChanged += OnAppsChanged;
    }

    private void OnAppsChanged(object? sender, EventArgs e) => InvokeAsync(StateHasChanged);

    private IEnumerable<AppDescriptor> AppsInCategory(string category) =>
        Registry.LauncherApps.Where(a =>
            string.Equals(a.Category, category, StringComparison.OrdinalIgnoreCase));

    private async Task ToggleAsync()
    {
        _open = !_open;
        if (_open)
        {
            await EnsureModuleAsync();
            await _module!.InvokeVoidAsync("registerOutsideClick", _root, _selfRef);
        }
        else
        {
            await StopOutsideClickAsync();
        }
    }

    private async Task LaunchAsync(AppDescriptor app)
    {
        Registry.Launch(app);
        _open = false;
        await StopOutsideClickAsync();
    }

    /// <summary>
    /// Invoked from JavaScript when a click occurs outside the launcher so the
    /// menu can close itself.
    /// </summary>
    [JSInvokable]
    public Task CloseFromOutside()
    {
        if (_open)
        {
            _open = false;
            return InvokeAsync(StateHasChanged);
        }

        return Task.CompletedTask;
    }

    private async Task EnsureModuleAsync()
    {
        _selfRef ??= DotNetObjectReference.Create(this);
        _module ??= await JS.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/HackerOs.AppFramework/Components/AppLauncher.razor.js");
    }

    private async Task StopOutsideClickAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("unregisterOutsideClick");
            }
            catch (JSDisconnectedException)
            {
                // Circuit gone; ignore.
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Registry.AppsChanged -= OnAppsChanged;

        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("unregisterOutsideClick");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit gone; ignore.
            }
        }

        _selfRef?.Dispose();
    }
}
