using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    public partial class ThemeEditorApp : Windows.WindowBase
    {
        [Inject] private FileSystemService FS { get; set; } = default!;

        private string _path = "/themes/theme.css";
        private string _content = string.Empty;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Theme Editor";
            _ = Open();
        }

        private async Task Open()
        {
            if (await FS.Exists(_path))
                _content = await FS.ReadFile(_path);
        }

        private async Task Save()
        {
            await FS.WriteFile(_path, _content);
        }
    }
}
