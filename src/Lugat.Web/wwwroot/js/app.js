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

// Light/dark: "Device" follows the phone (CSS prefers-color-scheme); Light or Dark forces it (spec §4.7).
export function setTheme(theme) {
    if (theme === 'Light' || theme === 'Dark') {
        document.documentElement.dataset.theme = theme.toLowerCase();
    } else {
        delete document.documentElement.dataset.theme;
    }
}

function loadScript(src, attributes, parent) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = src;
        script.async = true;
        Object.entries(attributes || {}).forEach(([k, v]) => script.setAttribute(k, v));
        script.onload = resolve;
        script.onerror = () => reject(new Error('script'));
        (parent || document.head).appendChild(script);
    });
}

// Telegram Login Widget (https://core.telegram.org/widgets/login). The widget's user object goes to C# with every
// value as a string; the server checks its hash. "write" access lets the bot send reminders without /start.
export async function telegramWidget(elementId, botUsername, dotnet) {
    const host = document.getElementById(elementId);
    if (!host) return false;
    host.innerHTML = '';
    window.lugatTelegramAuth = user => {
        const data = {};
        Object.entries(user).forEach(([k, v]) => { if (v !== null && v !== undefined) data[k] = String(v); });
        dotnet.invokeMethodAsync('OnTelegram', data);
    };
    try {
        await loadScript('https://telegram.org/js/telegram-widget.js?22', {
            'data-telegram-login': botUsername,
            'data-size': 'large',
            'data-radius': '0',
            'data-onauth': 'lugatTelegramAuth(user)',
            'data-request-access': 'write',
            'data-lang': 'uz',
        }, host);
        return true;
    } catch {
        return false;
    }
}

// Google Identity Services: the button returns a signed ID token (credential), which the server verifies.
export async function googleButton(elementId, clientId, dotnet) {
    const host = document.getElementById(elementId);
    if (!host) return false;
    try {
        if (!(window.google && window.google.accounts && window.google.accounts.id)) {
            await loadScript('https://accounts.google.com/gsi/client');
        }
        window.google.accounts.id.initialize({
            client_id: clientId,
            callback: response => dotnet.invokeMethodAsync('OnGoogle', response.credential),
        });
        host.innerHTML = '';
        window.google.accounts.id.renderButton(host, { theme: 'outline', size: 'large', text: 'continue_with', shape: 'rectangular', locale: 'uz', width: 280 });
        return true;
    } catch {
        return false;
    }
}
