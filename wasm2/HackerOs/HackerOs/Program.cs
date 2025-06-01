using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
//using BlazorWindowManager.Extensions; // Temporarily disabled for initial setup

namespace HackerOs
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            // Add Blazor Window Manager services including ThemeService
            // Temporarily disabled: builder.Services.AddBlazorWindowManager();

            // Add HackerOS Core Services
            builder.Services.AddHackerOSServices();

            builder.Services.AddOidcAuthentication(options =>
            {
                // Configure your authentication provider options here.
                // For more information, see https://aka.ms/blazor-standalone-auth
                builder.Configuration.Bind("Local", options.ProviderOptions);
            });

            await builder.Build().RunAsync();
        }
    }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHackerOSServices(this IServiceCollection services)
        {
            // Core Infrastructure Services (Singletons - shared across application)
            // TODO: Add IKernel service registration
            // TODO: Add IFileSystem service registration
            // TODO: Add IMemoryManager service registration
            
            // System Services (Scoped - per user session)
            // TODO: Add ISettingsService service registration
            // TODO: Add IUserManager service registration
            // TODO: Add ISecurityService service registration
            
            // Application Services (Scoped - per user session)
            // TODO: Add IShell service registration
            // TODO: Add IApplicationManager service registration
            
            // Network Services (Singleton - shared network stack)
            // TODO: Add INetworkStack service registration
            
            // Theme Services (already provided by BlazorWindowManager)
            // Integration with existing theme system will be handled in Theme module
            
            return services;
        }
    }
}
