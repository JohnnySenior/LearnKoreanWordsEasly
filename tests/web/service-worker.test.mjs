import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';

const source = await readFile(new URL('../../src/Lugat.Web/wwwroot/service-worker.published.js', import.meta.url), 'utf8');
const origin = 'https://lugat.example';

// Runs the published worker in a fake service worker scope with an in-memory Cache Storage.
function load({ version = 'v2', stores = {}, network = url => new Response('net:' + url) } = {}) {
    const handlers = {};
    const fetched = [];
    const storage = new Map(Object.entries(stores).map(([name, entries]) => [name, new Map(entries)]));
    const keyOf = request => new URL(typeof request === 'string' ? request : request.url, origin + '/').href;
    const cacheFor = name => {
        if (!storage.has(name)) storage.set(name, new Map());
        const entries = storage.get(name);
        return {
            match: async request => entries.get(keyOf(request))?.clone(),
            put: async (request, response) => { entries.set(keyOf(request), response); },
            addAll: async requests => requests.forEach(r => entries.set(keyOf(r), new Response('asset'))),
            keys: async () => [...entries.keys()],
        };
    };
    const self = {
        origin,
        skipped: false,
        importScripts: () => {
            self.assetsManifest = {
                version,
                assets: [
                    { url: 'index.html', hash: 'sha256-a' },
                    { url: '_framework/dotnet.js', hash: 'sha256-b' },
                    { url: '_framework/icudt_CJK.abc.dat', hash: 'sha256-c' },
                    { url: 'service-worker.js', hash: 'sha256-d' },
                    { url: 'css/app.css', hash: 'sha256-e' },
                ],
            };
        },
        addEventListener: (type, handler) => { handlers[type] = handler; },
        skipWaiting: () => { self.skipped = true; },
    };
    const context = vm.createContext({
        self, URL, Response, console,
        Request: class { constructor(url, init) { this.url = new URL(url, origin + '/').href; Object.assign(this, init); } },
        caches: {
            open: async name => cacheFor(name),
            keys: async () => [...storage.keys()],
            delete: async name => storage.delete(name),
        },
        fetch: async request => { fetched.push(request.url); return network(request.url); },
    });
    vm.runInContext(source, context);

    const dispatch = async (type, data) => {
        let waited, responded;
        handlers[type]({ ...data, waitUntil: p => { waited = p; }, respondWith: p => { responded = p; } });
        await waited;
        return responded;
    };
    return { self, storage, fetched, dispatch };
}

const request = (path, mode = 'cors', method = 'GET') => ({ url: origin + path, mode, method });

test('install caches the app shell with hashes, without the worker itself or the ICU files', async () => {
    const sw = load();
    await sw.dispatch('install');
    assert.deepEqual([...sw.storage.get('lugat-shell-v2').keys()].sort(), [
        `${origin}/_framework/dotnet.js`, `${origin}/css/app.css`, `${origin}/index.html`,
    ]);
});

test('public word pages, the sitemap and robots.txt go to the server, not the app shell', async () => {
    const sw = load({ stores: { 'lugat-shell-v2': [[`${origin}/index.html`, new Response('shell')]] } });
    assert.equal(await sw.dispatch('fetch', { request: request('/soz/sarang', 'navigate') }), undefined);
    assert.equal(await sw.dispatch('fetch', { request: request('/soz', 'navigate') }), undefined);
    assert.equal(await sw.dispatch('fetch', { request: request('/sitemap.xml') }), undefined);
    assert.equal(await sw.dispatch('fetch', { request: request('/robots.txt') }), undefined);
    // An app page that only starts with the same letters is still the shell.
    assert.equal(await (await sw.dispatch('fetch', { request: request('/sozlarim', 'navigate') })).text(), 'shell');
});

test('API calls are never answered by the worker', async () => {
    const sw = load({ stores: { 'lugat-shell-v2': [[`${origin}/api/me`, new Response('stale')]] } });
    assert.equal(await sw.dispatch('fetch', { request: request('/api/me') }), undefined);
    assert.equal(await sw.dispatch('fetch', { request: request('/api/me/backup', 'navigate') }), undefined);
    assert.equal(await sw.dispatch('fetch', { request: request('/api/study/answers', 'cors', 'POST') }), undefined);
    assert.equal(await sw.dispatch('fetch', { request: { url: 'https://fonts.googleapis.com/css2', mode: 'no-cors', method: 'GET' } }), undefined);
});

test('every page of the app opens from the cached index.html', async () => {
    const sw = load({ stores: { 'lugat-shell-v2': [[`${origin}/index.html`, new Response('shell')]] } });
    const response = await sw.dispatch('fetch', { request: request('/bolimlar/abc', 'navigate') });
    assert.equal(await response.text(), 'shell');
    assert.deepEqual(sw.fetched, []);
});

test('an asset of this version missing from the cache is fetched once and kept', async () => {
    const sw = load();
    const url = `${origin}/_framework/icudt_CJK.abc.dat`;
    assert.equal(await (await sw.dispatch('fetch', { request: request('/_framework/icudt_CJK.abc.dat') })).text(), 'net:' + url);
    assert.ok(sw.storage.get('lugat-shell-v2').has(url));
    await sw.dispatch('fetch', { request: request('/js/other.js') });
    assert.ok(!sw.storage.get('lugat-shell-v2').has(`${origin}/js/other.js`));
});

test('activating a new version deletes the old shell caches only', async () => {
    const sw = load({ stores: { 'lugat-shell-v1': [], 'lugat-shell-v2': [], 'other-cache': [] } });
    await sw.dispatch('activate');
    assert.deepEqual([...sw.storage.keys()].sort(), ['lugat-shell-v2', 'other-cache']);
});

test('the waiting version takes over only when the page asks', async () => {
    const sw = load();
    await sw.dispatch('message', { data: { type: 'other' } });
    assert.equal(sw.self.skipped, false);
    await sw.dispatch('message', { data: { type: 'SKIP_WAITING' } });
    assert.equal(sw.self.skipped, true);
});
