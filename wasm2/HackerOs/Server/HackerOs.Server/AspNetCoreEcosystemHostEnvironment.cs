using HackerOs.Platform.Blazor.Hosting;

namespace HackerOs.Server;

/// <summary>Wraps the ASP.NET Core host environment for the render-mode-agnostic host environment abstraction.</summary>
public sealed class AspNetCoreEcosystemHostEnvironment(IHostEnvironment inner) : IEcosystemHostEnvironment
{
    public bool IsDevelopment => inner.IsDevelopment();
}
