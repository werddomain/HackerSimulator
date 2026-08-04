namespace HackerOs.Platform.Core.Lifecycle;

/// <summary>
/// Optional host boundary that makes a build-known app descriptor available before launch.
/// Platform Core never loads assemblies itself; browser hosts may implement this with a
/// constrained lazy loader while headless hosts can omit it.
/// </summary>
public interface IAppDescriptorLoader
{
    /// <summary>Ensures the descriptor for a known application is available to the lifecycle.</summary>
    ValueTask<AppDescriptorLoadResult> EnsureAvailableAsync(string appId, CancellationToken cancellationToken);
}

/// <summary>Typed, recoverable result of resolving an app descriptor for launch.</summary>
public sealed record AppDescriptorLoadResult(AppDescriptorLoadStatus Status, string? Detail = null)
{
    /// <summary>Creates an available result.</summary>
    public static AppDescriptorLoadResult Available() => new(AppDescriptorLoadStatus.Available);

    /// <summary>Creates a result for an app not declared by the host build.</summary>
    public static AppDescriptorLoadResult NotDeclared() => new(AppDescriptorLoadStatus.NotDeclared);

    /// <summary>Creates a recoverable unavailable-asset result.</summary>
    public static AppDescriptorLoadResult Unavailable(string detail) => new(AppDescriptorLoadStatus.Unavailable, detail);
}

/// <summary>Descriptor availability states exposed to the trusted lifecycle.</summary>
public enum AppDescriptorLoadStatus { Available, NotDeclared, Unavailable }
