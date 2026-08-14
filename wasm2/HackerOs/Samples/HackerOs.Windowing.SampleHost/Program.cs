using HackerOs.Windowing.Core;
using HackerOs.Windowing.SampleHost;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(_ => new WindowRuntime(new WindowBounds(0, 0, 1280, 720)));

await builder.Build().RunAsync();
