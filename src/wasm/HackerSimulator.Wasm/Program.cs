using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using HackerSimulator.Wasm.Core;

namespace HackerSimulator.Wasm
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddSingleton<KernelService>();
            builder.Services.AddSingleton<ShellService>();
            builder.Services.AddSingleton<FileSystemService>();

            await builder.Build().RunAsync();
        }
    }
}
