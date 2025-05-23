using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm.Apps
{
    public partial class TextEditorApp : Windows.WindowBase
    {
        [Inject] private FileSystemService FS { get; set; } = default!;

        private string _path = string.Empty;
        private string _content = string.Empty;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Title = "Text Editor";
            _ = Open();
        }

        private async Task Open()
        {
            if (string.IsNullOrWhiteSpace(_path))
                return;
            if (await FS.Exists(_path))
            {
                _content = await FS.ReadFile(_path);
            }
        }

        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(_path))
                return;
            await FS.WriteFile(_path, _content);
        }
    }
}
