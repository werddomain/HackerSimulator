using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HackerOs.Ecosystem;
using HackerOs.Platform.Blazor.Hosting;
using HackerOs.Platform.Blazor.LazyLoading;
using System.Runtime.InteropServices.JavaScript;
using HackerOs.Infrastructure.Browser.Workers;
using HackerOs.Platform.WorkerHost;
using HackerOs.Simulation.Abstractions.Workers;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
// This assembly's WASM bundle is booted two different ways: as the standalone
// published PWA (its own wwwroot/index.html has a literal <div id="app">), and as
// the interactive WebAssembly render island for Blazor Web App hosts (test/test,
// and any future host following the same pattern), which mount this component tree
// via server-rendered markers instead and have no #app element at all -- registering
// an explicit root for a selector that doesn't exist there aborts the whole WASM boot
// before the marker-based mount ever runs. Only register the explicit root when #app
// genuinely exists in the DOM.
if (Program.GetElementById("app") is not null)
{
    builder.RootComponents.Add<App>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
}
builder.Services.AddSingleton<IEcosystemHostEnvironment, WebAssemblyEcosystemHostEnvironment>();
builder.Services.AddSingleton<IBuildKnownAssemblyTransport, WebAssemblyLazyAssemblyTransport>();
builder.Services.AddSingleton(provider => new BuildKnownAssemblyLoaderRegistry(
    BuildKnownLazyAssemblies.Names,
    provider.GetRequiredService<IBuildKnownAssemblyTransport>()));
builder.Services.AddSingleton(provider => new BuildKnownLazyAppDescriptorRegistry(
    BuildKnownLazyApps.Catalog,
    provider.GetRequiredService<BuildKnownAssemblyLoaderRegistry>()));
// ADR 0066: optional measured CPU/memory, decided once from HackerOs:ResourceAccounting in
// appsettings.json and what the build compiled in. Must precede AddHackerOsEcosystem, and runs before
// any app code so the context-switch hook can be installed.
builder.Services.AddHackerOsResourceAccounting(builder.Configuration);
builder.Services.AddHackerOsEcosystem(
    BuildKnownLazyApps.Catalog,
    provider => provider.GetRequiredService<BuildKnownLazyAppDescriptorRegistry>().Descriptors,
    provider => provider.GetRequiredService<BuildKnownLazyAppDescriptorRegistry>());

// ADR 0070/0071: commands and services run in Web Workers; the worker pool warms up once the
// desktop is shown. The JavaScript ships in HackerOs.Infrastructure.Browser's wwwroot, imported by
// BrowserWebWorkerRuntime itself. The server host never registers this (ADR 0070, section 6).
if (OperatingSystem.IsBrowser())
{
    builder.Services.AddSingleton<IWebWorkerRuntime, BrowserWebWorkerRuntime>();
    builder.Services.AddHackerOsWorkerProcesses(options => options with
    {
        // The built-in commands, lazy-loaded on the page, are loaded by each worker while it warms up.
        PreloadAssemblies = ["HackerOs.Commands.dll"]
    });
}

// AddHackerOsEcosystem's ILoggerProvider (HackerOsDiagnosticLoggerProvider) depends only on
// singletons since ADR 0045, so this host keeps it: ILogger<T> entries land in /var/log/syslog.

await builder.Build().RunAsync();

internal partial class Program
{
    [JSImport("globalThis.document.getElementById")]
    internal static partial JSObject? GetElementById(string id);
}
