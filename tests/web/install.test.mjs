import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../../src/Lugat.Web/wwwroot/js/app.js', import.meta.url), 'utf8');
const app = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

function device({ standalone = false, userAgent = 'Mozilla/5.0 (Linux; Android 14) Chrome/130', platform = 'Linux', touch = 5, prompt } = {}) {
    globalThis.window = {
        matchMedia: () => ({ matches: standalone }),
        navigator: {},
        __installPrompt: prompt,
    };
    Object.defineProperty(globalThis, 'navigator', { configurable: true, value: { userAgent, platform, maxTouchPoints: touch } });
}

test('the install guide matches the device', () => {
    device({ standalone: true });
    assert.equal(app.installState(), 'installed');
    device({ prompt: {} });
    assert.equal(app.installState(), 'prompt');
    device({ userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) Safari/604.1' });
    assert.equal(app.installState(), 'ios');
    device({ userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Safari/605.1.15', platform: 'MacIntel', touch: 5 });
    assert.equal(app.installState(), 'ios', 'iPadOS reports itself as a Mac');
    device();
    assert.equal(app.installState(), 'manual');
});

test("Chrome's install offer is used once", async () => {
    let shown = 0;
    device({ prompt: { prompt: async () => { shown++; }, userChoice: Promise.resolve({ outcome: 'accepted' }) } });
    assert.equal(await app.promptInstall(), true);
    assert.equal(await app.promptInstall(), false);
    assert.equal(shown, 1);
    device({ prompt: { prompt: async () => { }, userChoice: Promise.resolve({ outcome: 'dismissed' }) } });
    assert.equal(await app.promptInstall(), false);
    assert.equal(app.installState(), 'manual');
});

test('a link opened inside an app is sent to Safari or Chrome first (L-50)', () => {
    device({ userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148 Instagram 350.0' });
    assert.equal(app.installState(), 'inapp-ios:Instagram');
    device({ userAgent: 'Mozilla/5.0 (Linux; Android 14; wv) AppleWebKit/537.36 Chrome/130 Mobile Safari/537.36 [FBAN/EMA;FBAV/400.0]' });
    assert.equal(app.installState(), 'inapp-android:Facebook');
    device({ userAgent: 'Mozilla/5.0 (Linux; Android 14; SM-A146B Build/UP1A; wv) AppleWebKit/537.36 Chrome/130 Mobile Safari/537.36 Telegram-Android/11.2' });
    assert.equal(app.installState(), 'inapp-android:Telegram');
    device({ userAgent: 'Mozilla/5.0 (Linux; Android 14; wv) AppleWebKit/537.36 Chrome/130 Mobile Safari/537.36' });
    assert.equal(app.installState(), 'inapp-android:', 'an unknown app still gets the steps');
    device({ userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148' });
    assert.equal(app.installState(), 'inapp-ios:', 'no "Safari/" token: an app view');
    device({ userAgent: 'Mozilla/5.0 (Linux; Android 14) Chrome/130 Mobile Safari/537.36 [FBAN/EMA;FBAV/400.0]', prompt: {} });
    assert.equal(app.installState(), 'inapp-android:Facebook', "an in-app view never offers Chrome's install");
});

test('other iPhone browsers and Samsung Internet get their own steps', () => {
    device({ userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/130.0 Mobile/15E148 Safari/604.1' });
    assert.equal(app.installState(), 'ios-other');
    device({ userAgent: 'Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 SamsungBrowser/26.0 Chrome/122 Mobile Safari/537.36' });
    assert.equal(app.installState(), 'samsung');
});

