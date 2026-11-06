import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../../src/Lugat.Web/wwwroot/js/sinab.js', import.meta.url), 'utf8');
const { createPlayer, clipsOf, grade, rememberSource, SOURCE_KEY } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

function fakeAudio() {
    const handlers = new Map();
    return {
        src: '', ended: false, error: null, paused: true,
        addEventListener: (name, fn) => handlers.set(name, fn),
        load() { this.ended = false; this.error = null; },
        async play() { this.paused = false; },
        pause() { this.paused = true; },
        fire(name) { handlers.get(name)?.(); },
    };
}

const tick = () => new Promise(r => setTimeout(r, 0));

test('the player moves on only when a recording ends, and preloads the next one', async () => {
    const audio = fakeAudio(), loads = [], changes = [];
    const player = createPlayer({ audio, count: 3, load: async i => { loads.push(i); return 'blob:' + i; }, onChange: s => changes.push(s) });
    assert.equal(await player.play(0), true);
    assert.equal(audio.src, 'blob:0');
    await tick();
    assert.deepEqual(loads, [0, 1]); // the next clip is fetched while this one plays

    player.pause();
    audio.fire('ended'); // a paused clip that reports ended without being at its end does not advance
    assert.equal(audio.src, 'blob:0');

    await player.resume();
    audio.ended = true; audio.fire('ended');
    await tick();
    assert.equal(audio.src, 'blob:1');
    audio.ended = true; audio.fire('ended');
    await tick();
    assert.equal(audio.src, 'blob:2');
    audio.ended = true; audio.fire('ended');
    await tick();
    assert.equal(player.state.done, true);
    assert.ok(changes.at(-1).done);
});

test('a recording that cannot be loaded stops on that step with an error', async () => {
    const audio = fakeAudio(), changes = [];
    const player = createPlayer({ audio, count: 2, load: async () => null, onChange: s => changes.push(s) });
    assert.equal(await player.play(0), false);
    assert.ok(changes.at(-1).error);
    assert.equal(player.state.index, 0);
});

test('clips follow the board: the Korean word, then each step, parts kept together', () => {
    const clips = clipsOf([
        { koreanAudioUrl: '/k0', beats: [{ audioUrl: '/a', partUrls: ['/a/0', '/a/1'] }, { audioUrl: null }] },
        { koreanAudioUrl: null, beats: [{ audioUrl: '/b' }] },
    ]);
    assert.deepEqual(clips, [
        { word: 0, beat: -1, urls: ['/k0'] },
        { word: 0, beat: 0, urls: ['/a/0', '/a/1'] },
        { word: 1, beat: 0, urls: ['/b'] },
    ]);
});

test('a trap answer is graded against the right option, wherever it stands', () => {
    assert.deepEqual(grade([{ right: false }, { right: true }], 1), { right: 1, correct: true });
    assert.deepEqual(grade([{ right: true }, { right: false }], 1), { right: 0, correct: false });
});

test('the source is kept once and the visit is posted only when none was kept before', () => {
    const store = new Map();
    const storage = { getItem: k => store.get(k) ?? null, setItem: (k, v) => store.set(k, v) };
    const posted = [];
    assert.equal(rememberSource(storage, 'sinab-ig', v => posted.push(v)), true);
    assert.equal(store.get(SOURCE_KEY), 'sinab-ig');
    assert.equal(rememberSource(storage, 'sinab-soz', v => posted.push(v)), false);
    assert.deepEqual(posted, ['sinab-ig']);
    assert.equal(store.get(SOURCE_KEY), 'sinab-ig');

    const blocked = { getItem: () => { throw new Error('blocked'); }, setItem: () => { throw new Error('blocked'); } };
    assert.equal(rememberSource(blocked, 'sinab', v => posted.push(v)), false); // storage off: the page still works
    assert.deepEqual(posted, ['sinab-ig']);
});

/** A word page (L-65) as the browser sees it: one trap question, no #start link and no taster data. */
function wordPageDom() {
    const node = (props = {}) => {
        const listeners = {};
        return {
            hidden: true, disabled: false, textContent: '', dataset: {}, ...props,
            classList: { set: new Set(), add(c) { this.set.add(c); }, contains(c) { return this.set.has(c); } },
            addEventListener(name, fn) { listeners[name] = fn; },
            click() { listeners.click?.(); },
        };
    };
    const buttons = [node({ dataset: { right: '0' } }), node({ dataset: { right: '1' } })];
    const verdict = node(), why = node();
    const trap = node({
        querySelectorAll: s => (s === '.opt' ? buttons : []),
        querySelector: s => (s === '.verdict' ? verdict : s === '.why' ? why : null),
    });
    return {
        buttons, verdict, why,
        document: { getElementById: () => null, querySelectorAll: s => (s === '.trap' ? [trap] : []) },
    };
}

test('on a word page the trap works and no visit is posted, since there is no #start link', async () => {
    const dom = wordPageDom(), requests = [], stored = new Map();
    const saved = { document: globalThis.document, fetch: globalThis.fetch, window: globalThis.window };
    globalThis.document = dom.document;
    globalThis.fetch = (url, init) => { requests.push(url); return Promise.resolve({ ok: true }); };
    globalThis.window = { localStorage: { getItem: k => stored.get(k) ?? null, setItem: (k, v) => stored.set(k, v) } };
    try {
        // A fresh copy of the module, so its page code runs against this document.
        await import('data:text/javascript;base64,' + Buffer.from(source + '\n// word page').toString('base64'));
        dom.buttons[0].click();
        await tick();
    } finally {
        Object.assign(globalThis, saved);
    }

    assert.deepEqual(requests, []); // nothing to /api/visits, nothing at all
    assert.equal(stored.size, 0);   // and no source kept on the device
    assert.equal(dom.verdict.hidden, false);
    assert.equal(dom.why.hidden, false);
    assert.ok(dom.buttons[1].classList.contains('right'));
    assert.ok(dom.buttons[0].classList.contains('wrong'));
});
