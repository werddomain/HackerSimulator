using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using HackerOs.Platform.WorkerHost.Worker;

namespace HackerOs.Platform.WorkerHost.Browser;

/// <summary>
/// The .NET entry point of a HackerOS Web Worker (ADR 0070, section 4), called by
/// <c>workerBootstrap.js</c> (in <c>HackerOs.Infrastructure.Browser/wwwroot</c>) once the worker's own
/// runtime is loaded: <see cref="Start"/>, then <see cref="Receive"/> for every message from the OS.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class BrowserWorkerEntryPoint
{
    private static WorkerProcessHost? s_host;

    /// <summary>Creates the worker's host. Called once, before any message.</summary>
    [JSExport]
    public static void Start() =>
        s_host ??= new WorkerProcessHost(Post, new BrowserWorkerAssemblyLoader(), canMeterCpu: true);

    /// <summary>Handles one message from the OS.</summary>
    /// <param name="message">The JSON text.</param>
    [JSExport]
    public static void Receive(string message) => s_host?.Receive(message);

    /// <summary><c>self.postMessage</c>, given by the bootstrap.</summary>
    [JSImport("post", "hackeros-worker")]
    private static partial void Post(string message);
}

/// <summary>
/// Loads an app's assembly in a worker: the lazy-loaded ones (the commands) are fetched first through
/// the runtime's <c>loadLazyAssembly</c>, as Blazor's <c>LazyAssemblyLoader</c> does on the page.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed partial class BrowserWorkerAssemblyLoader : ResolvedAssemblyLoader
{
    private readonly HashSet<string> _loaded = new(StringComparer.Ordinal);

    public override async Task PreloadAsync(IReadOnlyList<string> assemblyFiles)
    {
        foreach (string file in assemblyFiles)
        {
            await LoadLazyAsync(file).ConfigureAwait(true);
        }
    }

    public override async Task<Type> LoadEntryTypeAsync(string assemblyFile, string typeName)
    {
        await LoadLazyAsync(assemblyFile).ConfigureAwait(true);
        return ResolveType(assemblyFile, typeName);
    }

    private async Task LoadLazyAsync(string assemblyFile)
    {
        string name = Path.GetFileNameWithoutExtension(assemblyFile);
        if (!_loaded.Add(name) || AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == name))
        {
            return;
        }

        try
        {
            await LoadLazyAssembly(name + ".wasm").ConfigureAwait(true);
        }
        catch (JSException)
        {
            // Not a lazy-loaded assembly: it is part of the boot set and loads on first use.
        }
    }

    /// <summary>The runtime's own lazy loader (the API behind Blazor's <c>LazyAssemblyLoader</c>).</summary>
    [JSImport("INTERNAL.loadLazyAssembly")]
    private static partial Task<bool> LoadLazyAssembly(string assemblyNameToLoad);
}
