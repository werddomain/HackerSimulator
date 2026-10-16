namespace HackerOs.Simulation.Abstractions.Workers;

/// <summary>
/// Starts Web Workers that run their own .NET runtime (ADR 0070, section 4). The browser
/// implementation lives in <c>HackerOs.Infrastructure.Browser</c> (a JavaScript module in its
/// <c>wwwroot/</c>, imported by C#, never referenced by a host); tests use an in-process loopback.
/// </summary>
/// <remarks>
/// A worker is a message pipe: strings in both directions, in order. It knows nothing about
/// processes: <c>HackerOs.Platform.WorkerHost</c> speaks its protocol over this pipe.
/// </remarks>
public interface IWebWorkerRuntime
{
    /// <summary>
    /// Gets <c>navigator.hardwareConcurrency</c>: the number of logical processors the browser
    /// reports, the ceiling of the worker count (ADR 0071, section 4).
    /// </summary>
    int HardwareConcurrency { get; }

    /// <summary>
    /// Starts one worker and completes once its runtime is loaded (cold start: 0.5 to 2 s, ADR 0070).
    /// </summary>
    /// <param name="options">What the worker loads.</param>
    /// <param name="cancellationToken">Abandons the wait; the worker, if started, is terminated.</param>
    /// <returns>The ready worker.</returns>
    /// <exception cref="WebWorkerStartException">The worker could not start (script error, runtime abort).</exception>
    Task<IWebWorker> StartAsync(WebWorkerStartOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes once the browser is idle (<c>requestIdleCallback</c>), or after <paramref name="timeout"/>
    /// at the latest (ADR 0071, section 2).
    /// </summary>
    /// <param name="timeout">Longest wait.</param>
    /// <param name="cancellationToken">Abandons the wait.</param>
    Task WhenIdleAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>What a new worker loads (ADR 0070, section 4).</summary>
/// <param name="Name">Display name of the worker (browser developer tools), e.g. <c>hackeros-worker-2</c>.</param>
/// <param name="EntryAssembly">Assembly whose exported entry point receives the worker's messages.</param>
/// <param name="PreloadAssemblies">
/// Lazy-loaded assemblies to load while warming up (the built-in commands), so the first process
/// placed on the worker does not wait for them.
/// </param>
public sealed record WebWorkerStartOptions(
    string Name,
    string EntryAssembly,
    IReadOnlyList<string> PreloadAssemblies);

/// <summary>One running Web Worker: an ordered message pipe that the OS can terminate at any time.</summary>
public interface IWebWorker
{
    /// <summary>Gets the runtime-unique identifier of this worker.</summary>
    int Id { get; }

    /// <summary>Raised for every message the worker posts, in order, on the OS's thread.</summary>
    event Action<string>? MessageReceived;

    /// <summary>
    /// Raised once when the worker dies on its own (uncaught script error, runtime abort). Not raised
    /// by <see cref="Terminate"/>.
    /// </summary>
    event Action<string>? Faulted;

    /// <summary>Gets whether <see cref="Terminate"/> was called or the worker faulted.</summary>
    bool IsTerminated { get; }

    /// <summary>Posts one message to the worker. Ignored once terminated.</summary>
    /// <param name="message">The message (JSON text of the WorkerHost protocol).</param>
    void Post(string message);

    /// <summary>
    /// Stops the worker at once, whatever it is running (<c>worker.terminate()</c>): the only way to
    /// stop code that never yields (ADR 0069, section 3, step 4). Idempotent.
    /// </summary>
    void Terminate();
}

/// <summary>A worker could not start.</summary>
public sealed class WebWorkerStartException : Exception
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="message">What failed.</param>
    /// <param name="innerException">The underlying failure, if any.</param>
    public WebWorkerStartException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
