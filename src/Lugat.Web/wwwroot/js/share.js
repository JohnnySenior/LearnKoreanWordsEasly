// "Do‘stga ulashish" after a passed unit test (L-59): a 1080×1350 card in the app's Sokin (light) or Tungi (dark)
// look and a link to the sign-in-free taster. The wording only says what is true: the unit was finished.

export const SHARE_URL = 'https://koreyscha.com/sinab?src=ulash';

const LATIN = '"Plus Jakarta Sans", system-ui, -apple-system, "Segoe UI", sans-serif';
const HANGUL = '"Noto Sans KR", "Apple SD Gothic Neo", "Malgun Gothic", sans-serif';
const W = 1080, H = 1350;

export const unitLabel = data => `${data.level}-${data.unitNumber}`;

export function shareText(data) {
    return `Men Koreyscha’da ${unitLabel(data)} bo‘limini tugatdim (${data.wordCount} ta so‘z). Siz ham 1 daqiqada sinab ko‘ring:`;
}

export function telegramUrl(text, url = SHARE_URL) {
    return `https://t.me/share/url?url=${encodeURIComponent(url)}&text=${encodeURIComponent(text)}`;
}

/** The viewer's theme as the app sets it: data-theme on <html>, else the device setting. */
export function currentTheme() {
    const forced = document.documentElement.getAttribute('data-theme');
    if (forced === 'dark' || forced === 'light') return forced;
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

/**
 * Shares the card file where the browser can, else the text and link, else opens Telegram's share page.
 * Cancelling is not an error. env is injectable for tests.
 */
export async function shareResult(data, env = browserEnv()) {
    const text = shareText(data);
    let file = null;
    try { file = await env.file(data); } catch { file = null; }
    const nav = env.nav;
    if (file && nav?.canShare?.({ files: [file] })) {
        // The link goes inside the text, not as url: given a url, iOS Telegram sends only the link and drops the image.
        try { await nav.share({ files: [file], text: `${text} ${SHARE_URL}` }); return 'shared'; }
        catch (e) { if (e?.name === 'AbortError') return 'cancelled'; }
    }
    if (nav?.share) {
        try { await nav.share({ text, url: SHARE_URL }); return 'shared'; }
        catch (e) { if (e?.name === 'AbortError') return 'cancelled'; }
    }
    try { return env.open(telegramUrl(text)) ? 'telegram' : 'failed'; } catch { return 'failed'; }
}

// ---------- the card ----------
let prepared = null;

/** Draws the card ahead of the tap: share() must start right from the tap, and drawing waits for fonts. */
export function prepare(data, theme) {
    const key = JSON.stringify([data, theme ?? null]);
    if (!prepared || prepared.key !== key) {
        prepared = { key, file: drawCard(data, theme).then(blob => new File([blob], `koreyscha-${unitLabel(data)}.png`, { type: 'image/png' })) };
        prepared.file.catch(() => { prepared = null; });
    }
    return prepared.file;
}

function browserEnv() {
    return {
        nav: typeof navigator === 'undefined' ? null : navigator,
        file: data => prepare(data),
        // Not 'noopener': then open() returns null and a successful open could not be told from a blocked one.
        open: url => { const w = window.open(url, '_blank'); if (w) w.opener = null; return w; },
    };
}

async function fontsReady(data) {
    if (!document.fonts?.load) return;
    const ko = 'ㅋ' + data.words.map(w => w.korean).join('');
    const waits = [
        document.fonts.load(`800 46px ${LATIN}`), document.fonts.load(`600 36px ${LATIN}`), document.fonts.load(`500 36px ${LATIN}`),
        document.fonts.load(`700 64px ${HANGUL}`, ko),
    ];
    // Never hang on a slow network: after 3 s the card is drawn with whatever has arrived.
    await Promise.race([Promise.allSettled(waits), new Promise(r => setTimeout(r, 3000))]);
}

function round(ctx, x, y, w, h, r) {
    ctx.beginPath();
    if (ctx.roundRect) { ctx.roundRect(x, y, w, h, r); return; }
    ctx.moveTo(x + r, y); ctx.arcTo(x + w, y, x + w, y + h, r); ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r); ctx.arcTo(x, y, x + w, y, r); ctx.closePath();
}

/** The text cut with "…" so it fits maxWidth in the current font. */
function fit(ctx, text, maxWidth) {
    if (ctx.measureText(text).width <= maxWidth) return text;
    let s = text;
    while (s.length > 1 && ctx.measureText(s + '…').width > maxWidth) s = s.slice(0, -1);
    return s.trimEnd() + '…';
}

