import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';

// L-91: home-screen shortcuts and install screenshots in the manifest, and how an opening is counted.
const web = new URL('../../src/Lugat.Web/', import.meta.url);
const wwwroot = new URL('wwwroot/', web);
const manifest = JSON.parse(await readFile(new URL('manifest.webmanifest', wwwroot), 'utf8'));
const source = await readFile(new URL('js/app.js', wwwroot), 'utf8');
const { takeShortcut, SHORTCUTS } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

// Every "@page" route of the Blazor app, without parameters ("/bolimlar/{Id:guid}" is left out).
async function routes() {
    const found = new Set();
    const walk = async dir => {
        for (const entry of await readdir(dir, { withFileTypes: true })) {
            const path = new URL(entry.name + (entry.isDirectory() ? '/' : ''), dir);
            if (entry.isDirectory()) await walk(path);
            else if (entry.name.endsWith('.razor')) {
                for (const m of (await readFile(path, 'utf8')).matchAll(/^@page "([^"{]+)"$/gm)) found.add(m[1]);
            }
        }
    };
    await walk(new URL('Pages/', web));
    return found;
}

// Width and height of a PNG (IHDR) or a lossy/lossless/extended WebP.
function size(bytes) {
    if (bytes.readUInt32BE(0) === 0x89504e47) return [bytes.readUInt32BE(16), bytes.readUInt32BE(20)];
    assert.equal(bytes.toString('ascii', 0, 4), 'RIFF');
    assert.equal(bytes.toString('ascii', 8, 12), 'WEBP');
    const chunk = bytes.toString('ascii', 12, 16);
    if (chunk === 'VP8 ') return [bytes.readUInt16LE(26) & 0x3fff, bytes.readUInt16LE(28) & 0x3fff];
    if (chunk === 'VP8L') { const b = bytes.readUInt32LE(21); return [(b & 0x3fff) + 1, ((b >> 14) & 0x3fff) + 1]; }
    if (chunk === 'VP8X') return [bytes.readUIntLE(24, 3) + 1, bytes.readUIntLE(27, 3) + 1];
    throw new Error('unknown WebP chunk ' + chunk);
}

const file = src => new URL(src.split('?')[0], wwwroot);

test('there are four shortcuts, each opening an existing page with its own "yorliq"', async () => {
    const pages = await routes();
    assert.equal(manifest.shortcuts.length, 4);
    assert.deepEqual(manifest.shortcuts.map(s => new URL(s.url, 'https://koreyscha.com').searchParams.get('yorliq')), SHORTCUTS);
    for (const shortcut of manifest.shortcuts) {
        const url = new URL(shortcut.url, 'https://koreyscha.com');
        assert.ok(pages.has(url.pathname), `${shortcut.url} is not an @page route`);
        assert.ok(shortcut.name && shortcut.short_name && shortcut.short_name.length <= 12, shortcut.name);
    }
});

test('each shortcut has a 96x96 PNG icon that exists, with a version in its address', async () => {
    for (const shortcut of manifest.shortcuts) {
        assert.equal(shortcut.icons.length, 1);
        const [icon] = shortcut.icons;
        assert.equal(icon.sizes, '96x96');
        assert.equal(icon.type, 'image/png');
        assert.match(icon.src, /^icons\/shortcut-[a-z]+\.png\?v=\d+$/);
        assert.deepEqual(size(await readFile(file(icon.src))), [96, 96]);
    }
});

test('three narrow 1080x1920 WebP screenshots exist with Uzbek labels', async () => {
    assert.equal(manifest.screenshots.length, 3);
    for (const shot of manifest.screenshots) {
        assert.equal(shot.form_factor, 'narrow');
        assert.equal(shot.type, 'image/webp');
        assert.equal(shot.sizes, '1080x1920');
        assert.match(shot.src, /^screens\/[a-z]+\.webp\?v=\d+$/);
        assert.ok(shot.label.length > 0);
        assert.deepEqual(size(await readFile(file(shot.src))), [1080, 1920]);
    }
});

test('the earlier manifest fields are kept', () => {
    assert.equal(manifest.start_url, '/');
    assert.equal(manifest.display, 'standalone');
    assert.equal(manifest.theme_color, '#0047A0');
    assert.equal(manifest.icons.length, 4);
});

test('a shortcut opening gives its name and the address without "yorliq"', () => {
    assert.deepEqual(takeShortcut('https://koreyscha.com/takrorlash?yorliq=takrorlash'), { name: 'takrorlash', url: '/takrorlash' });
    assert.deepEqual(takeShortcut('https://koreyscha.com/mashq/yozish?yorliq=YOZISH'), { name: 'yozish', url: '/mashq/yozish' });
    // Other parameters and the hash stay.
    assert.deepEqual(takeShortcut('https://koreyscha.com/bolimlar?src=tg&yorliq=bolimlar#kirish'), { name: 'bolimlar', url: '/bolimlar?src=tg#kirish' });
});

test('a name that is not ours is not counted, but still leaves the address', () => {
    assert.deepEqual(takeShortcut('https://koreyscha.com/tinglash?yorliq=reklama'), { name: null, url: '/tinglash' });
    assert.deepEqual(takeShortcut('https://koreyscha.com/tinglash?yorliq='), { name: null, url: '/tinglash' });
    assert.deepEqual(takeShortcut('https://koreyscha.com/tinglash?yorliq=tinglash%20x'), { name: null, url: '/tinglash' });
});

test('without "yorliq" nothing is counted and the address is left alone', () => {
    assert.deepEqual(takeShortcut('https://koreyscha.com/'), { name: null, url: null });
    assert.deepEqual(takeShortcut('https://koreyscha.com/?src=tg'), { name: null, url: null });
});

test('a shortcut never touches the ad channel kept on the device', () => {
    const start = source.indexOf('export function takeShortcut');
    const body = source.slice(start, source.indexOf('\n}\n', start));
    assert.doesNotMatch(body, /localStorage|storage|koreyscha\.src/);
});
