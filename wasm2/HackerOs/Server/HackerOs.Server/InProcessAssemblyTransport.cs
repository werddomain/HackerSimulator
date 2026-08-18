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
    [RequiresUnreferencedCode("Resolves already-loaded assemblies, or loads them by simple name; no additional metadata is fetched.")]
    public Task<IReadOnlyList<Assembly>> LoadAsync(IReadOnlyList<string> assemblyNames, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<Assembly> resolved = new(assemblyNames.Count);
        foreach (string assemblyName in assemblyNames)
        {
            Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
            Assembly? match = Array.Find(loaded, assembly =>
                string.Equals(assembly.GetName().Name + ".dll", assemblyName, StringComparison.Ordinal));
            if (match is null)
            {
                // Every declared app project is a direct compile-time reference of this host
                // (see this class's own doc comment), so its DLL sits next to HackerOs.Server.dll
                // in the output directory — but a compile-time reference alone does not put an
                // assembly into AppDomain.CurrentDomain.GetAssemblies(): the CLR only loads an
                // assembly once something actually touches one of its types, and this app's
                // catalog wiring resolves app assemblies by name/reflection rather than a direct
                // C# reference, so nothing forces that load before the first launch attempt.
                // Assembly.Load resolves it from the same probing path the compile-time reference
                // already guaranteed exists.
                try
                {
                    match = Assembly.Load(Path.GetFileNameWithoutExtension(assemblyName));
                }
                catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or BadImageFormatException)
                {
                    LogAssemblyNotLoaded(assemblyName);
                    continue;
                }
            }

            resolved.Add(match);
        }

        return Task.FromResult<IReadOnlyList<Assembly>>(resolved);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Build-known assembly {AssemblyName} was requested but is not loaded in this process.")]
    private partial void LogAssemblyNotLoaded(string assemblyName);
}
