// Web Push on this device (L-63). Blazor calls these through IJSObjectReference; nothing here asks for permission
// on its own: subscribe() runs only from the learner's tap.

/** iPhone/iPad: push works only in the app added to the Home Screen (iOS 16.4+), not in Safari's tab. */
export function isIos(nav = navigator) {
    return /iPad|iPhone|iPod/.test(nav.userAgent) || (nav.platform === 'MacIntel' && nav.maxTouchPoints > 1);
}

export function isInstalled(win = window) {
    return win.matchMedia?.('(display-mode: standalone)').matches === true || win.navigator.standalone === true;
}

/**
 * What this device can do: "install" (iPhone browser tab: install the app first), "unsupported", "denied" (the
 * learner blocked notifications), "subscribed", or "ready" (supported, not subscribed yet).
 */
export async function state(win = window) {
    const nav = win.navigator;
    const supported = 'serviceWorker' in nav && 'PushManager' in win && 'Notification' in win;
    if (isIos(nav) && !isInstalled(win)) return 'install';
    if (!supported) return 'unsupported';
    if (win.Notification.permission === 'denied') return 'denied';
    const subscription = await current(nav);
    return subscription ? 'subscribed' : 'ready';
}

async function registration(nav) {
    // The worker is registered after Blazor starts (index.html); it is normally ready long before a tap.
    return Promise.race([nav.serviceWorker.ready, new Promise(r => setTimeout(() => r(null), 10000))]);
}

async function current(nav) {
    const reg = await registration(nav);
    return reg ? reg.pushManager.getSubscription() : null;
}

/** The VAPID key as the bytes pushManager.subscribe wants. */
export function keyBytes(base64Url) {
    const base64 = (base64Url + '='.repeat((4 - base64Url.length % 4) % 4)).replace(/-/g, '+').replace(/_/g, '/');
    return Uint8Array.from(atob(base64), c => c.charCodeAt(0));
}

/** Asks for permission (from a tap) and subscribes; returns {endpoint, p256dh, auth} or {error}. */
export async function subscribe(publicKey, win = window) {
    try {
        const permission = await win.Notification.requestPermission();
        if (permission !== 'granted') return { error: permission === 'denied' ? 'denied' : 'dismissed' };
        const reg = await registration(win.navigator);
        if (!reg) return { error: 'unsupported' };
        const subscription = await reg.pushManager.getSubscription()
            ?? await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(publicKey) });
        const json = subscription.toJSON();
        return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
    } catch {
        return { error: 'failed' };
    }
}

/** Unsubscribes this device; returns the endpoint it had (for the server to forget), or null. */
export async function unsubscribe(win = window) {
    try {
        const subscription = await current(win.navigator);
        if (!subscription) return null;
        const endpoint = subscription.endpoint;
        await subscription.unsubscribe();
        return endpoint;
    } catch {
        return null;
    }
}

/** This device's endpoint, so the page can tell the server which subscription is this one. */
export async function endpoint(win = window) {
    try { return (await current(win.navigator))?.endpoint ?? null; } catch { return null; }
}
