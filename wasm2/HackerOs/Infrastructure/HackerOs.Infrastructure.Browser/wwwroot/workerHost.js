// Web Workers of HackerOS (ADR 0070, 0071), the page half of Workers/BrowserWebWorkerRuntime.cs,
// imported by that class through JSHost.ImportAsync. Never referenced by a host (AGENTS.md).
//
// Each worker is a module worker running workerBootstrap.js, which starts its own .NET runtime from
// the same _framework/dotnet.js as the page (cold start 0.5 to 2 s) and hands every message to the
// worker host's exported entry point. Between the page and a worker, messages are plain strings (the
// JSON of the WorkerHost protocol); the only objects are the bootstrap's own control messages.
//
// The JavaScript does nothing else: placement, heartbeats and kills are decided in C#. Terminating a
// worker (worker.terminate()) is the only way to stop .NET code that never yields (ADR 0069, step 4).

const workers = new Map();
let nextId = 0;

/** Resolves a path of the app through the page's import map (fingerprinted file names), if any. */
function resolveAsset(path) {
    const plain = new URL(path, document.baseURI).href;
    try {
        return import.meta.resolve(plain);
    } catch {
        return plain;
    }
}

/** navigator.hardwareConcurrency: the ceiling of the worker count (ADR 0071, section 4). */
export function hardwareConcurrency() {
    return navigator.hardwareConcurrency || 2;
}

/** Resolves once the browser is idle (requestIdleCallback), or after timeoutMs at the latest. */
export function whenIdle(timeoutMs) {
    return new Promise(resolve => {
        if (typeof globalThis.requestIdleCallback === 'function') {
            globalThis.requestIdleCallback(() => resolve(), { timeout: timeoutMs });
        } else {
            setTimeout(resolve, Math.min(timeoutMs, 50));
        }
    });
}

/**
 * Starts one worker and returns its id. onEvent(kind, payload) is called with:
 * "ready" once its runtime is up, "message" for every message it posts, "error" if it fails.
 */
export function startWorker(name, entryAssembly, entryType, onEvent) {
    const id = ++nextId;
    const bootstrapUrl = resolveAsset('_content/HackerOs.Infrastructure.Browser/workerBootstrap.js');
    const worker = new Worker(bootstrapUrl, { type: 'module', name });
    workers.set(id, worker);
    worker.onmessage = event => {
        const data = event.data;
        if (typeof data === 'string') {
            onEvent('message', data);
        } else if (data && data.__hackeros === 'ready') {
            onEvent('ready', '');
        } else if (data && data.__hackeros === 'error') {
            onEvent('error', String(data.message));
        }
    };
    worker.onerror = event => {
        event.preventDefault();
        onEvent('error', event.message || 'worker script error');
    };
    worker.postMessage({
        __hackeros: 'boot',
        dotnetUrl: resolveAsset('_framework/dotnet.js'),
        entryAssembly,
        entryType
    });
    return id;
}

/** Posts one protocol message to a worker. */
export function post(id, message) {
    workers.get(id)?.postMessage(message);
}

/** Stops a worker at once, whatever it is running. */
export function terminate(id) {
    const worker = workers.get(id);
    if (worker) {
        workers.delete(id);
        worker.terminate();
    }
}
