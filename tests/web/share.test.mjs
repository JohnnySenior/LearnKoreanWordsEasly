import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../../src/Lugat.Web/wwwroot/js/share.js', import.meta.url), 'utf8');
const { shareResult, shareText, telegramUrl, SHARE_URL } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

const data = { level: 'A', unitNumber: 3, wordCount: 25, score: 23, total: 25, words: [{ korean: '사람', romanization: 'saram', uzbek: 'odam' }] };
const file = { name: 'koreyscha-A-3.png' };

function env({ canShare = true, share = async () => {}, open = () => ({}) } = {}) {
    const calls = { share: [], open: [] };
    return {
        calls,
        nav: {
            canShare: canShare === null ? undefined : () => canShare,
            share: share === null ? undefined : async payload => { calls.share.push(payload); return share(payload); },
        },
        file: async () => file,
        open: url => { calls.open.push(url); return open(url); },
    };
}

test('the text says only that the unit was finished, and the link counts as "ulash"', () => {
    assert.equal(shareText(data), 'Men Koreyscha’da A-3 bo‘limini tugatdim (25 ta so‘z). Siz ham 1 daqiqada sinab ko‘ring:');
    assert.equal(SHARE_URL, 'https://koreyscha.com/sinab?src=ulash');
});

test('the card is shared as a file with the link inside the text, not as a separate url', async () => {
    const e = env();
    assert.equal(await shareResult(data, e), 'shared');
    assert.deepEqual(e.calls.share, [{ files: [file], text: `${shareText(data)} ${SHARE_URL}` }]); // iOS Telegram drops the image when url is given
    assert.deepEqual(e.calls.open, []);
});

test('without file sharing the text and link are shared', async () => {
    const e = env({ canShare: false });
    assert.equal(await shareResult(data, e), 'shared');
    assert.deepEqual(e.calls.share, [{ text: shareText(data), url: SHARE_URL }]);
});

test('without any share sheet Telegram opens with the link and text encoded', async () => {
    const e = env({ canShare: null, share: null });
    assert.equal(await shareResult(data, e), 'telegram');
    assert.equal(e.calls.open[0], telegramUrl(shareText(data)));
    const url = new URL(e.calls.open[0]);
    assert.equal(url.origin + url.pathname, 'https://t.me/share/url');
    assert.equal(url.searchParams.get('url'), SHARE_URL);
    assert.equal(url.searchParams.get('text'), shareText(data));
    assert.ok(!e.calls.open[0].includes(' '));
});

test('cancelling the share sheet is not an error and nothing else opens', async () => {
    const abort = Object.assign(new Error('cancelled'), { name: 'AbortError' });
    const e = env({ share: async () => { throw abort; } });
    assert.equal(await shareResult(data, e), 'cancelled');
    assert.deepEqual(e.calls.open, []);
});

test('a refused file share falls back to the text, and a blocked Telegram window is reported', async () => {
    let n = 0;
    const e = env({ share: async () => { if (n++ === 0) throw Object.assign(new Error('no'), { name: 'NotAllowedError' }); } });
    assert.equal(await shareResult(data, e), 'shared');
    assert.equal(e.calls.share.length, 2);

    const blocked = env({ canShare: null, share: null, open: () => null });
    assert.equal(await shareResult(data, blocked), 'failed');
});
