import { test, expect } from '@playwright/test';

// Seed pending proposals through real editor APIs, then review them together in the real UI.
// Paid external AI is covered by provider fakes in PostgreSQL API tests, not called by this browser test.
test('an editor reviews 25 pronunciation proposals together and only edits the exceptional row', async ({ page }) => {
    await page.setViewportSize({ width: 360, height: 780 });
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.addInitScript(() => localStorage.setItem('lugat.onboarded', '1'));
    await page.goto('/');
    await page.getByRole('button', { name: 'Dev sifatida kirish' }).click({ timeout: 90_000 });
    await expect.poll(() => page.evaluate(() => localStorage.getItem('lugat.refresh'))).toBeTruthy();
    const token = await page.evaluate(async () => (await fetch('/api/auth/refresh', { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: localStorage.getItem('lugat.refresh') }) }).then(r => r.json())).accessToken);
    const api = async (url, method = 'GET', data) => {
        const r = await page.request.fetch(url, { method, data, headers: { Authorization: `Bearer ${token}` } });
        expect(r.ok(), `${method} ${url}: ${await r.text()}`).toBeTruthy();
        return r.json();
    };
    const unitId = (await api('/api/levels/A/units')).units[0].id;
    await api(`/api/units/${unitId}/open`, 'POST');
    const original = await api(`/api/admin/units/${unitId}`);
    try {
        for (const w of original.words) {
            await api(`/api/admin/catalog/words/${w.id}/pronunciation`, 'PUT', {
                reading: w.word === '좋다' ? '조다' : w.word,
                explanationUz: w.word === '좋다' ? 'Bu taklifni tekshiring.' : null,
                reviewed: false, version: w.version,
            });
        }
        await page.goto(`/kontent/${unitId}`);
        const panel = page.locator('.pronunciation-review-panel');
        await expect(panel.getByRole('heading', { name: 'Talaffuzlar', exact: true })).toBeVisible({ timeout: 30_000 });
        await expect(panel.locator('.pronunciation-review-list > li')).toHaveCount(25);
        const row = panel.locator('li').filter({ has: page.getByText('좋다', { exact: true }) });
        await row.getByRole('textbox', { name: 'Talaffuzi', exact: true }).fill('조타');
        await row.getByRole('textbox', { name: 'Izoh', exact: true }).fill('ㅎ va ㄷ birlashib ㅌ kabi aytiladi.');
        await panel.getByRole('checkbox').last().uncheck();
        await panel.getByRole('heading', { name: 'Talaffuzlar', exact: true }).scrollIntoViewIfNeeded();
        expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
        await page.screenshot({ path: `test-results/preparation-${test.info().project.name}.png` });
        const approved = page.waitForResponse(r => /\/pronunciations\/review$/.test(r.url()) && r.request().method() === 'PUT');
        await panel.getByRole('button', { name: 'Ko‘rib chiqdim — 24 tasini tasdiqlash', exact: true }).click();
        expect((await approved).ok()).toBeTruthy();
        await expect(panel.locator('.pronunciation-review-list > li')).toHaveCount(1);
        const unit = await api(`/api/units/${unitId}`);
        expect(unit.pronunciationCount).toBe(1);
        expect(unit.words.find(w => w.word === '좋다').pronunciation.reading).toBe('조타');
        expect(errors).toEqual([]);
    } finally {
        const fresh = await api(`/api/admin/units/${unitId}`);
        for (const w of original.words) {
            const review = w.pronunciationReview;
            await api(`/api/admin/catalog/words/${w.id}/pronunciation`, 'PUT', { reading: review.reading,
                explanationUz: review.explanationUz, reviewed: review.reviewed, version: fresh.words.find(v => v.id === w.id).version });
        }
    }
});
