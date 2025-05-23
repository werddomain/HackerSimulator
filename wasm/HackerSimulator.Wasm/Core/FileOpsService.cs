using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace HackerSimulator.Wasm.Core
{
    public class FileOpsService : IAsyncDisposable
    {
        private readonly IJSRuntime _js;
        private IJSObjectReference? _module;

        public FileOpsService(IJSRuntime js)
        {
            _js = js;
        }

        private async Task<IJSObjectReference> GetModule()
        {
            if (_module == null)
            {
                _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/fileops.js");
            }
            return _module;
        }

        public async Task SaveFile(string name, byte[] bytes)
        {
            var mod = await GetModule();
            await mod.InvokeVoidAsync("saveFile", name, bytes);
        }

        public async ValueTask DisposeAsync()
        {
            if (_module != null)
            {
                await _module.DisposeAsync();
            }
        }
    }
}