/** A font size from max down to min that lets the text fit maxWidth. */
function sizeFor(ctx, text, weight, family, max, min, maxWidth) {
    for (let size = max; size > min; size -= 4) {
        ctx.font = `${weight} ${size}px ${family}`;
        if (ctx.measureText(text).width <= maxWidth) return size;
    }
    return min;
}

function mark(ctx, x, y, size, fill) {
    ctx.fillStyle = fill; round(ctx, x, y, size, size, size * 0.27); ctx.fill();
    ctx.fillStyle = '#ffffff'; ctx.font = `700 ${Math.round(size * 0.58)}px ${HANGUL}`;
    ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    ctx.fillText('ㅋ', x + size / 2, y + size / 2 + 2);
    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
}

function pill(ctx, text, x, y, bg, fg, alignRight) {
    ctx.font = `700 32px ${LATIN}`;
    const w = ctx.measureText(text).width + 56, h = 64;
    const left = alignRight ? x - w : x;
    ctx.fillStyle = bg; round(ctx, left, y, w, h, h / 2); ctx.fill();
    ctx.fillStyle = fg; ctx.textBaseline = 'middle'; ctx.fillText(text, left + 28, y + h / 2 + 1); ctx.textBaseline = 'alphabetic';
    return w;
}

function drawSokin(ctx, data) {
    const ink = '#2b2420', muted = '#776a5f';
    ctx.fillStyle = '#f6f2ed'; ctx.fillRect(0, 0, W, H);
    mark(ctx, 90, 86, 88, '#e5533d');
    ctx.fillStyle = ink; ctx.font = `800 46px ${LATIN}`; ctx.fillText('Koreyscha', 200, 146);
    ctx.fillStyle = muted; ctx.font = `600 34px ${LATIN}`; ctx.textAlign = 'right';
    ctx.fillText(`${data.level} daraja · ${data.unitNumber}-bo‘lim`, 990, 144); ctx.textAlign = 'left';

    const percent = data.total ? Math.round(data.score * 100 / data.total) : null;
    pill(ctx, data.total ? `✓ Bo‘lim testi: ${data.score} / ${data.total} · ${percent}%` : '✓ Bo‘lim testidan o‘tdim', 90, 250, '#e6efe2', '#4b7541');

    ctx.fillStyle = ink; ctx.font = `800 92px ${LATIN}`;
    ctx.fillText(`${unitLabel(data)} bo‘limini`, 90, 450); ctx.fillText('tugatdim', 90, 550);
    ctx.fillStyle = muted; ctx.font = `500 36px ${LATIN}`;
    ctx.fillText(`${data.wordCount} ta so‘z · o‘zbekcha tushuntirish bilan`, 90, 618);

    ctx.font = `600 30px ${LATIN}`; ctx.fillText('Shu bo‘limdagi so‘zlardan', 96, 690);
    const top = 720, row = 116, cardH = 40 + data.words.length * row;
    ctx.save(); ctx.shadowColor = 'rgba(80, 50, 30, 0.18)'; ctx.shadowBlur = 40; ctx.shadowOffsetY = 16;
    ctx.fillStyle = '#fffdfa'; round(ctx, 90, top, 900, cardH, 36); ctx.fill(); ctx.restore();
    data.words.forEach((w, n) => {
        const mid = top + 20 + n * row + row / 2;
        if (n > 0) { ctx.fillStyle = 'rgba(43, 36, 32, 0.08)'; ctx.fillRect(130, top + 20 + n * row, 820, 2); }
        ctx.fillStyle = ink; ctx.font = `700 64px ${HANGUL}`; ctx.textBaseline = 'middle';
        const ko = fit(ctx, w.korean, 430); ctx.fillText(ko, 140, mid);
        ctx.fillStyle = muted; ctx.font = `500 38px ${LATIN}`; ctx.textAlign = 'right';
        ctx.fillText(fit(ctx, w.uzbek, 360), 940, mid);
        ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
    });

    ctx.fillStyle = ink; ctx.font = `600 36px ${LATIN}`; ctx.fillText('Siz ham 1 daqiqada sinab ko‘ring', 90, 1196);
    ctx.fillStyle = '#b8402c'; ctx.font = `800 46px ${LATIN}`; ctx.fillText('koreyscha.com/sinab', 90, 1262);
    ctx.fillStyle = muted; ctx.font = `600 32px ${LATIN}`; ctx.textAlign = 'right';
    ctx.fillText('Kirishsiz', 990, 1196); ctx.fillText('bepul', 990, 1250); ctx.textAlign = 'left';
}

