using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using HackerOs.Ecosystem;
using HackerOs.Platform.Blazor.Hosting;
using HackerOs.Platform.Blazor.LazyLoading;

// This Program.cs is the entry point only for the standalone published PWA (this project
// run/published on its own). test/test's own page never has "#app" (it embeds
// HackerOs.Ecosystem.App directly in its markup instead, mounted via Blazor Web App's
// marker-based auto-discovery, not RootComponents.Add), yet this Program.cs still runs there:
// the shared WASM runtime that test/test's marker-mounted components depend on is bootstrapped
// by *a* Program.cs calling WebAssemblyHostBuilder.Build().RunAsync() — since test/test has no
// dedicated ".Client" WASM project of its own, this file's RunAsync() is what's actually
// starting that shared runtime there, in addition to its own standalone-PWA role. So RunAsync()
// must still run in both contexts, but RunAsync() mounts ALL registered RootComponents and
// aborts entirely (throwing before marker-based auto-discovery ever gets a turn) the moment ONE
// of them — "#app" here — can't find its target element. So the "#app" mapping is removed from
// the builder's RootComponents collection (still mutable after Build(), which only constructs
// the DI container) whenever "#app" doesn't exist on the page, checked via the now-available
// IJSRuntime — leaving RunAsync() free to complete its other job, bootstrapping the shared
// runtime that the marker-based components depend on.
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddSingleton<IEcosystemHostEnvironment, WebAssemblyEcosystemHostEnvironment>();
builder.Services.AddSingleton<IBuildKnownAssemblyTransport, WebAssemblyLazyAssemblyTransport>();
builder.Services.AddSingleton(provider => new BuildKnownAssemblyLoaderRegistry(
    BuildKnownLazyAssemblies.Names,
    provider.GetRequiredService<IBuildKnownAssemblyTransport>()));
builder.Services.AddSingleton(provider => new BuildKnownLazyAppDescriptorRegistry(
    BuildKnownLazyApps.Catalog,
    provider.GetRequiredService<BuildKnownAssemblyLoaderRegistry>()));
int serviceCountBeforeEcosystem = builder.Services.Count;
builder.Services.AddHackerOsEcosystem(
    BuildKnownLazyApps.Catalog,
    provider => provider.GetRequiredService<BuildKnownLazyAppDescriptorRegistry>().Descriptors,
    provider => provider.GetRequiredService<BuildKnownLazyAppDescriptorRegistry>());

// AddHackerOsEcosystem registers HackerOsDiagnosticLoggerProvider as ILoggerProvider, which
// construct-injects the Scoped IPersistentDiagnosticRepository (Scoped because it construct-
// injects IJSRuntime — see EcosystemServiceCollectionExtensions.cs's scoping note). That
// shape is functionally harmless in WASM (IJSRuntime is available immediately, no circuit
// concept, so nothing is actually broken at runtime), but WebAssemblyHostBuilder.Build()
// validates it strictly anyway and throws "Cannot resolve scoped service ... from root
// provider" from Build() below, before RunAsync() ever gets a chance to run. Removing only the
// descriptor AddHackerOsEcosystem just added (mirrors Server/HackerOs.Server/Program.cs's
// identical fix for the same captive dependency) keeps the diagnostic sink itself intact;
// only its ILoggerFactory bridge is skipped for this host.
for (int i = builder.Services.Count - 1; i >= serviceCountBeforeEcosystem; i--)
{
    if (builder.Services[i].ServiceType == typeof(ILoggerProvider))
    {
        builder.Services.RemoveAt(i);
    }
}

WebAssemblyHost host = builder.Build();

IJSRuntime jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
bool hasAppRoot = await jsRuntime.InvokeAsync<bool>("eval", "document.querySelector('#app') !== null");
if (!hasAppRoot)
{
    // Both mappings registered above target this project's own standalone shell ("#app" and
    // the HeadOutlet at "head::after"); neither applies when this assembly is merely providing
    // the shared WASM runtime for another host's page, so both come out together.
    builder.RootComponents.Clear();
}

await host.RunAsync();
