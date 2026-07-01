using HackerOs.AppFramework.Extensions;
using HackerOs.Ecosystem;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register the HackerOS application framework and auto-discover every module in
// this assembly that is decorated with [App]. Adding a new application is as
// simple as dropping a new component into the Modules folder.
builder.Services.AddHackerOsAppFramework(typeof(App).Assembly);

await builder.Build().RunAsync();
