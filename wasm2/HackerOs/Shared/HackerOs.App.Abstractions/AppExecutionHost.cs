using System.Text.Json.Serialization;

namespace HackerOs.App.Abstractions;

/// <summary>
/// Where the OS runs an app's code (ADR 0070, section 1): on the browser's main thread with the
/// desktop, on a worker of the pool (one per virtual core), or on a worker of its own. Declared with
/// <c>[AppManifest(Execution = ...)]</c> and written to <c>manifest.json</c> as <c>execution</c>.
/// </summary>
/// <remarks>
/// A worker is a separate Web Worker running its own .NET runtime: an infinite loop there no longer
/// freezes the OS, and a <c>kill -9</c> can really stop it by terminating the worker. Window apps
/// and embedded components need the DOM, so they always run on the main thread. A host without Web
/// Workers (<c>HackerOs.Server</c>, ADR 0070 section 6) ignores the declaration and runs everything
/// in-process.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<AppExecutionHost>))]
public enum AppExecutionHost
{
    /// <summary>
    /// Not declared: the generator writes the default of the app's kind
    /// (<see cref="AppExecutionHosts.DefaultFor"/>). Never written to a manifest.
    /// </summary>
    [JsonStringEnumMemberName("default")]
    Default = 0,

    /// <summary>The browser's main thread, with the desktop and every window (the only choice for a window app).</summary>
    [JsonStringEnumMemberName("mainThread")]
    MainThread = 1,

    /// <summary>A worker of the pool: one worker per virtual core, shared by commands and pipelines (default of a command).</summary>
    [JsonStringEnumMemberName("pool")]
    Pool = 2,

    /// <summary>A worker of its own, taken from the always-ready worker (default of a service, ADR 0071).</summary>
    [JsonStringEnumMemberName("dedicated")]
    Dedicated = 3
}

/// <summary>Defaults and resolution of <see cref="AppExecutionHost"/> (ADR 0070, section 1).</summary>
public static class AppExecutionHosts
{
    /// <summary>
    /// Gets the default host of an app kind: <see cref="AppExecutionHost.MainThread"/> for a window
    /// app, <see cref="AppExecutionHost.Pool"/> for a command, <see cref="AppExecutionHost.Dedicated"/>
    /// for a service.
    /// </summary>
    /// <param name="kind">The app kind.</param>
    /// <returns>The host the OS uses when the app declares none.</returns>
    public static AppExecutionHost DefaultFor(AppKind kind) => kind switch
    {
        AppKind.Terminal => AppExecutionHost.Pool,
        AppKind.Service => AppExecutionHost.Dedicated,
        _ => AppExecutionHost.MainThread
    };

    /// <summary>
    /// Resolves a declared host: <see cref="AppExecutionHost.Default"/> becomes the default of
    /// <paramref name="kind"/>; any other value is kept (the validator rejects a worker for a window app).
    /// </summary>
    /// <param name="kind">The app kind.</param>
    /// <param name="declared">The declared host, if any.</param>
    /// <returns>The effective host.</returns>
    public static AppExecutionHost Resolve(AppKind kind, AppExecutionHost? declared) =>
        declared is null or AppExecutionHost.Default ? DefaultFor(kind) : declared.Value;

    /// <summary>
    /// Gets the effective host of a manifest: its <see cref="AppManifest.Execution"/>, or the default of
    /// its kind for a manifest written before ADR 0070.
    /// </summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The effective host.</returns>
    public static AppExecutionHost Of(AppManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return Resolve(manifest.Kind, manifest.Execution);
    }

    /// <summary>Gets whether <paramref name="host"/> runs the app in a Web Worker.</summary>
    /// <param name="host">An effective host.</param>
    /// <returns><see langword="true"/> for <see cref="AppExecutionHost.Pool"/> and <see cref="AppExecutionHost.Dedicated"/>.</returns>
    public static bool IsWorker(AppExecutionHost host) => host is AppExecutionHost.Pool or AppExecutionHost.Dedicated;
}
