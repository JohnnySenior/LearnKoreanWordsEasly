// L-72: sign-in on the static home page before the app has started. It must use the app's own endpoints and key,
// so the app continues the session it finds.
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { REFRESH_KEY, telegramData, refreshTokenOf, failureOf, needsReload } from '../../src/Lugat.Web/wwwroot/js/boot-signin.js';

const web = new URL('../../src/Lugat.Web/', import.meta.url);
const read = path => readFile(new URL(path, web), 'utf8');
const [index, script, session, boot, css] = await Promise.all([
    read('wwwroot/index.html'), read('wwwroot/js/boot-signin.js'), read('Services/SessionService.cs'), read('Shared/BootScreen.razor'), read('wwwroot/css/app.css'),
]);

test('the token is kept under the app\'s key and posted to the app\'s sign-in endpoints', () => {
    assert.equal(REFRESH_KEY, session.match(/RefreshKey = "([^"]+)"/)[1]);
    assert.ok(session.includes('"api/auth/telegram-login", new TelegramLoginRequest(data)'));
    assert.ok(session.includes('"api/auth/google", new GoogleLoginRequest(idToken)'));
    assert.ok(script.includes("signIn('/api/auth/telegram-login', { data: telegramData(user) })"));
    assert.ok(script.includes("signIn('/api/auth/google', { idToken: response.credential })"));
});

test('Telegram\'s user goes as strings without empty fields; the refresh token and the refusal are read', () => {
    assert.deepEqual(telegramData({ id: 5, first_name: 'Ali', last_name: null, photo_url: undefined, auth_date: 17, hash: 'h' }),
        { id: '5', first_name: 'Ali', auth_date: '17', hash: 'h' });
    assert.equal(refreshTokenOf({ accessToken: 'a', refreshToken: 'r' }), 'r');
    assert.equal(refreshTokenOf({ accessToken: 'a' }), null);
    assert.equal(refreshTokenOf(null), null);
    assert.equal(failureOf({ detail: 'Telegram tasdig‘i eskirgan.' }), 'Telegram tasdig‘i eskirgan.');
    assert.equal(failureOf(null), 'Kirish tasdiqlanmadi. Qayta urinib ko‘ring.');
});

test('the page reloads only when the app has already shown its own sign-in screen', () => {
    const doc = selector => ({ querySelector: s => (s === 'main.signin-page:not(.boot)' && selector) ? {} : null });
    assert.equal(needsReload(doc(true)), true);
    assert.equal(needsReload(doc(false)), false);
});

test('the home page shows the Telegram button first and the taster second, and starts the script', () => {
    const bootHtml = index.slice(index.indexOf('<div id="app">'), index.indexOf('<div id="blazor-error-ui">'));
    const landing = bootHtml.slice(bootHtml.indexOf('<div class="boot-landing">'));
    assert.ok(landing.indexOf('id="boot-telegram"') < landing.indexOf('/sinab?src=bosh'));
    assert.match(bootHtml, /<button id="boot-telegram" class="btn btn-primary block big" type="button" disabled><span>Telegram orqali kirish<\/span>/);
    assert.match(index, /import \{ start \} from '\.\/js\/boot-signin\.js';\s*start\(\);/);
    assert.match(boot, /<button class="btn btn-primary block big" type="button" disabled><span>Telegram orqali kirish<\/span>/);
});

test('the loading line shows how far the first download is', () => {
    assert.match(index, /Yuklanmoqda…<span class="boot-percent" aria-hidden="true"><\/span><\/p>\s*<div class="boot-bar" aria-hidden="true"><span><\/span><\/div>/);
    assert.match(css, /\.boot-percent::before \{ content: var\(--blazor-load-percentage-text, ""\);/);
    assert.match(css, /width: var\(--blazor-load-percentage, 0%\)/);
});
