// Takes the three install-dialog screenshots for manifest.webmanifest (L-91): Today, the units list, a unit test
// result. 360x640 at device scale 3 → 1080x1920, saved as WebP in src/Lugat.Web/wwwroot/screens/.
//
//   dotnet run --project src/Lugat.Api        # Development: sample catalog (A-1, A-2) and "Dev sifatida kirish"
//   node scripts/make-screenshots.mjs [http://localhost:5248]
//
// Only the local sample data and the dev account are used. Run it on a fresh local database (the dev user's A-1
// test is taken and passed, so A-1 shows as done and A-2 as open). Needs Playwright with Chromium.
import { createRequire } from 'node:module';
import { readFile, writeFile, mkdir } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const base = (process.argv[2] || 'http://localhost:5248').replace(/\/$/, '');
const root = new URL('../', import.meta.url);
const out = new URL('src/Lugat.Web/wwwroot/screens/', root);

// The sample words, both ways, to answer the unit test like a learner who knows almost all of them.
const quote = s => s.replace(/['’ʻ`]/g, '‘').trim();
const words = JSON.parse(await readFile(new URL('content/samples/lugat_A_namuna_50.json', root), 'utf8'));
const toUzbek = new Map(words.map(w => [w.word, quote(w.translation_uz)]));
const toKorean = new Map(words.map(w => [quote(w.translation_uz), w.word]));

async function api(path, token, body) {
    const response = await fetch(base + path, {
        method: body === undefined ? 'GET' : 'POST',
        headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
        body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) throw new Error(`${path}: ${response.status} ${await response.text()}`);
    return response.status === 204 ? null : response.json();
}

// Passes A-1's final test with two wrong answers, so the result looks like a real one.
async function passUnitTest() {
    const { accessToken: token } = await api('/api/auth/dev', null, null);
    const units = await api('/api/levels/A/units', token);
    const unit = units.units.find(u => u.number === 1);
    const test = await api(`/api/units/${unit.id}/tests`, token, { id: crypto.randomUUID(), kind: 'Final' });
    let wrong = 0;
    for (const q of test.questions) {
        const prompt = quote(q.prompt);
        let answer = toUzbek.get(q.prompt) ?? toKorean.get(prompt) ?? q.options[0] ?? '';
        if (q.options.length && !q.options.some(o => quote(o) === quote(answer))) answer = q.options[0];
        if (q.options.length && wrong < 2 && q.index % 7 === 3) {
            answer = q.options.find(o => quote(o) !== quote(answer));
            wrong++;
        }
        await api(`/api/unit-tests/${test.id}/answers`, token, { index: q.index, answer });
    }
    const done = await api(`/api/unit-tests/${test.id}/finish`, token, null);
    console.log(`A-1 test passed: ${done.unit?.passed}`);
    return test.id;
}

async function shoot(page, path, name) {
    await page.goto(base + path, { waitUntil: 'networkidle' });
    await page.waitForSelector('main.content h1, main.content .today, main.content header', { timeout: 30000 });
    await page.waitForTimeout(1500);
    // Locally there is no voice service; its notice never shows on the live site, so it is not in the picture.
    await page.evaluate(() => document.querySelectorAll('main.content p, main.content .notice').forEach(el => {
        if (el.textContent.trim().startsWith('Ovoz xizmati ulanmagan')) el.remove();
    }));
    const png = await page.screenshot({ type: 'png' });
    // Chromium encodes WebP itself, so no image library is needed.
    const webp = await page.evaluate(async b64 => {
        const image = new Image();
        image.src = 'data:image/png;base64,' + b64;
        await image.decode();
        const canvas = document.createElement('canvas');
        canvas.width = image.width;
        canvas.height = image.height;
        canvas.getContext('2d').drawImage(image, 0, 0);
        return canvas.toDataURL('image/webp', 0.82).split(',')[1];
    }, png.toString('base64'));
    await writeFile(new URL(`${name}.webp`, out), Buffer.from(webp, 'base64'));
    console.log(`screens/${name}.webp`);
}

const testId = await passUnitTest();
await mkdir(out, { recursive: true });
const browser = await chromium.launch();
const context = await browser.newContext({
    viewport: { width: 360, height: 640 },
    deviceScaleFactor: 3,
    isMobile: true,
    hasTouch: true,
    colorScheme: 'light',
    locale: 'uz-UZ',
    serviceWorkers: 'block',
});
// Onboarding is seen; the install offer and update banner are not part of the pictures.
await context.addInitScript(() => localStorage.setItem('lugat.onboarded', '1'));
const page = await context.newPage();
await page.goto(base + '/', { waitUntil: 'networkidle' });
await page.getByRole('button', { name: 'Dev sifatida kirish' }).click();
await page.waitForSelector('nav.tabbar', { timeout: 30000 });

await shoot(page, '/', 'bugun');
await shoot(page, '/bolimlar', 'bolimlar');
await shoot(page, `/sinov/${testId}`, 'natija');
await browser.close();
