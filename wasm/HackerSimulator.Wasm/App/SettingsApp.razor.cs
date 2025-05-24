using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    [AppIcon("fa:cog")]
    public partial class SettingsApp : Windows.WindowBase
    {
        [Inject] private IJSRuntime JS { get; set; } = default!;

        private bool _dark;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Settings";
        }

        private async Task Apply()
        {
            await JS.InvokeVoidAsync("eval", $"document.body.classList.toggle('dark',{_dark.ToString().ToLowerInvariant()})");
        }
    }
}
