using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
using HackerOs.Ecosystem;
using HackerOs.Platform.Blazor.Hosting;
using HackerOs.Platform.Blazor.LazyLoading;
using System.Runtime.InteropServices.JavaScript;

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
// provider". Removing only the descriptor AddHackerOsEcosystem just added (mirrors
// Server/HackerOs.Server/Program.cs's identical fix for the same captive dependency) keeps
// the diagnostic sink itself intact; only its ILoggerFactory bridge is skipped for this host.
for (int i = builder.Services.Count - 1; i >= serviceCountBeforeEcosystem; i--)
{
    if (builder.Services[i].ServiceType == typeof(ILoggerProvider))
    {
        builder.Services.RemoveAt(i);
    }
}

await builder.Build().RunAsync();

internal partial class Program
{
    [JSImport("globalThis.document.getElementById")]
    internal static partial JSObject? GetElementById(string id);
}
