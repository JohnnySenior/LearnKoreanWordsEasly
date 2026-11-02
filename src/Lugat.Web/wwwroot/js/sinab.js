// The sign-in-free taster /sinab (L-58): a short part of unit A-1's voiced introduction and three trap questions.
// Plain module on a server-rendered page: no Blazor, nothing is scored or saved except where the visitor came from.

/** The app's own key (MainLayout.SourceKey): the app then neither counts the visit again nor loses it at sign-in. */
export const SOURCE_KEY = 'koreyscha.src';

/** Keeps the first source only, as the app does; posts one visit when this page is where the visitor arrived. */
export function rememberSource(storage, source, post) {
    try {
        if (!source || storage.getItem(SOURCE_KEY)) return false;
        storage.setItem(SOURCE_KEY, source);
    } catch { return false; }
    post(source);
    return true;
}

/** Recordings in playing order: each word's Korean voice, then its board steps (each step may come in parts). */
export function clipsOf(words) {
    const clips = [];
    words.forEach((w, word) => {
        if (w.koreanAudioUrl) clips.push({ word, beat: -1, urls: [w.koreanAudioUrl] });
        w.beats.forEach((b, beat) => {
            const urls = b.partUrls && b.partUrls.length ? b.partUrls : b.audioUrl ? [b.audioUrl] : null;
            if (urls) clips.push({ word, beat, urls });
        });
    });
    return clips;
}

/** Which option was right and whether the chosen one is it. */
export function grade(options, chosen) {
    const right = options.findIndex(o => o.right);
    return { right, correct: chosen === right };
}

/**
 * One audio element plays the clips in turn; only a recording's own ended event moves on (never a pause or a stall),
 * and the next clip is fetched while the current one plays. load(index) resolves to a playable URL or null.
 */
export function createPlayer({ audio, count, load, onChange }) {
    const urls = new Map();
    const loading = new Map();
    let index = 0, active = null, playing = false, done = false, epoch = 0;
    const state = () => ({ index, playing, done });
    const fetchClip = i => {
        if (urls.has(i)) return Promise.resolve(urls.get(i));
        if (!loading.has(i)) loading.set(i, Promise.resolve(load(i)).then(url => { loading.delete(i); if (url) urls.set(i, url); return url; },
            () => { loading.delete(i); return null; }));
        return loading.get(i);
    };
    async function play(i) {
        const mine = ++epoch;
        index = i; done = false; active = null;
        onChange({ ...state(), loading: true });
        const url = await fetchClip(i);
        if (mine !== epoch) return false;
        if (!url) { playing = false; onChange({ ...state(), error: true }); return false; }
        if (audio.src !== url) { audio.src = url; audio.load(); }
        active = i;
        try { await audio.play(); playing = true; } catch { playing = false; active = null; onChange({ ...state(), blocked: true }); return false; }
        onChange(state());
        if (i + 1 < count) fetchClip(i + 1);
        return true;
    }
    audio.addEventListener('ended', () => {
        if (active === null || !audio.ended || active !== index) return;
        active = null; playing = false;
        if (index + 1 < count) play(index + 1);
        else { done = true; onChange(state()); }
    });
    audio.addEventListener('error', () => {
        if (active === null || !audio.error) return;
        active = null; playing = false; onChange({ ...state(), error: true });
    });
    return {
        play,
        pause() { epoch++; audio.pause(); playing = false; onChange(state()); },
        async resume() {
            if (active === index && audio.src && !audio.ended) {
                try { await audio.play(); playing = true; onChange(state()); return true; } catch { return false; }
            }
            return play(index);
        },
        get state() { return state(); },
    };
}

// ---------- the page ----------
const silence = () => {
    // 0.05 s of 8 kHz silence: iOS lets later plays start once one play came from a tap (L-54).
    const n = 400, buffer = new ArrayBuffer(44 + n * 2), v = new DataView(buffer);
    const text = (at, s) => { for (let i = 0; i < s.length; i++) v.setUint8(at + i, s.charCodeAt(i)); };
    text(0, 'RIFF'); v.setUint32(4, 36 + n * 2, true); text(8, 'WAVE'); text(12, 'fmt '); v.setUint32(16, 16, true);
    v.setUint16(20, 1, true); v.setUint16(22, 1, true); v.setUint32(24, 8000, true); v.setUint32(28, 16000, true);
    v.setUint16(32, 2, true); v.setUint16(34, 16, true); text(36, 'data'); v.setUint32(40, n * 2, true);
    return URL.createObjectURL(new Blob([buffer], { type: 'audio/wav' }));
};

function el(tag, cls, text) {
    const e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
}

