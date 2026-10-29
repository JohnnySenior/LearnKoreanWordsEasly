// Presentation-only audio controller. A recording's ended event is the only automatic advance signal.
export function create(root, target) {
    const audio = root.querySelector('audio');
    const cache = new Map();
    let active = null;
    let disposed = false;
    const silence = 'data:audio/wav;base64,UklGRiQAAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQAAAAA=';
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
        },
    };
}
