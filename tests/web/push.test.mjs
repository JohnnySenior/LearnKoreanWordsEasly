// L-63: Web Push on the device (push.js) and in the service workers (push and notificationclick).
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';

const web = new URL('../../src/Lugat.Web/wwwroot/', import.meta.url);
const pushSource = await readFile(new URL('js/push.js', web), 'utf8');
const push = await import('data:text/javascript;base64,' + Buffer.from(pushSource).toString('base64'));

function browser({ ua = 'Mozilla/5.0 (Linux; Android 14) Chrome/130', standalone = false, permission = 'default', subscribed = null, ask = 'granted' } = {}) {
    let subscription = subscribed;
    const asked = [];
    const reg = {
        pushManager: {
            getSubscription: async () => subscription,
            subscribe: async options => {
                asked.push(options);
                subscription = { endpoint: 'https://push.example/new', toJSON: () => ({ endpoint: 'https://push.example/new', keys: { p256dh: 'P', auth: 'A' } }), unsubscribe: async () => { subscription = null; return true; } };
                return subscription;
            },
        },
    };
    const win = {
        navigator: { userAgent: ua, platform: 'Linux', maxTouchPoints: 0, standalone, serviceWorker: { ready: Promise.resolve(reg) } },
        matchMedia: q => ({ matches: standalone && q.includes('standalone') }),
        PushManager: function () { },
        Notification: { permission, requestPermission: async () => ask },
    };
    return { win, asked, get subscription() { return subscription; } };
}

test('an iPhone in a Safari tab is told to install the app; installed it can subscribe', async () => {
    const iphone = 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) Safari/604.1';
    assert.equal(await push.state(browser({ ua: iphone }).win), 'install');
    assert.equal(await push.state(browser({ ua: iphone, standalone: true }).win), 'ready');
});

test('the state says denied, ready or subscribed; no permission is asked for it', async () => {
    let asked = 0;
    const b = browser({ permission: 'denied' });
    b.win.Notification.requestPermission = async () => { asked++; return 'granted'; };
    assert.equal(await push.state(b.win), 'denied');
    assert.equal(await push.state(browser().win), 'ready');
    assert.equal(await push.state(browser({ subscribed: { endpoint: 'x' } }).win), 'subscribed');
    assert.equal(asked, 0);
});

test('subscribe asks once, sends the VAPID key as bytes and returns the endpoint and keys', async () => {
    const b = browser();
    const result = await push.subscribe('AQID', b.win); // base64url of 01 02 03
    assert.deepEqual(result, { endpoint: 'https://push.example/new', p256dh: 'P', auth: 'A' });
    assert.equal(b.asked[0].userVisibleOnly, true);
    assert.deepEqual([...b.asked[0].applicationServerKey], [1, 2, 3]);
    assert.equal(await push.unsubscribe(b.win), 'https://push.example/new');
    assert.equal(b.subscription, null);
});

test('a refused or dismissed permission is reported, not thrown', async () => {
    assert.deepEqual(await push.subscribe('AQID', browser({ ask: 'denied' }).win), { error: 'denied' });
    assert.deepEqual(await push.subscribe('AQID', browser({ ask: 'default' }).win), { error: 'dismissed' });
});

for (const file of ['service-worker.published.js', 'service-worker.js']) {
    test(`${file}: a push shows the reminder and a tap opens practice`, async () => {
        const source = await readFile(new URL(file, web), 'utf8');
        const handlers = {}, shown = [], opened = [], focused = [];
        let windows = [];
        const self = {
            origin: 'https://koreyscha.com', location: { origin: 'https://koreyscha.com' },
            importScripts: () => { self.assetsManifest = { version: 'v', assets: [] }; },
            addEventListener: (type, h) => { handlers[type] = h; },
            skipWaiting: () => { }, clients: {
                claim: async () => { },
                matchAll: async () => windows,
                openWindow: async url => { opened.push(url); },
            },
            registration: { showNotification: async (title, options) => { shown.push({ title, options }); } },
        };
        vm.runInContext(source, vm.createContext({ self, URL, Response, console, caches: {}, fetch: async () => new Response('') }));
        const run = async (type, event) => { let p; handlers[type]({ ...event, waitUntil: x => { p = x; } }); await p; };

        await run('push', { data: { json: () => ({ title: 'Koreyscha', body: 'Bugun 3 ta so‘zni takrorlash vaqti. ~1 daqiqa.', url: '/takrorlash', tag: 'lugat-reminder' }) } });
        assert.equal(shown[0].title, 'Koreyscha');
        assert.equal(shown[0].options.body, 'Bugun 3 ta so‘zni takrorlash vaqti. ~1 daqiqa.');
        assert.equal(shown[0].options.tag, 'lugat-reminder');
        assert.equal(shown[0].options.icon, '/icons/icon-192.png');

        const notification = { data: shown[0].options.data, close: () => { } };
        await run('notificationclick', { notification });
        assert.deepEqual(opened, ['https://koreyscha.com/takrorlash']); // no window open: a new one

        windows = [{ url: 'https://koreyscha.com/bolimlar', focus: async function () { focused.push(this.url); return this; }, navigate: async url => { focused.push(url); } }];
        await run('notificationclick', { notification });
        assert.deepEqual(focused, ['https://koreyscha.com/bolimlar', 'https://koreyscha.com/takrorlash']); // the open app, moved to practice
        assert.equal(opened.length, 1);
    });
}
