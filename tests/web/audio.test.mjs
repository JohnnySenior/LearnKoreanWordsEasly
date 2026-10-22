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
