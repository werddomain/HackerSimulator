using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Processes;

namespace HackerSimulator.Wasm
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddSingleton<WindowManager>();
            builder.Services.AddSingleton<FileSystemService>();
            builder.Services.AddTransient<CalculatorProcess>();
            builder.Services.AddTransient<TerminalProcess>();
            builder.Services.AddTransient<FileExplorerProcess>();
            builder.Services.AddSingleton<ShellService>();

            await builder.Build().RunAsync();
        }
    }
}