function mountTaster() {
    const data = JSON.parse(document.getElementById('taster-data').textContent);
    const words = data.words, clips = clipsOf(words);
    if (!clips.length) return;
    const board = document.getElementById('board'), subtitle = document.getElementById('subtitle');
    const playBtn = document.getElementById('play'), replay = document.getElementById('replay'), error = document.getElementById('taster-error');
    const audio = new Audio();
    audio.preload = 'auto';
    let started = false, shown = null;

    async function load(i) {
        const parts = [];
        for (const url of clips[i].urls) {
            const res = await fetch(url);
            if (!res.ok) return null;
            parts.push(new Uint8Array(await res.arrayBuffer()));
        }
        if (parts.length === 1) return URL.createObjectURL(new Blob([parts[0]], { type: 'audio/mpeg' }));
        const { joinParts } = await import('./introduction.js');
        return URL.createObjectURL(new Blob([await joinParts(parts)], { type: 'audio/wav' }));
    }

    function draw(word, beat) {
        const w = words[word];
        const key = word + ':' + beat;
        if (shown === key) return;
        shown = key;
        board.replaceChildren();
        board.append(el('p', 'label', `${data.unit} · ${word + 1}-so‘z / ${words.length} (bo‘limda ${data.total} ta)`));
        const h = el('p', 'hangul', w.word); h.lang = 'ko'; board.append(h);
        if (beat < 0) return;
        const meaning = el('p', 'meaning', w.meaningUz);
        if (w.romanization) meaning.append(el('small', null, w.romanization));
        board.append(meaning, el('p', 'title', w.title));
        for (let n = 0; n <= beat && n < w.beats.length; n++) {
            const b = w.beats[n];
            const f = el('div', 'frag' + (b.kind === 'trap' ? ' trap-kind' : '') + (n === beat ? ' speaking' : ''));
            f.append(el('span', 'kind', b.kind === 'trap' ? '✧ FARQIGA QARANG' : b.kind === 'example' ? 'BIRGA KO‘RAMIZ' : 'MA’NOSI'));
            if (b.korean) { const k = el('div', null, b.korean); k.lang = 'ko'; f.append(k); }
            if (b.uzbek) f.append(el('p', null, b.uzbek));
            if (!b.korean && !b.uzbek) f.append(el('p', null, b.narrationUz));
            board.append(f);
        }
    }

    const player = createPlayer({
        audio, count: clips.length, load,
        onChange(s) {
            const clip = clips[s.index];
            error.hidden = !(s.error || s.blocked);
            if (s.error) error.textContent = 'Ovoz yuklanmadi. Internetni tekshirib, yana bir bor bosing.';
            if (s.blocked) error.textContent = 'Ovozni boshlash uchun yana bir marta bosing.';
            if (s.done) {
                playBtn.textContent = '▶ Boshidan';
                subtitle.textContent = `Bu ${data.total} ta so‘zdan ${words.length} tasi edi. Qolganlari ilovada, xuddi shunday ovoz bilan.`;
                return;
            }
            draw(clip.word, clip.beat);
            const beat = words[clip.word].beats[clip.beat];
            subtitle.textContent = s.loading ? 'Ovoz yuklanmoqda…' : clip.beat < 0 ? `${words[clip.word].word} — avval talaffuzini eshiting.` : beat.narrationUz;
            playBtn.textContent = s.playing || s.loading ? '❚❚ Pauza' : '▶ Davom ettirish';
        },
    });

    playBtn.addEventListener('click', async () => {
        if (!started) {
            started = true; replay.hidden = false;
            audio.src = silence();
            try { await audio.play(); audio.pause(); } catch { }
            player.play(0);
            return;
        }
        const s = player.state;
        if (s.done) player.play(0);
        else if (s.playing) player.pause();
        else player.resume();
    });
    replay.addEventListener('click', () => {
        const word = clips[player.state.index]?.word ?? 0;
        player.play(Math.max(0, clips.findIndex(c => c.word === word)));
    });
}

function mountTraps() {
    const score = document.getElementById('score');
    const questions = [...document.querySelectorAll('.trap')];
    let answered = 0, found = 0;
    for (const q of questions) {
        const buttons = [...q.querySelectorAll('.opt')];
        buttons.forEach((button, chosen) => button.addEventListener('click', () => {
            if (q.dataset.done) return;
            q.dataset.done = '1';
            const { right, correct } = grade(buttons.map(b => ({ right: b.dataset.right === '1' })), chosen);
            buttons.forEach((b, n) => { b.disabled = true; if (n === right) b.classList.add('right'); });
            if (!correct) button.classList.add('wrong');
            const verdict = q.querySelector('.verdict');
            verdict.textContent = correct ? '✓ To‘g‘ri topdingiz.' : '✗ Bu tuzoq edi: o‘zbekcha mantiq shu yerda adashtiradi.';
            verdict.hidden = false;
            q.querySelector('.why').hidden = false;
            answered++; if (correct) found++;
            if (score && answered === questions.length) {
                score.textContent = `${score.dataset.total} tadan ${found} tasini topdingiz. Ilovada har bir so‘zda shunday izoh bor.`;
                score.hidden = false;
            }
        }));
    }
}

function mountSource() {
    const start = document.getElementById('start');
    const source = start?.dataset.source;
    if (!source) return;
    rememberSource(window.localStorage, source, value => {
        fetch('/api/visits', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ source: value }), keepalive: true }).catch(() => {});
    });
}

if (typeof document !== 'undefined') {
    try { mountSource(); } catch { }
    if (document.getElementById('taster-data')) mountTaster();
    mountTraps();
}
