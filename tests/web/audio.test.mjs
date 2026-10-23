import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../../src/Lugat.Web/wwwroot/js/audio.js', import.meta.url), 'utf8');
const audio = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

test('microphone tracks close when constructing AudioContext fails', async () => {
    let stopped = 0;
    globalThis.window = { AudioContext: class { constructor() { throw new Error('context unavailable'); } } };
    Object.defineProperty(globalThis, 'navigator', { configurable: true, value: {
        mediaDevices: { getUserMedia: async () => ({ getTracks: () => [{ stop: () => stopped++ }] }) },
    } });
    const result = await audio.startSpeaking(1000);
    assert.equal(result.ok, false);
    assert.equal(stopped, 1);
});

test('a partially initialized context and microphone close when creating the node fails', async () => {
    let stopped = 0, closed = 0, disconnected = 0;
    globalThis.window = { AudioContext: class {
        state = 'running';
        createMediaStreamSource() { return { disconnect: () => disconnected++ }; }
        createScriptProcessor() { throw new Error('node unavailable'); }
        async close() { closed++; }
    } };
    Object.defineProperty(globalThis, 'navigator', { configurable: true, value: {
        mediaDevices: { getUserMedia: async () => ({ getTracks: () => [{ stop: () => stopped++ }] }) },
    } });
    assert.equal((await audio.startSpeaking(1000)).ok, false);
    assert.deepEqual([stopped, closed, disconnected], [1, 1, 1]);
});

test('a tap before the recording arrives starts an element that later plays the recording', async () => {
    const elements = [];
    globalThis.Audio = class {
        constructor(src) { this.src = src ?? ''; this.played = []; this.paused = 0; elements.push(this); }
        play() { this.played.push(this.src); return Promise.resolve(); }
        pause() { this.paused++; }
    };

    assert.equal(audio.primeSpeech(), true); // inside the tap: silence, so iOS lets this element play later
    assert.match(elements[0].played[0], /^data:audio\/wav;base64,/);

    audio.loadSpeech('하다', new Uint8Array([1, 2, 3]));
    const result = await audio.playSpeech('하다', 0.7);

    assert.equal(result.ok, true);
    assert.equal(elements.length, 1); // the same element, not a new one that iOS would block
    assert.match(elements[0].played[1], /^blob:/);
    assert.equal(elements[0].playbackRate, 0.7);

    await audio.playSpeech('하다', 1);
    assert.equal(elements.length, 2); // an ordinary tap on a loaded recording gets its own element
    assert.equal(elements[0].paused, 1);
});
