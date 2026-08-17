using HackerOs.Platform.Blazor.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace HackerOs.Ecosystem;

/// <summary>Wraps the WASM host environment for the render-mode-agnostic host environment abstraction.</summary>
public sealed class WebAssemblyEcosystemHostEnvironment(IWebAssemblyHostEnvironment inner) : IEcosystemHostEnvironment
{
    public bool IsDevelopment => inner.IsDevelopment();
}