function drawTungi(ctx, data) {
    const ink = '#eef0f6', muted = '#8f94a7', accent = '#ff5a3c';
    ctx.fillStyle = '#0f1117'; ctx.fillRect(0, 0, W, H);
    const glow = ctx.createRadialGradient(W, 0, 0, W, 0, 900);
    glow.addColorStop(0, 'rgba(255, 90, 60, 0.18)'); glow.addColorStop(1, 'rgba(255, 90, 60, 0)');
    ctx.fillStyle = glow; ctx.fillRect(0, 0, W, H);
    mark(ctx, 90, 86, 88, accent);
    ctx.fillStyle = ink; ctx.font = `800 46px ${LATIN}`; ctx.fillText('Koreyscha', 200, 146);
    pill(ctx, `✓ ${unitLabel(data)} bo‘lim tugadi`, 990, 98, 'rgba(120, 220, 170, 0.14)', '#8fe3b8', true);

    const first = data.words[0];
    ctx.save(); ctx.shadowColor = 'rgba(255, 90, 60, 0.32)'; ctx.shadowBlur = 90;
    ctx.fillStyle = '#161922'; round(ctx, 90, 250, 900, 640, 36); ctx.fill(); ctx.restore();
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.08)'; ctx.lineWidth = 2; round(ctx, 90, 250, 900, 640, 36); ctx.stroke();
    ctx.fillStyle = muted; ctx.font = `600 32px ${LATIN}`; ctx.fillText('Shu bo‘limdagi so‘zlardan biri', 150, 340);
    const size = sizeFor(ctx, first.korean, 700, HANGUL, 180, 90, 780);
    ctx.fillStyle = accent; ctx.font = `700 ${size}px ${HANGUL}`; ctx.fillText(first.korean, 146, 560);
    if (first.romanization) { ctx.fillStyle = muted; ctx.font = `500 40px ${LATIN}`; ctx.fillText(fit(ctx, first.romanization, 780), 150, 650); }
    ctx.fillStyle = ink; ctx.font = `700 54px ${LATIN}`; ctx.fillText(fit(ctx, first.uzbek, 780), 150, 760);

    // The other words as chips, then how many more the unit has.
    let x = 90;
    const chip = (draw, width) => {
        if (x + width > 990) return;
        ctx.fillStyle = 'rgba(255, 255, 255, 0.06)'; round(ctx, x, 940, width, 84, 42); ctx.fill();
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)'; round(ctx, x, 940, width, 84, 42); ctx.stroke();
        draw(x); x += width + 16;
    };
    for (const w of data.words.slice(1)) {
        ctx.font = `700 38px ${HANGUL}`; const ko = fit(ctx, w.korean, 220), kw = ctx.measureText(ko).width;
        ctx.font = `500 32px ${LATIN}`; const uz = fit(ctx, w.uzbek, 200), uw = ctx.measureText(uz).width;
        chip(left => {
            ctx.textBaseline = 'middle';
            ctx.fillStyle = ink; ctx.font = `700 38px ${HANGUL}`; ctx.fillText(ko, left + 30, 983);
            ctx.fillStyle = muted; ctx.font = `500 32px ${LATIN}`; ctx.fillText(uz, left + 30 + kw + 16, 983);
            ctx.textBaseline = 'alphabetic';
        }, 60 + kw + 16 + uw);
    }
    const more = data.wordCount - data.words.length;
    if (more > 0) {
        ctx.font = `700 32px ${LATIN}`; const t = `+${more} so‘z`, tw = ctx.measureText(t).width;
        chip(left => { ctx.textBaseline = 'middle'; ctx.fillStyle = muted; ctx.fillText(t, left + 30, 983); ctx.textBaseline = 'alphabetic'; }, tw + 60);
    }

    ctx.fillStyle = muted; ctx.font = `600 34px ${LATIN}`; ctx.fillText('O‘zbekcha, kirishsiz, 1 daqiqa', 90, 1186);
    ctx.fillStyle = ink; ctx.font = `800 50px ${LATIN}`; ctx.fillText('koreyscha.com/sinab', 90, 1258);
    ctx.fillStyle = accent; round(ctx, 880, 1150, 110, 110, 30); ctx.fill();
    ctx.strokeStyle = '#ffffff'; ctx.lineWidth = 8; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
    ctx.beginPath(); ctx.moveTo(910, 1205); ctx.lineTo(960, 1205); ctx.moveTo(940, 1183); ctx.lineTo(962, 1205); ctx.lineTo(940, 1227); ctx.stroke();
}

/** The card as a PNG blob, in the viewer's theme unless one is given. */
export async function drawCard(data, theme) {
    await fontsReady(data);
    const canvas = document.createElement('canvas');
    canvas.width = W; canvas.height = H;
    const ctx = canvas.getContext('2d');
    ((theme ?? currentTheme()) === 'dark' ? drawTungi : drawSokin)(ctx, data);
    return await new Promise((resolve, reject) => canvas.toBlob(b => (b ? resolve(b) : reject(new Error('png'))), 'image/png'));
}
