// Bootstrap of one HackerOS Web Worker (ADR 0070, section 4), started by workerHost.js.
//
// It loads the page's own .NET runtime (_framework/dotnet.js) into this worker, then forwards every
// message to the exported entry point of HackerOs.Platform.WorkerHost (BrowserWorkerEntryPoint), and
// gives .NET a `post` function back. Messages that arrive while the runtime loads are queued.
//
// `dotnetSidecar`: without it, dotnet.js takes any worker for one of its own WebAssembly threads
// (a pthread) and waits forever for a main thread that never comes; with it, the worker runs a
// complete, separate runtime.

let entryPoint = null;
const pending = [];

self.onmessage = event => {
    const data = event.data;
    if (data && typeof data === 'object' && data.__hackeros === 'boot') {
        boot(data);
    } else if (entryPoint) {
        entryPoint.Receive(data);
    } else {
        pending.push(data);
    }
};

async function boot({ dotnetUrl, entryAssembly, entryType }) {
    try {
        globalThis.dotnetSidecar = true;
        const { dotnet } = await import(dotnetUrl);
        const runtime = await dotnet.create();
        runtime.setModuleImports('hackeros-worker', {
            post: message => self.postMessage(message)
        });
        const exports = await runtime.getAssemblyExports(entryAssembly);
        entryPoint = entryType.split('.').reduce((scope, part) => scope?.[part], exports);
        if (!entryPoint) {
            throw new Error(`${entryType} is not exported by ${entryAssembly}.`);
        }

        entryPoint.Start();
        self.postMessage({ __hackeros: 'ready' });
        for (const message of pending.splice(0)) {
            entryPoint.Receive(message);
        }
    } catch (error) {
        self.postMessage({ __hackeros: 'error', message: String(error?.stack ?? error) });
    }
}
