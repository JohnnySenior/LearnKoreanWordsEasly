// Audio/microphone probes for the stage-1 device check (spec AU01, AU02).
const candidates = ['audio/webm;codecs=opus', 'audio/mp4', 'audio/ogg;codecs=opus', 'audio/webm', 'audio/aac'];
let lastUrl = null;

export function supportedMimeTypes() {
    if (typeof MediaRecorder === 'undefined') return [];
    return candidates.filter(t => MediaRecorder.isTypeSupported(t));
}

export async function playTone() {
    try {
        const Ctx = window.AudioContext || window.webkitAudioContext;
        const ctx = new Ctx();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.frequency.value = 440;
        gain.gain.value = 0.15;
        osc.connect(gain).connect(ctx.destination);
        osc.start();
        await new Promise(r => setTimeout(r, 600));
        osc.stop();
        await ctx.close();
        return { ok: true, error: null };
    } catch (e) {
        return { ok: false, error: String(e && e.name || e) };
    }
}

export async function record(ms) {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        return { ok: false, error: 'getUserMedia mavjud emas', mimeType: null, bytes: 0, durationMs: 0 };
    }
    if (typeof MediaRecorder === 'undefined') {
        return { ok: false, error: 'MediaRecorder mavjud emas', mimeType: null, bytes: 0, durationMs: 0 };
    }
    let stream;
    try {
        stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch (e) {
        return { ok: false, error: String(e && e.name || e), mimeType: null, bytes: 0, durationMs: 0 };
    }
    try {
        const types = supportedMimeTypes();
        const rec = types.length ? new MediaRecorder(stream, { mimeType: types[0] }) : new MediaRecorder(stream);
        const chunks = [];
        rec.ondataavailable = e => { if (e.data && e.data.size) chunks.push(e.data); };
        const started = performance.now();
        const done = new Promise(r => { rec.onstop = r; });
        rec.start();
        await new Promise(r => setTimeout(r, ms));
        rec.stop();
        await done;
        const blob = new Blob(chunks, { type: rec.mimeType });
        if (lastUrl) URL.revokeObjectURL(lastUrl);
        lastUrl = URL.createObjectURL(blob);
        return { ok: blob.size > 0, error: blob.size > 0 ? null : 'bo‘sh yozuv', mimeType: rec.mimeType, bytes: blob.size, durationMs: Math.round(performance.now() - started) };
    } catch (e) {
        return { ok: false, error: String(e && e.name || e), mimeType: null, bytes: 0, durationMs: 0 };
    } finally {
        stream.getTracks().forEach(t => t.stop());
    }
}

export async function playLastRecording() {
    if (!lastUrl) return { ok: false, error: 'yozuv yo‘q' };
    try {
        await new Audio(lastUrl).play();
        return { ok: true, error: null };
    } catch (e) {
        return { ok: false, error: String(e && e.name || e) };
    }
}

// Pronunciation (spec AU01, D-29). The recording is fetched ahead (when the card appears) and kept as a blob URL,
// so pressing the button plays at once: iOS only allows audio started directly by a tap.
const speech = new Map();
let current = null;

export function loadSpeech(key, bytes) {
    if (speech.has(key)) return true;
    const url = URL.createObjectURL(new Blob([bytes], { type: 'audio/mpeg' }));
    speech.set(key, url);
    if (speech.size > 60) {
        const [oldKey, oldUrl] = speech.entries().next().value;
        URL.revokeObjectURL(oldUrl);
        speech.delete(oldKey);
    }
    return true;
}

export function hasSpeech(key) {
    return speech.has(key);
}

export async function playSpeech(key, rate) {
    const url = speech.get(key);
    if (!url) return { ok: false, error: 'yuklanmagan' };
    try {
        if (current) current.pause();
        current = new Audio(url);
        current.playbackRate = rate;
        current.preservesPitch = true;
        current.webkitPreservesPitch = true;
        await current.play();
        return { ok: true, error: null };
    } catch (e) {
        return { ok: false, error: String(e && e.name || e) };
    }
}

// Fallback when the server has no voice: the device's own Korean voice, if it has one.
export function speakWithDevice(text, rate) {
    try {
        if (!('speechSynthesis' in window)) return { ok: false, error: 'qurilmada ovoz yo‘q' };
        const voices = window.speechSynthesis.getVoices();
        const voice = voices.find(v => v.lang && v.lang.toLowerCase().startsWith('ko'));
        const u = new SpeechSynthesisUtterance(text);
        u.lang = 'ko-KR';
        if (voice) u.voice = voice;
        u.rate = rate;
        window.speechSynthesis.cancel();
        window.speechSynthesis.speak(u);
        return { ok: true, error: null };
    } catch (e) {
        return { ok: false, error: String(e && e.name || e) };
    }
}

