// Development build: no offline cache, so every change shows at once.
// The published app uses service-worker.published.js (spec §6.2).
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { });
