// L-60: the home page shows a static landing before Blazor starts. Its text is a copy of the Blazor sign-in screen,
// so these tests keep the two in step and check what a returning user and the taster link get.
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const web = new URL('../../src/Lugat.Web/', import.meta.url);
const read = path => readFile(new URL(path, web), 'utf8');
const [index, landing, intro, boot, layout, session, css] = await Promise.all([
    read('wwwroot/index.html'), read('Shared/Landing.razor'), read('Shared/SigninIntro.razor'), read('Shared/BootScreen.razor'),
    read('Layout/MainLayout.razor'), read('Services/SessionService.cs'), read('wwwroot/css/app.css'),
]);
const bootHtml = index.slice(index.indexOf('<div id="app">'), index.indexOf('<div id="blazor-error-ui">'));
const text = html => html.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ');

test('the static landing has the same sentences as the Blazor sign-in screen', () => {
    // Every line of text in SigninIntro and in Landing's first three parts (up to the install guide).
    const landingText = landing.slice(0, landing.indexOf('@if (!Boot)'));
    const lines = [...intro.matchAll(/>([^<>@]{6,})</g), ...landingText.matchAll(/>([^<>@]{6,})</g), ...landingText.matchAll(/Text="([^"]+)"/g)]
        .map(m => m[1].trim()).filter(Boolean);
    assert.ok(lines.length > 15);
    for (const line of lines) assert.ok(bootHtml.includes(line), `index.html lacks: ${line}`);
    assert.match(bootHtml, /<h1>Koreys tili so‘zlari — o‘zbek tilida\.<\/h1>/);
    assert.match(bootHtml, /<span class="landing-word-ko" lang="ko">우리<\/span>/);
    assert.ok(text(bootHtml).includes('Manba: 국립국어원 한국어 학습용 어휘 목록 (KOGL 1) · Maxfiylik siyosati'));
});

test('the main link goes to the taster as "bosh", the same link the Blazor screens use', () => {
    assert.match(bootHtml, /<a class="btn btn-secondary block" href="\/sinab\?src=bosh" target="_top">/); // L-72: sign-in comes first
    assert.match(bootHtml, /<a class="btn btn-ghost" href="\/soz" target="_top">Koreyscha so‘zlar ro‘yxati<\/a>/);
    assert.match(boot, /TasterLink = "\/sinab\?src=bosh"/);
    assert.match(layout, /href="@BootScreen\.TasterLink" target="_top"/); // target: Blazor must not route /sinab itself
    assert.match(layout, /<BootScreen \/>/);
});

test('a returning user sees only the brand and "Yuklanmoqda…": the key is SessionService.RefreshKey', () => {
    const key = session.match(/RefreshKey = "([^"]+)"/)[1];
    const script = index.slice(0, index.indexOf('</head>'));
    assert.ok(script.includes(`try { if (localStorage.getItem('${key}')) document.documentElement.classList.add('returning'); } catch { }`));
    assert.match(css, /\.returning \.boot-landing \{ display: none; \}/);
    // The brand and the loading line stay outside .boot-landing; everything else is inside it.
    assert.match(bootHtml, /<span class="app-header-title">Koreyscha<\/span>/);
    assert.match(bootHtml, /<p class="boot-loading" role="status" aria-live="polite">.*Yuklanmoqda…<span class="boot-percent" aria-hidden="true"><\/span><\/p>/);
    const before = bootHtml.slice(0, bootHtml.indexOf('<div class="boot-landing">'));
    assert.ok(!before.includes('signin-words') && !before.includes('/sinab'));
    assert.match(css, /@media \(prefers-reduced-motion: reduce\) \{ \.boot-dot \{ animation: none; \} \}/);
});

test('the home page carries canonical and Open Graph tags', () => {
    assert.match(index, /<link rel="canonical" href="https:\/\/koreyscha\.com\/" \/>/);
    for (const p of ['og:title', 'og:description', 'og:url']) assert.match(index, new RegExp(`<meta property="${p}" content="[^"]+" />`));
});

test('the home page names the site for search results in one WebSite JSON-LD block (L-66)', () => {
    const blocks = [...index.matchAll(/<script type="application\/ld\+json">([\s\S]*?)<\/script>/g)];
    assert.equal(blocks.length, 1);
    const site = JSON.parse(blocks[0][1]);
    assert.deepEqual(site, { '@context': 'https://schema.org', '@type': 'WebSite', name: 'Koreyscha', alternateName: 'koreyscha.com', url: 'https://koreyscha.com/', inLanguage: 'uz' });
    assert.ok(index.indexOf('application/ld+json') < index.indexOf('</head>')); // in the first HTML, before any script runs
});
