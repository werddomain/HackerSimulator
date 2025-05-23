using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    public partial class ThemeEditorCssApp : Windows.WindowBase
    {
        [Inject] private FileSystemService FS { get; set; } = default!;

        private string _content = string.Empty;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Theme CSS";
            _ = Load();
        }

        private async Task Load()
        {
            if (await FS.Exists("/themes/theme.css"))
                _content = await FS.ReadFile("/themes/theme.css");
        }

        private async Task Save()
        {
            await FS.WriteFile("/themes/theme.css", _content);
        }
    }
}