// Speaking practice (spec AU02, D-32): records raw PCM with the Web Audio API and encodes 16 kHz mono WAV, the
// format Azure's short-audio recognition accepts on every phone (iOS MediaRecorder only gives AAC).
let mic = null;

export async function startSpeaking(maxMs) {
    if (mic) await stopSpeaking(true);
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        return { ok: false, error: 'Bu qurilmada mikrofon ishlatib bo‘lmaydi.' };
    }
    let stream = null;
    let ctx = null;
    let source = null;
    let node = null;
    try {
        stream = await navigator.mediaDevices.getUserMedia({ audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true } });
        const Ctx = window.AudioContext || window.webkitAudioContext;
        ctx = new Ctx();
        if (ctx.state === 'suspended') await ctx.resume();
        source = ctx.createMediaStreamSource(stream);
        node = ctx.createScriptProcessor(4096, 1, 1);
        const chunks = [];
        node.onaudioprocess = e => chunks.push(new Float32Array(e.inputBuffer.getChannelData(0)));
        source.connect(node);
        node.connect(ctx.destination);
        const timer = setTimeout(() => { if (mic) mic.timedOut = true; }, maxMs);
        mic = { stream, ctx, source, node, chunks, timer, started: performance.now(), timedOut: false };
        return { ok: true, error: null };
    } catch (e) {
        try { node && node.disconnect(); source && source.disconnect(); } catch { }
        if (stream) stream.getTracks().forEach(t => t.stop());
        try { if (ctx && ctx.state !== 'closed') await ctx.close(); } catch { }
        const name = String(e && e.name || e);
        return { ok: false, error: name === 'NotAllowedError' ? 'Mikrofonga ruxsat berilmadi. Telegram sozlamalarida ruxsat bering.' : name };
    }
}

export function isSpeakingTimedOut() {
    return !!(mic && mic.timedOut);
}

export async function stopSpeaking(discard) {
    if (!mic) return { ok: false, error: 'yozuv yo‘q', wav: null, durationMs: 0 };
    const { stream, ctx, source, node, chunks, timer, started } = mic;
    mic = null;
    clearTimeout(timer);
    try { node.disconnect(); source.disconnect(); } catch { }
    stream.getTracks().forEach(t => t.stop());
    const rate = ctx.sampleRate;
    await ctx.close();
    if (discard) return { ok: false, error: null, wav: null, durationMs: 0 };

    const length = chunks.reduce((n, c) => n + c.length, 0);
    const all = new Float32Array(length);
    let offset = 0;
    for (const c of chunks) { all.set(c, offset); offset += c.length; }
    const pcm = downsample(all, rate, 16000);
    const wav = encodeWav(pcm, 16000);
    if (lastUrl) URL.revokeObjectURL(lastUrl);
    lastUrl = URL.createObjectURL(new Blob([wav], { type: 'audio/wav' }));
    return { ok: pcm.length > 1600, error: pcm.length > 1600 ? null : 'Yozuv juda qisqa.', wav: toBase64(wav), durationMs: Math.round(performance.now() - started) };
}

function downsample(input, from, to) {
    if (from === to) return input;
    const ratio = from / to;
    const out = new Float32Array(Math.floor(input.length / ratio));
    for (let i = 0; i < out.length; i++) {
        const start = Math.floor(i * ratio), end = Math.min(input.length, Math.floor((i + 1) * ratio));
        let sum = 0;
        for (let j = start; j < end; j++) sum += input[j];
        out[i] = end > start ? sum / (end - start) : 0;
    }
    return out;
}

function toBase64(bytes) {
    let binary = '';
    for (let i = 0; i < bytes.length; i += 0x8000) binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
    return btoa(binary);
}

function encodeWav(samples, rate) {
    const buffer = new ArrayBuffer(44 + samples.length * 2);
    const view = new DataView(buffer);
    const text = (o, s) => { for (let i = 0; i < s.length; i++) view.setUint8(o + i, s.charCodeAt(i)); };
    text(0, 'RIFF'); view.setUint32(4, 36 + samples.length * 2, true); text(8, 'WAVE');
    text(12, 'fmt '); view.setUint32(16, 16, true); view.setUint16(20, 1, true); view.setUint16(22, 1, true);
    view.setUint32(24, rate, true); view.setUint32(28, rate * 2, true); view.setUint16(32, 2, true); view.setUint16(34, 16, true);
    text(36, 'data'); view.setUint32(40, samples.length * 2, true);
    for (let i = 0; i < samples.length; i++) {
        const s = Math.max(-1, Math.min(1, samples[i]));
        view.setInt16(44 + i * 2, s < 0 ? s * 0x8000 : s * 0x7fff, true);
    }
    return new Uint8Array(buffer);
}

