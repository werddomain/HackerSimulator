namespace HackerOs.Platform.Blazor.Hosting;

/// <summary>Render-mode-agnostic host environment classification, supplied by each host's own Program.cs.</summary>
public interface IEcosystemHostEnvironment
{
    bool IsDevelopment { get; }
}
