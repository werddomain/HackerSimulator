using HackerOs.OS.System;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace HackerOs;

/// <summary>
/// Startup class for initializing the application
/// </summary>
public static class Startup
{
    /// <summary>
    /// Initializes the application
    /// </summary>
    /// <param name="host">WebAssembly host</param>
    public static async Task InitializeAsync(WebAssemblyHost host)
    {
        // Get the main service from the service provider
        var mainService = host.Services.GetRequiredService<IMainService>();
        
        // Initialize the system
        await mainService.InitializeAsync();
    }
}
