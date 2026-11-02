// Published service worker, based on the Blazor PWA template (spec §6.2).
// - Caches the app shell and _framework files of this version, checked against their hashes.
// - Never caches /api: answers, progress and payments are personal and must be fresh.
// - A new version waits until the learner taps "Qayta yuklash" (message SKIP_WAITING); then the old cache is deleted.
self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
self.addEventListener('fetch', event => {
    if (handles(event.request)) {
        event.respondWith(onFetch(event.request));
    }
});
self.addEventListener('message', event => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

const cacheNamePrefix = 'lugat-shell-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.svg$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
// The runtime loads one of the three ICU files (by browser language); it is cached when first fetched.
const offlineAssetsExclude = [/^service-worker\.js$/, /icudt.*\.dat$/];
// Server-rendered word pages, the sitemap and robots.txt (L-56) come from the server, never the app shell.
const neverCached = [/^\/api\//, /^\/health/, /^\/soz(\/|$)/, /^\/sinab(\/|$)/, /^\/sitemap\.xml$/, /^\/robots\.txt$/];

const baseUrl = new URL('/', self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

function handles(request) {
    if (request.method !== 'GET') return false;
    const url = new URL(request.url);
    return url.origin === self.origin && !neverCached.some(pattern => pattern.test(url.pathname));
}

async function onInstall() {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    const cache = await caches.open(cacheName);
    await cache.addAll(assetsRequests);
}

async function onActivate() {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(request) {
    // Every page of the app (/bolimlar, /premium, …) is index.html; Blazor routes it.
    const shouldServeIndexHtml = request.mode === 'navigate' && !manifestUrlList.some(url => url === request.url);
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(shouldServeIndexHtml ? 'index.html' : request);
    if (cachedResponse) return cachedResponse;
    const response = await fetch(request);
    if (response.ok && manifestUrlList.includes(request.url)) {
        await cache.put(request, response.clone());
    }
    return response;
}
