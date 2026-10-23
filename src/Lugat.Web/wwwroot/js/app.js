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

// ---- PWA (spec §6.2) ----

// How this device installs the app: "installed" (opened from the home screen), "prompt" (Chrome's install offer is
// waiting), "ios" (Safari: Share → Add to Home Screen), or "manual" (browser menu).
export function installState() {
    if (window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true) return 'installed';
    if (window.__installPrompt) return 'prompt';
    const ua = navigator.userAgent;
    const ios = /iPhone|iPad|iPod/.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    return ios ? 'ios' : 'manual';
}

// Shows Chrome's install dialog; true when the learner accepted. The offer can be used once.
export async function promptInstall() {
    const prompt = window.__installPrompt;
    if (!prompt) return false;
    window.__installPrompt = null;
    try {
        await prompt.prompt();
        const choice = await prompt.userChoice;
        return choice.outcome === 'accepted';
    } catch {
        return false;
    }
}

// Calls dotnet.OnUpdateReady() once a new version is installed and waiting. Checks for one now and when the app
// comes back to the screen (an installed app can stay open for days), at most every 30 minutes.
export async function watchUpdates(dotnet) {
    const registration = await (window.__swRegistration || Promise.resolve(null));
    if (!registration) return false;
    let notified = false;
    const notify = () => {
        if (!notified && registration.waiting && navigator.serviceWorker.controller) {
            notified = true;
            dotnet.invokeMethodAsync('OnUpdateReady');
        }
    };
    const track = worker => worker && worker.addEventListener('statechange', () => { if (worker.state === 'installed') notify(); });
    registration.addEventListener('updatefound', () => track(registration.installing));
    track(registration.installing);
    notify();

    let lastCheck = Date.now();
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible' && Date.now() - lastCheck > 30 * 60 * 1000) {
            lastCheck = Date.now();
            registration.update().catch(() => { });
        }
    });
    return true;
}

// Moves the waiting version in and reloads once it controls the page; the old cache is deleted on activate.
export async function applyUpdate() {
    const registration = await (window.__swRegistration || Promise.resolve(null));
    if (!registration || !registration.waiting) {
        window.location.reload();
        return;
    }
    let reloading = false;
    navigator.serviceWorker.addEventListener('controllerchange', () => {
        if (!reloading) {
            reloading = true;
            window.location.reload();
        }
    });
    registration.waiting.postMessage({ type: 'SKIP_WAITING' });
}
