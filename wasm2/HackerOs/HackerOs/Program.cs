using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HackerOs.OS.Shell;
using HackerOs.OS.Shell.Completion;
using HackerOs.OS.Applications;
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
            // TODO: Add ISecurityService service registration            // Application Services (Scoped - per user session)
            // Shell Services
            services.AddScoped<IShell, HackerOs.OS.Shell.Shell>();
            services.AddScoped<ICommandRegistry, CommandRegistry>();
            services.AddScoped<CommandParser>(); // No interface, register as concrete type
              // Register shell completion services
            services.AddScoped<ICompletionService, CompletionService>();
            services.AddScoped<ICompletionProvider, CommandCompletionProvider>();
            services.AddScoped<ICompletionProvider, FilePathCompletionProvider>();
            services.AddScoped<ICompletionProvider, VariableCompletionProvider>();
            
            // Configure completion service with providers
            services.AddScoped<ICompletionService>(serviceProvider =>
            {
                var completionService = new CompletionService(
                    serviceProvider.GetRequiredService<ILogger<CompletionService>>());
                
                // Register all completion providers
                var providers = serviceProvider.GetServices<ICompletionProvider>();
                foreach (var provider in providers)
                {
                    completionService.RegisterProvider(provider);
                }
                
                return completionService;
            });
            
            // Register shell commands
            RegisterShellCommands(services);
            
            // Add command initializer service
            services.AddScoped<ICommandInitializer, CommandInitializer>();
            
            // Applications Services (Singleton - system-wide application management)
            services.AddSingleton<IApplicationManager, ApplicationManager>();
            
            // Network Services (Singleton - shared network stack)
            // TODO: Add INetworkStack service registration
            
            // Theme Services (already provided by BlazorWindowManager)
            // Integration with existing theme system will be handled in Theme module
            
            return services;
        }
        
        private static void RegisterShellCommands(IServiceCollection services)
        {
            // Register all shell commands as scoped services
            services.AddScoped<HackerOs.OS.Shell.Commands.CatCommand>();
            services.AddScoped<HackerOs.OS.Shell.Commands.CdCommand>();
            services.AddScoped<HackerOs.OS.Shell.Commands.CpCommand>();
            services.AddScoped<HackerOs.OS.Shell.Commands.EchoCommand>();
            services.AddScoped<HackerOs.OS.Shell.Commands.FindCommand>();
            services.AddScoped<HackerOs.OS.Shell.Commands.GrepCommand>();
            services.AddScoped<HackerOs.OS.Shell.Commands.LsCommand>();
            services.AddScoped<HackerOs.OS.Shell.Commands.MkdirCommand>();
            services.AddScoped<HackerOs.OS.Shell.Commands.MvCommand>();
            services.AddScoped<HackerOs.OS.Shell.Commands.PwdCommand>();
            services.AddScoped<HackerOs.OS.Shell.Commands.RmCommand>();
            services.AddScoped<HackerOs.OS.Shell.Commands.TouchCommand>();
            services.AddScoped<HackerOs.OS.Shell.Commands.ShCommand>();
        }
    }
}
