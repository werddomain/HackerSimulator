using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HackerOs.Platform.Blazor.LazyLoading;

namespace HackerOs.Server;

/// <summary>
/// Server-side build-known assembly transport. Every declared app assembly is already
/// loaded in-process at startup (the server host references every app project directly,
/// the same way the test harness does), so there is no separate download step to bridge —
/// unlike the WASM transport, this never fetches anything.
/// </summary>
public sealed partial class InProcessAssemblyTransport(ILogger<InProcessAssemblyTransport> logger) : IBuildKnownAssemblyTransport
{
    [RequiresUnreferencedCode("Resolves already-loaded assemblies; no additional metadata is fetched.")]
    public Task<IReadOnlyList<Assembly>> LoadAsync(IReadOnlyList<string> assemblyNames, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
        List<Assembly> resolved = new(assemblyNames.Count);
        foreach (string assemblyName in assemblyNames)
        {
            Assembly? match = Array.Find(loaded, assembly =>
                string.Equals(assembly.GetName().Name + ".dll", assemblyName, StringComparison.Ordinal));
            if (match is null)
            {
                LogAssemblyNotLoaded(assemblyName);
                continue;
            }

            resolved.Add(match);
        }

        return Task.FromResult<IReadOnlyList<Assembly>>(resolved);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Build-known assembly {AssemblyName} was requested but is not loaded in this process.")]
    private partial void LogAssemblyNotLoaded(string assemblyName);
}
