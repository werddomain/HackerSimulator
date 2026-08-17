// HackerOS Progressive Web App Service Worker (Published / Production Mode)
// Implements Cache-First offline shell strategy, atomic cache versioning,
// asset integrity checking, and safe update activation (P2-PWA-002 through P2-PWA-005).

self.importScripts('./service-worker-assets.js');

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));
self.addEventListener('message', event => onMessage(event));

const cacheNamePrefix = 'hackeros-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

// Base URL for relative asset resolution
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('HackerOS Service Worker: Installing release asset cache', cacheName);

    // Fetch and cache all matching items from the assets manifest with integrity verification (P2-PWA-003)
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    const cache = await caches.open(cacheName);
    await cache.addAll(assetsRequests);
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
    }
}
