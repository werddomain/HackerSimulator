using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.AspNetCore.Components.Web;
using MudBlazor.Services;
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

            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddSingleton<KernelService>();
            builder.Services.AddSingleton<AliasService>();
            builder.Services.AddSingleton<ShellService>();
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<FileSystemService>();
            builder.Services.AddSingleton<FileTypeService>();
            builder.Services.AddSingleton<FileOpsService>();
            builder.Services.AddSingleton<SettingsService>();

            // Register application discovery service
            builder.Services.AddSingleton<ApplicationService>();

            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<EncryptionService>();

            builder.Services.AddSingleton<AutoRunService>();

            builder.Services.AddSingleton<NetworkService>();
            builder.Services.AddSingleton<DnsService>();
            builder.Services.AddSingleton<HackerHttpClient>();

            builder.Services.AddSingleton<HomeController>();
            builder.Services.AddSingleton<Windows.WindowManagerService>();
            builder.Services.AddMudServices();
            builder.Services.AddSingleton<ThemeService>();
            

            var host = builder.Build();
            
            // Instantiate controllers so they register with the network
            _ = host.Services.GetRequiredService<HomeController>();

            await host.RunAsync();

        }
    }
}
