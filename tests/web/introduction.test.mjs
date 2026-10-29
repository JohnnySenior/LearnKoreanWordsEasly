import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
const source = await readFile(new URL('../../src/Lugat.Web/wwwroot/js/introduction.js', import.meta.url), 'utf8');
const { create } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
function fixture() {
    const handlers = new Map(), calls = [];
    const audio = {
        src: '', currentSrc: '', currentTime: 0, ended: false, seeking: false, error: null, paused: true,
        addEventListener: (name, fn) => handlers.set(name, fn), removeEventListener: name => handlers.delete(name),
        pause() { this.paused = true; },
        load() { this.currentSrc = this.src; this.currentTime = 0; this.ended = false; this.error = null; },
        async play() { this.currentSrc = this.src; this.paused = false; },
        removeAttribute() { this.src = ''; },
    };
    const target = { async invokeMethodAsync(...args) { calls.push(args); } };
    const player = create({ querySelector: () => audio }, target);
    return { player, audio, calls, event: name => handlers.get(name)?.() };
}
test('narration advances only on a complete recording, never on pause, waiting or a stale ended event', async () => {
    const { player, audio, calls, event } = fixture();
    player.put(1, new Uint8Array([1]));
    assert.equal(await player.play(1, 1), true);
    audio.currentTime = 10;
    player.pause(); event('waiting'); event('ended');
    assert.deepEqual(calls, []);
    assert.equal(audio.currentTime, 10);
    await player.play(1, 1.25);
    assert.equal(audio.currentTime, 10); // resume does not truncate or restart the explanation
    assert.equal(audio.playbackRate, 1.25);
    audio.ended = true; event('ended'); event('ended');
    assert.deepEqual(calls, [['OnIntroductionEnded', 1]]);
    player.dispose();
});
test('media failure reports the exact clip and never advances to the next word', async () => {
    const { player, audio, calls, event } = fixture();
    player.put(2, new Uint8Array([2])); await player.play(2, 1);
    audio.error = { code: 3 }; event('error'); event('ended');
    assert.deepEqual(calls, [['OnIntroductionError', 2]]);
    player.dispose();
});
test('manual navigation and disposal cancel old callbacks and release generated object URLs', async () => {
    const { player, audio, calls, event } = fixture();
    player.put(1, new Uint8Array([1])); player.put(2, new Uint8Array([2]));
    await player.play(1, 1); player.stop(); audio.ended = true; event('ended');
    assert.deepEqual(calls, []);
    await player.play(2, 1); event('ended'); assert.deepEqual(calls, []);
    player.dispose(); audio.ended = true; event('ended');
    assert.deepEqual(calls, []);
    assert.equal(await player.play(2, 1), false);
});
test('a browser autoplay rejection pauses for a user retry and never reports completion', async () => {
    const { player, audio, calls } = fixture();
    player.put(0, new Uint8Array([0]));
    audio.play = async () => { throw new Error('gesture required'); };
    assert.equal(await player.play(0, 1), false);
    assert.deepEqual(calls, []);
    player.dispose();
});

test('first-tap priming uses decodable PCM samples, rather than a zero-frame WAV', async () => {
    const { player, audio } = fixture();
    await player.prime();
    const bytes = Buffer.from(audio.src.split(',')[1], 'base64');
    assert.equal(bytes.subarray(0, 4).toString(), 'RIFF');
    assert.equal(bytes.subarray(8, 12).toString(), 'WAVE');
    const dataLength = bytes.readUInt32LE(40);
    assert.ok(dataLength > 0, 'Safari must receive actual silent samples to start the element');
    assert.equal(bytes.length, 44 + dataLength);
    assert.equal(audio.paused, true);
    player.dispose();
});
