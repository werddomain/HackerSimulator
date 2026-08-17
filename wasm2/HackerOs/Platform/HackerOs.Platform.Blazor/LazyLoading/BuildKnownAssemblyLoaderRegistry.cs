using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace HackerOs.Platform.Blazor.LazyLoading;

/// <summary>Loads only assemblies declared by the build and coalesces concurrent requests.</summary>
public sealed class BuildKnownAssemblyLoaderRegistry
{
    private readonly IReadOnlySet<string> _knownAssemblies;
    private readonly IBuildKnownAssemblyTransport _transport;
    private readonly ConcurrentDictionary<string, Lazy<Task<BuildKnownAssemblyLoadOutcome>>> _loads = new(StringComparer.Ordinal);

    public BuildKnownAssemblyLoaderRegistry(IEnumerable<string> knownAssemblies, IBuildKnownAssemblyTransport transport)
    {
        ArgumentNullException.ThrowIfNull(knownAssemblies);
        _knownAssemblies = new HashSet<string>(knownAssemblies, StringComparer.Ordinal);
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <summary>Loads one declared optional assembly at most once for the host lifetime.</summary>
    [RequiresUnreferencedCode("Lazy-loaded assemblies require their own trim-safe descriptors.")]
    public async Task<BuildKnownAssemblyLoadOutcome> LoadAsync(string assemblyName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assemblyName) || !_knownAssemblies.Contains(assemblyName))
            return BuildKnownAssemblyLoadOutcome.NotDeclared(assemblyName);

        Task<BuildKnownAssemblyLoadOutcome> sharedLoad = _loads.GetOrAdd(assemblyName, name => new Lazy<Task<BuildKnownAssemblyLoadOutcome>>(
            () => LoadDeclaredAsync(name), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        try
        {
            // Cancelling one launch must not cancel the single shared browser fetch;
            // another concurrent launcher may still need the declared asset.
            return await sharedLoad.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return BuildKnownAssemblyLoadOutcome.Cancelled(assemblyName);
        }
    }

    [RequiresUnreferencedCode("Lazy-loaded assemblies require their own trim-safe descriptors.")]
    private async Task<BuildKnownAssemblyLoadOutcome> LoadDeclaredAsync(string assemblyName)
    {
        try
        {
            IReadOnlyList<Assembly> assemblies = await _transport.LoadAsync([assemblyName], CancellationToken.None);
            Assembly? assembly = assemblies.SingleOrDefault(item => string.Equals(item.GetName().Name + ".dll", assemblyName, StringComparison.Ordinal))
                ?? assemblies.SingleOrDefault();
            return assembly is null
                ? BuildKnownAssemblyLoadOutcome.Missing(assemblyName)
                : BuildKnownAssemblyLoadOutcome.Loaded(assemblyName, assembly);
        }
        catch (OperationCanceledException) { return BuildKnownAssemblyLoadOutcome.Cancelled(assemblyName); }
        catch (Exception exception) { return BuildKnownAssemblyLoadOutcome.Failed(assemblyName, exception.Message); }
    }
}

/// <summary>Browser-specific transport boundary implemented with <c>LazyAssemblyLoader</c> by the WASM host.</summary>
public interface IBuildKnownAssemblyTransport
{
    [RequiresUnreferencedCode("The browser lazy loader loads metadata discovered after trimming.")]
    Task<IReadOnlyList<Assembly>> LoadAsync(IReadOnlyList<string> assemblyNames, CancellationToken cancellationToken);
}

/// <summary>Typed, recoverable result of a build-known assembly load request.</summary>
public sealed record BuildKnownAssemblyLoadOutcome(string AssemblyName, BuildKnownAssemblyLoadStatus Status, Assembly? Assembly, string? Detail)
{
    public static BuildKnownAssemblyLoadOutcome Loaded(string name, Assembly assembly) => new(name, BuildKnownAssemblyLoadStatus.Loaded, assembly, null);
    public static BuildKnownAssemblyLoadOutcome NotDeclared(string name) => new(name, BuildKnownAssemblyLoadStatus.NotDeclared, null, "The assembly is not declared by this build.");
    public static BuildKnownAssemblyLoadOutcome Missing(string name) => new(name, BuildKnownAssemblyLoadStatus.Missing, null, "The published asset did not load.");
    public static BuildKnownAssemblyLoadOutcome Cancelled(string name) => new(name, BuildKnownAssemblyLoadStatus.Cancelled, null, "The browser cancelled the assembly request.");
    public static BuildKnownAssemblyLoadOutcome Failed(string name, string detail) => new(name, BuildKnownAssemblyLoadStatus.Failed, null, detail);
}

public enum BuildKnownAssemblyLoadStatus { Loaded, NotDeclared, Missing, Cancelled, Failed }
