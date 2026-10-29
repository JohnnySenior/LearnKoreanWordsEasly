// Presentation-only audio controller. A recording's ended event is the only automatic advance signal.
export function create(root, target) {
    const audio = root.querySelector('audio');
    const cache = new Map();
    const pending = new Map();
    let active = null;
    let disposed = false;
    const silence = 'data:audio/wav;base64,UklGRmQBAABXQVZFZm10IBAAAAABAAEAQB8AAIA+AAACABAAZGF0YUABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==';
    const onEnd = () => {
        const id = active;
        if (!disposed && id !== null && audio.ended && !audio.seeking && audio.currentSrc === cache.get(id)) {
            active = null;
            target.invokeMethodAsync('OnIntroductionEnded', id).catch(() => {});
        }
    };
    const onError = () => {
        if (!disposed && active !== null && audio.error) {
            const id = active;
            active = null;
            target.invokeMethodAsync('OnIntroductionError', id).catch(() => {});
        }
    };
    audio.addEventListener('ended', onEnd);
    audio.addEventListener('error', onError);
    return {
        put(id, bytes) {
            if (disposed || cache.has(id)) return;
            cache.set(id, URL.createObjectURL(new Blob([bytes], { type: 'audio/mpeg' })));
        },
        putPart(id, index, bytes) {
            if (disposed || cache.has(id)) return;
            if (!pending.has(id)) pending.set(id, []);
            pending.get(id)[index] = bytes;
        },
        async join(id, count) {
            if (disposed) return false;
            if (cache.has(id)) return true;
            const parts = pending.get(id);
            pending.delete(id);
            if (!parts || parts.length !== count || parts.some(p => !p)) return false;
            try {
                const wav = await joinParts(parts);
                if (disposed) return false;
                cache.set(id, URL.createObjectURL(new Blob([wav], { type: 'audio/wav' })));
                return true;
            } catch { return false; }
        },
        async prime() {
            if (disposed || audio.src) return;
            audio.src = silence;
            try { await audio.play(); audio.pause(); } catch { }
        },
        async play(id, rate) {
            if (disposed || !cache.has(id)) return false;
            active = null;
            audio.pause();
            const url = cache.get(id);
            if (audio.src !== url) { audio.src = url; audio.load(); }
            audio.playbackRate = rate;
            active = id;
            try { await audio.play(); return true; }
            catch { if (active === id) active = null; return false; }
        },
        pause() { audio.pause(); },
        rewind() { audio.currentTime = 0; },
        stop() { active = null; audio.pause(); audio.removeAttribute('src'); audio.load(); },
        rate(value) { audio.playbackRate = value; },
        dispose() {
            disposed = true;
            active = null;
            audio.pause();
            audio.removeEventListener('ended', onEnd);
            audio.removeEventListener('error', onError);
            audio.removeAttribute('src'); audio.load();
            for (const url of cache.values()) URL.revokeObjectURL(url);
            cache.clear();
            pending.clear();
        },
    };
}

// The narrator's Uzbek and the Korean voice's words arrive as separate MP3s. They are decoded at one rate, their
// leading and trailing silence trimmed, levelled to the narrator's loudness, and joined with a short natural breath
// and tiny fades (no clicks) into one WAV, so the beat plays as a single seamless recording.
const RATE = 24000;
async function joinParts(parts) {
    const Offline = window.OfflineAudioContext || window.webkitOfflineAudioContext;
    const context = new Offline(1, RATE, RATE);
    const clips = [];
    for (const bytes of parts) {
        const data = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
        const buffer = await new Promise((resolve, reject) => {
            const result = context.decodeAudioData(data, resolve, reject);
            if (result && result.then) result.then(resolve, reject);
        });
        clips.push(trim(mono(buffer)));
    }
    const loudest = clips.reduce((a, b) => (b.length > a.length ? b : a));
    const target = rms(loudest);
    const gap = Math.round(RATE * 0.05), fade = Math.round(RATE * 0.008);
    const total = clips.reduce((sum, c) => sum + c.length, 0) + gap * (clips.length - 1);
    const out = new Float32Array(total);
    let at = 0;
    clips.forEach((clip, n) => {
        const level = rms(clip);
        let gain = level > 0 && target > 0 ? Math.min(3, Math.max(0.5, target / level)) : 1;
        const peak = peakOf(clip);
        if (peak * gain > 0.97) gain = 0.97 / peak;
        for (let i = 0; i < clip.length; i++) {
            const edge = Math.min(1, i / fade, (clip.length - 1 - i) / fade);
            out[at + i] = clip[i] * gain * edge;
        }
        at += clip.length + (n < clips.length - 1 ? gap : 0);
    });
    return wav(out);
}
function mono(buffer) {
    const out = new Float32Array(buffer.length);
    for (let c = 0; c < buffer.numberOfChannels; c++) {
        const data = buffer.getChannelData(c);
        for (let i = 0; i < data.length; i++) out[i] += data[i] / buffer.numberOfChannels;
    }
    return out;
}
function peakOf(data) {
    let peak = 0;
    for (let i = 0; i < data.length; i++) peak = Math.max(peak, Math.abs(data[i]));
    return peak;
}
// Thresholds follow each recording's own peak: services deliver at different levels.
function trim(data) {
    const threshold = Math.max(0.002, peakOf(data) * 0.04), margin = Math.round(RATE * 0.03);
    let start = 0, end = data.length - 1;
    while (start < data.length && Math.abs(data[start]) < threshold) start++;
    while (end > start && Math.abs(data[end]) < threshold) end--;
    if (start >= end) return data;
    return data.subarray(Math.max(0, start - margin), Math.min(data.length, end + margin + 1));
}
function rms(data) {
    const floor = peakOf(data) * 0.1;
    let sum = 0, count = 0;
    for (let i = 0; i < data.length; i++) { if (Math.abs(data[i]) > floor) { sum += data[i] * data[i]; count++; } }
    return count ? Math.sqrt(sum / count) : 0;
}
function wav(samples) {
    const buffer = new ArrayBuffer(44 + samples.length * 2);
    const view = new DataView(buffer);
    const text = (at, s) => { for (let i = 0; i < s.length; i++) view.setUint8(at + i, s.charCodeAt(i)); };
    text(0, 'RIFF'); view.setUint32(4, 36 + samples.length * 2, true); text(8, 'WAVE');
    text(12, 'fmt '); view.setUint32(16, 16, true); view.setUint16(20, 1, true); view.setUint16(22, 1, true);
    view.setUint32(24, RATE, true); view.setUint32(28, RATE * 2, true); view.setUint16(32, 2, true); view.setUint16(34, 16, true);
    text(36, 'data'); view.setUint32(40, samples.length * 2, true);
    for (let i = 0; i < samples.length; i++) view.setInt16(44 + i * 2, Math.max(-1, Math.min(1, samples[i])) * 0x7fff, true);
    return buffer;
}
