using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    [AppIcon("fa:triangle-exclamation")]
    public partial class ErrorLogViewerApp : Windows.WindowBase
    {
        [Inject] private FileSystemService FS { get; set; } = default!;

        private string _content = string.Empty;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Error Log";
            _ = Load();
        }

        private async Task Load()
        {
            if (await FS.Exists("/logs/error.log"))
            {
                _content = await FS.ReadFile("/logs/error.log");
            }
            else
            {
                _content = "No log entries.";
            }
        }
    }
}
