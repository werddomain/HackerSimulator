using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HackerOs.Ecosystem.Modules;

/// <summary>
/// A modern-styled welcome window that introduces the ecosystem and demonstrates
/// launching other applications through the <see cref="Registry.AppRegistry"/>.
/// </summary>
public partial class WelcomeApp
{
    private string _environment = "detecting\u2026";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load the collocated module, use it, and dispose it immediately so no
            // JS reference lingers after the window closes.
            await using var module = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./Modules/WelcomeApp.razor.js");
            _environment = await module.InvokeAsync<string>("describeEnvironment");
            StateHasChanged();
        }

        // IMPORTANT: WindowAppBase/WindowBase relies on OnAfterRenderAsync to load
        // its own JS interop module (drag/resize/etc.). Skipping this call breaks
        // window dragging and resizing for this app.
        await base.OnAfterRenderAsync(firstRender);
    }

    private void OpenShell() => Registry.Launch("hackeros.hackershell");

    private void OpenMonitor() => Registry.Launch("hackeros.sysmon");
}
