using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace HackerSimulator.Wasm.Core
{
    public class DatabaseService : IAsyncDisposable
    {
        private readonly IJSRuntime _js;
        private IJSObjectReference? _module;

        public DatabaseService(IJSRuntime js)
        {
            _js = js;
        }

        private async Task<IJSObjectReference> GetModule()
        {
            if (_module == null)
            {
                _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/database.js");
            }
            return _module;
        }

        public async Task<int> InitTable<T>(string name, int version, Func<int, Task>? migration)
        {
            var mod = await GetModule();
            var current = await mod.InvokeAsync<int>("initTable", name, version);
            if (migration != null && current < version)
            {
                await migration(current);
            }
            return current;
        }

        public async Task Set<T>(string store, string key, T value)
        {
            var mod = await GetModule();
            await mod.InvokeVoidAsync("set", store, key, value);
        }

        public async Task<T?> Get<T>(string store, string key)
        {
            var mod = await GetModule();
            return await mod.InvokeAsync<T?>("get", store, key);
        }

        public async Task<IReadOnlyList<T>> GetAll<T>(string store)
        {
            var mod = await GetModule();
            return await mod.InvokeAsync<IReadOnlyList<T>>("getAll", store);
        }

        public async Task Remove(string store, string key)
        {
            var mod = await GetModule();
            await mod.InvokeVoidAsync("remove", store, key);
        }

        public async Task Clear(string store)
        {
            var mod = await GetModule();
            await mod.InvokeVoidAsync("clear", store);
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
