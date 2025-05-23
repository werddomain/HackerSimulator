using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using HackerSimulator.Wasm.Core;
using HackerSimulator.Wasm.Web;
using HackerSimulator.Wasm.Web.Controllers;

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

            builder.Services.AddSingleton<AutoRunService>();

            builder.Services.AddSingleton<NetworkService>();
            builder.Services.AddSingleton<DnsService>();
            builder.Services.AddSingleton<HackerHttpClient>();

            builder.Services.AddSingleton<HomeController>();
            builder.Services.AddSingleton<Windows.WindowManagerService>();


            var host = builder.Build();
            // Instantiate controllers so they register with the network
            _ = host.Services.GetRequiredService<HomeController>();

            await host.RunAsync();
        }
    }
}
