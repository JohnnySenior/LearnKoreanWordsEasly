// Development build: no offline cache, so every change shows at once.
// The published app uses service-worker.published.js (spec §6.2).
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { });

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
