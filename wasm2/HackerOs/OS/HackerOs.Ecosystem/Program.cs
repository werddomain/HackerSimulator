using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HackerOs.Ecosystem;
using HackerOs.Platform.Blazor.Hosting;
using HackerOs.Platform.Blazor.LazyLoading;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
// This Program.cs is the entry point only for the standalone published PWA
// (this project run/published on its own); test/test and Server/HackerOs.Server
// have their own separate Program.cs/Main and never execute this file, so root
// components must be registered here for the standalone app to render at all.
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
builder.Services.AddHackerOsEcosystem(
    BuildKnownLazyApps.Catalog,
    provider => provider.GetRequiredService<BuildKnownLazyAppDescriptorRegistry>().Descriptors,
    provider => provider.GetRequiredService<BuildKnownLazyAppDescriptorRegistry>());

await builder.Build().RunAsync();
