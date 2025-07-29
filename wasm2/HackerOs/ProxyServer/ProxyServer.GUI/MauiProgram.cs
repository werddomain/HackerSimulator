using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;
using ProxyServer.Network;
using ProxyServer.GUI.ViewModels;

namespace ProxyServer.GUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
#if IOS || MACCATALYST
    				handlers.AddHandler<Microsoft.Maui.Controls.CollectionView, Microsoft.Maui.Controls.Handlers.Items2.CollectionViewHandler2>();
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif            // Project related services
            builder.Services.AddSingleton<ProjectRepository>();
            builder.Services.AddSingleton<TaskRepository>();
            builder.Services.AddSingleton<CategoryRepository>();
            builder.Services.AddSingleton<TagRepository>();
            builder.Services.AddSingleton<SeedDataService>();
            builder.Services.AddSingleton<ModalErrorHandler>();
              // Add proxy server services
            builder.Services.AddSingleton<Network.WebSockets.WebSocketServer>();
            builder.Services.AddSingleton<Network.TCP.TcpConnectionManager>();
            builder.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(provider => 
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                    .CreateLogger("ProxyServer"));
            builder.Services.AddSingleton<Network.ProxyService>();
            
            // View Models
            builder.Services.AddSingleton<ViewModels.ProxyServerViewModel>();
            builder.Services.AddSingleton<ViewModels.SharedFolderViewModel>();
            builder.Services.AddSingleton<ViewModels.ProxySettingsViewModel>();
            builder.Services.AddSingleton<MainPageModel>();
            builder.Services.AddSingleton<ProjectListPageModel>();
            builder.Services.AddSingleton<ManageMetaPageModel>();
            
            // Pages
            builder.Services.AddTransientWithShellRoute<Pages.ProxyStatusPage, ViewModels.ProxyServerViewModel>("proxy-status");
            builder.Services.AddTransientWithShellRoute<Pages.SharedFolderConfigPage, ViewModels.SharedFolderViewModel>("shared-folders");
            builder.Services.AddTransientWithShellRoute<Pages.ProxySettingsPage, ViewModels.ProxySettingsViewModel>("proxy-settings");
            builder.Services.AddTransientWithShellRoute<ProjectDetailPage, ProjectDetailPageModel>("project");
            builder.Services.AddTransientWithShellRoute<TaskDetailPage, TaskDetailPageModel>("task");

            return builder.Build();
        }
    }
}
