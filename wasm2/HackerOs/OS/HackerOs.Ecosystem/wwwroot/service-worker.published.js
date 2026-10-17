// HackerOS Progressive Web App Service Worker (Published / Production Mode)
// Implements Cache-First offline shell strategy, atomic cache versioning,
// asset integrity checking, and safe update activation (P2-PWA-002 through P2-PWA-005),
// plus the corrupted-cache recovery of ADR 0099 §3 (RESET_CACHE message, see onMessage).

self.importScripts('./service-worker-assets.js');

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));
self.addEventListener('message', event => onMessage(event));

const cacheNamePrefix = 'hackeros-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
// Fonts (.ttf/.woff2) are listed so lazy-loaded apps keep their icons offline: the Code
// Editor ships Monaco's codicon.ttf next to its bundle (v3/11, ADR 0103 §3).
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.ttf$/, /\.svg$/, /\.webp$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

// Base URL for relative asset resolution
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('HackerOS Service Worker: Installing release asset cache', cacheName);

    const cache = await caches.open(cacheName);
    await cache.addAll(buildAssetRequests());
}

// Fetch requests for every cacheable item of the assets manifest, with integrity verification (P2-PWA-003).
function buildAssetRequests() {
    return self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
}

async function onActivate(event) {
    console.info('HackerOS Service Worker: Activating cache version', cacheName);

    // Delete all stale/previous version caches atomically to avoid mixing old and new release assets (P2-PWA-005)
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));

    await self.clients.claim();
}

async function onFetch(event) {
    if (event.request.method !== 'GET') {
        return fetch(event.request);
    }

    // Cache-First strategy for static assets and HTML shell (P2-PWA-004)
    const shouldServeIndexHtml = event.request.mode === 'navigate'
        && !manifestUrlList.some(url => url === event.request.url);

    const request = shouldServeIndexHtml ? 'index.html' : event.request;
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(request);

    if (cachedResponse) {
        return cachedResponse;
    }

    try {
        const networkResponse = await fetch(event.request);
        return networkResponse;
    } catch (error) {
        // When network is unavailable and request is navigation, serve cached shell
        if (event.request.mode === 'navigate') {
            const fallbackShell = await cache.match('index.html');
            if (fallbackShell) {
                return fallbackShell;
            }
        }
        throw error;
    }
}

function onMessage(event) {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        console.info('HackerOS Service Worker: Received SKIP_WAITING signal');
        self.skipWaiting();
    } else if (event.data && event.data.type === 'RESET_CACHE') {
        // ADR 0099 §3: the page's boot failed on an integrity or load error. Reply on the
        // MessageChannel port the page passed, so it knows whether to reload.
        const port = event.ports && event.ports[0];
        event.waitUntil(resetCache()
            .catch(error => ({ ok: false, reason: 'error', message: String(error) }))
            .then(result => port && port.postMessage(result)));
    }
}

// Empties the release cache and refills it from the server (ADR 0099 §3).
// - Server unreachable (offline): the damaged cache is kept as it is, so the shell can still
//   show the "reconnect once" screen offline; reply { ok: false, reason: 'unreachable' }.
// - Otherwise every asset is downloaded again, integrity-checked, into a staging cache that
//   then replaces the damaged one. If the server cannot give a verified copy of this release
//   (e.g. a newer release is deployed), the damaged cache is emptied anyway: resources then
//   come straight from the server, and the page's one-reload guard prevents any loop.
async function resetCache() {
    console.warn('HackerOS Service Worker: Rebuilding damaged cache', cacheName);
    try {
        await fetch(new Request('service-worker-assets.js', { cache: 'no-store' }));
    } catch {
        return { ok: false, reason: 'unreachable' };
    }

    // Starts with cacheNamePrefix, so onActivate also purges a staging cache left half-written.
    const stagingName = `${cacheName}-repair`;
    await caches.delete(stagingName);
    const staging = await caches.open(stagingName);
    let refilled = true;
    try {
        await staging.addAll(buildAssetRequests());
    } catch (error) {
        console.warn('HackerOS Service Worker: Could not download a verified copy of this release', error);
        refilled = false;
    }

    await caches.delete(cacheName);
    if (refilled) {
        const cache = await caches.open(cacheName);
        for (const request of await staging.keys()) {
            await cache.put(request, await staging.match(request));
        }
    }
    await caches.delete(stagingName);
    return { ok: true, refilled };
}
