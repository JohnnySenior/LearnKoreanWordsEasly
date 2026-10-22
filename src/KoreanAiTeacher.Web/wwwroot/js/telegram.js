// Thin wrapper over the Telegram Mini App SDK (telegram-web-app.js). Keep logic in C#.
const tg = () => window.Telegram && window.Telegram.WebApp;

// Light or dark follows Telegram's colour scheme; outside Telegram the CSS falls back to the device setting.
function applyTheme(app) {
    const scheme = app && app.colorScheme;
    if (scheme === 'dark' || scheme === 'light') {
        document.documentElement.dataset.theme = scheme;
        try {
            const bg = scheme === 'dark' ? '#191817' : '#f3f2f2';
            app.setHeaderColor && app.setHeaderColor(bg);
            app.setBackgroundColor && app.setBackgroundColor(bg);
        } catch { }
    }
}

export function init() {
    const app = tg();
    if (!app) return;
    try {
        app.ready();
        app.expand();
        applyTheme(app);
        app.onEvent && app.onEvent('themeChanged', () => applyTheme(app));
    } catch { /* not inside Telegram */ }
}

export function getInfo() {
    const app = tg();
    const inside = !!(app && app.initData);
    return {
        insideTelegram: inside,
        platform: app ? app.platform : null,
        version: app ? app.version : null,
        colorScheme: app ? app.colorScheme : null,
        initData: inside ? app.initData : null,
        viewportHeight: app ? app.viewportStableHeight : null,
        userAgent: navigator.userAgent,
        loadMs: Math.round(performance.now() - (window.__appStart || 0)),
    };
}

export function haptic(kind) {
    const app = tg();
    try { app && app.HapticFeedback && app.HapticFeedback.impactOccurred(kind || 'light'); } catch { }
}

// Small device-local store for answers that could not be sent yet (spec REL03).
// Storage can be missing or blocked (private mode, WebView settings), so every call fails soft.
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
