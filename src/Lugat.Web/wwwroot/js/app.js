// Small browser helpers for the PWA. Keep logic in C#.

export function getInfo() {
    const standalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
    const dark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    return {
        standalone,
        colorScheme: dark ? 'dark' : 'light',
        userAgent: navigator.userAgent,
        loadMs: Math.round(performance.now() - (window.__appStart || 0)),
    };
}

// A short tap feedback where the device supports it (Android); iOS Safari has no vibration API.
export function vibrate() {
    try { navigator.vibrate && navigator.vibrate(10); } catch { }
}

// Small device-local store for answers that could not be sent yet (spec REL03).
// Storage can be missing or blocked (private mode, browser settings), so every call fails soft.
export function storageGet(key) {
    try { return window.localStorage.getItem(key); } catch { return null; }
}

export function storageSet(key, value) {
    try { window.localStorage.setItem(key, value); return true; } catch { return false; }
}

export function storageRemove(key) {
    try { window.localStorage.removeItem(key); } catch { }
}

export async function copyText(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}
