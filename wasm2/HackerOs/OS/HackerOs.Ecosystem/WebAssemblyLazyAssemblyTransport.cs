using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using HackerOs.Platform.Blazor.LazyLoading;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace HackerOs.Ecosystem;

/// <summary>
/// Bridges the platform's build-known loading boundary to Blazor's browser
/// loader. The registry supplies only build-declared names, so this transport
/// cannot install arbitrary post-publish assemblies.
/// </summary>
public sealed class WebAssemblyLazyAssemblyTransport(LazyAssemblyLoader loader) : IBuildKnownAssemblyTransport
{
    [RequiresUnreferencedCode("Blazor LazyAssemblyLoader requires metadata preserved by build-known lazy descriptors.")]
    public async Task<IReadOnlyList<Assembly>> LoadAsync(
        IReadOnlyList<string> assemblyNames,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (await loader.LoadAssembliesAsync(assemblyNames)).ToArray();
    }
}

/// <summary>Single source of truth for optional assemblies emitted by this host build.</summary>
public static class BuildKnownLazyAssemblies
{
    /// <summary>Gets the exact app assemblies declared by the immutable host catalog.</summary>
    public static IReadOnlyList<string> Names { get; } =
        [.. BuildKnownLazyApps.Catalog.Manifests.Values
            .Select(manifest => manifest.EntryPoint.Assembly)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)];
}
