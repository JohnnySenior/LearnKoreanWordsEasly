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

// Web Push reminders (L-63): the server sends {title, body, url, tag}; tapping opens practice, in a window already
// open when there is one. Same code in service-worker.js and service-worker.published.js.
self.addEventListener('push', event => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; } catch { data = { body: event.data ? event.data.text() : '' }; }
    event.waitUntil(self.registration.showNotification(data.title || 'Koreyscha', {
        body: data.body || 'Takrorlash vaqti keldi.',
        icon: '/icons/icon-192.png',
        badge: '/icons/icon-maskable-192.png',
        tag: data.tag || 'lugat-reminder',
        data: { url: data.url || '/takrorlash' },
    }));
});
self.addEventListener('notificationclick', event => {
    event.notification.close();
    const target = new URL((event.notification.data && event.notification.data.url) || '/takrorlash', self.location.origin).href;
    event.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(windows => {
        const open = windows.find(w => new URL(w.url).origin === self.location.origin);
        if (open) return open.focus().then(w => (w || open).navigate ? (w || open).navigate(target) : null).catch(() => self.clients.openWindow(target));
        return self.clients.openWindow(target);
    }));
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
