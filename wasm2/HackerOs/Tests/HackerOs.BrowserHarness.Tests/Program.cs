using HackerOs.AppSdk.Icons;
using HackerOs.BrowserHarness.Tests;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddMudServices();
// HackerIcon (used throughout Taskbar.Blazor, Terminal, CodeEditor, HackPaint) injects
// IIconCatalog. Without it, any scenario that renders one crashes the WebAssembly renderer
// on first render, which leaves the page stuck on index.html's "Loading..." placeholder
// forever instead of surfacing an error - see HackerOs.Ecosystem's own AddSingleton<IIconCatalog>.
builder.Services.AddSingleton<IIconCatalog, IconCatalog>();

await builder.Build().RunAsync();