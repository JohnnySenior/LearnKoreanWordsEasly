// L-72: sign-in on the static home page, before Blazor has started. On a first visit the app needs ~3 MB and some
// seconds to start (10–20 s on a slow Android), and the visitor coming from /sinab looked for a sign-up button that
// was not there yet. These buttons talk to the same endpoints as Shared/SignIn.razor and keep the refresh token
// under the app's own key (SessionService.RefreshKey), so the app, once started, continues the session.

/** SessionService.RefreshKey. */
export const REFRESH_KEY = 'lugat.refresh';

/** Telegram's user object as the server checks it: every value a string, empty ones left out (as app.js does). */
export function telegramData(user) {
    const data = {};
    Object.entries(user || {}).forEach(([k, v]) => { if (v !== null && v !== undefined) data[k] = String(v); });
    return data;
}

/** The refresh token of a sign-in response, or null. */
export function refreshTokenOf(body) {
    return body && typeof body.refreshToken === 'string' && body.refreshToken ? body.refreshToken : null;
}

/** The server's reason for a refused sign-in (ProblemDetails.detail), else a plain one. */
export function failureOf(body) {
    return body && typeof body.detail === 'string' && body.detail ? body.detail : 'Kirish tasdiqlanmadi. Qayta urinib ko‘ring.';
}

/**
 * After a sign-in here: the app already showed its own sign-in screen (it read the device before the token was kept)
 * — reload so it starts signed in. While the static page is still up, the app will find the token itself.
 */
export function needsReload(doc) {
    return !!doc.querySelector('main.signin-page:not(.boot)');
}

function loadScript(src) {
    return new Promise((resolve, reject) => {
        const s = document.createElement('script');
        s.src = src; s.async = true; s.onload = resolve; s.onerror = () => reject(new Error(src));
        document.head.appendChild(s);
    });
}

async function post(url, body) {
    const response = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body), credentials: 'same-origin' });
    let json = null;
    try { json = await response.json(); } catch { }
    return { ok: response.ok, json };
}

export function start(doc = document) {
    const root = doc.getElementById('boot-signin');
    if (!root || doc.documentElement.classList.contains('returning')) return;
    const telegram = doc.getElementById('boot-telegram');
    const google = doc.getElementById('boot-google');
    const note = doc.getElementById('boot-signin-note');
    let busy = false;

    const say = (text, alert) => {
        if (!doc.contains(note)) return;
        note.textContent = text || ''; note.hidden = !text;
        note.setAttribute('role', alert ? 'alert' : 'status');
    };

    async function signIn(url, body) {
        if (busy) return;
        busy = true; say('Tekshirilmoqda…');
        try {
            const { ok, json } = await post(url, body);
            const token = ok ? refreshTokenOf(json) : null;
            if (!token) { busy = false; say(failureOf(json), true); return; }
            try { localStorage.setItem(REFRESH_KEY, token); } catch { }
            doc.documentElement.classList.add('returning'); // only the brand and the loading line from now on
            // Wait for the app: if it already decided "not signed in", start it again; it then reads the token.
            const timer = setInterval(() => {
                if (needsReload(doc)) { clearInterval(timer); location.reload(); }
                else if (!doc.querySelector('main.boot')) clearInterval(timer);
            }, 300);
        } catch {
            busy = false; say('Server bilan aloqa yo‘q. Internetni tekshirib, qayta urinib ko‘ring.', true);
        }
    }

    fetch('/api/auth/config').then(r => r.ok ? r.json() : null).then(async config => {
        if (!config || !doc.contains(root)) return;
        if (telegram && !config.telegramBotId) telegram.hidden = true;
        if (config.telegramBotId && telegram) {
            try {
                // Loaded ahead: a popup opened after a network wait would be blocked.
                await loadScript('https://telegram.org/js/telegram-widget.js?22');
                telegram.disabled = false;
                telegram.addEventListener('click', () => {
                    if (busy || !(window.Telegram && window.Telegram.Login)) return;
                    window.Telegram.Login.auth({ bot_id: config.telegramBotId, request_access: 'write', lang: 'uz' },
                        user => { if (user) signIn('/api/auth/telegram-login', { data: telegramData(user) }); });
                });
            } catch { telegram.hidden = true; }
        }
        if (config.googleClientId && google) {
            try {
                await loadScript('https://accounts.google.com/gsi/client');
                window.google.accounts.id.initialize({
                    client_id: config.googleClientId,
                    callback: response => signIn('/api/auth/google', { idToken: response.credential }),
                });
                window.google.accounts.id.renderButton(google, { theme: 'outline', size: 'large', text: 'continue_with', shape: 'rectangular', locale: 'uz', width: Math.min(400, Math.max(200, google.clientWidth || 280)) });
            } catch { }
        }
    }).catch(() => { if (doc.contains(telegram)) telegram.hidden = true; });
}
