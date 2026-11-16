import { test, expect } from '@playwright/test';

// Human review through the real content UI, then the real learner UI. No external AI/audio calls or API mocks.
test('a reviewed pronunciation appears beside the word and remains hidden behind a covered Korean column', async ({ page }) => {
    await page.setViewportSize({ width: 360, height: 780 });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => localStorage.setItem('lugat.onboarded', '1'));
    await page.goto('/');
    await page.getByRole('button', { name: 'Dev sifatida kirish' }).click({ timeout: 90_000 });
    await expect.poll(() => page.evaluate(() => localStorage.getItem('lugat.refresh'))).toBeTruthy();
    const token = await page.evaluate(async () => {
        const result = await fetch('/api/auth/refresh', { method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken: localStorage.getItem('lugat.refresh') }) }).then(r => r.json());
        return result.accessToken;
    });
    const api = async (path, method = 'GET', data) => {
        const response = await page.request.fetch(path, { method, headers: { Authorization: `Bearer ${token}` }, data });
        expect(response.ok(), `${method} ${path}: ${await response.text()}`).toBeTruthy();
        return response.json();
    };
    const levels = await api('/api/levels/A/units');
    const unitId = levels.units[0].id;
    await api(`/api/units/${unitId}/open`, 'POST');
    const catalog = await api(`/api/admin/units/${unitId}`);
    const word = catalog.words.find(w => w.word === '좋다');
    expect(word).toBeTruthy();
    const endpoint = `/api/admin/catalog/words/${word.id}/pronunciation`;
    const original = word.pronunciationReview;
    try {
        await page.goto(`/kontent/${unitId}`);
        const card = page.locator(`#w-${word.id}`);
        const noteEditor = card.locator('.pronunciation-editor');
        await noteEditor.locator('summary').click();
        await noteEditor.getByRole('textbox', { name: 'Hangul bilan talaffuzi' }).fill('조타');
        await noteEditor.getByRole('textbox', { name: 'Qisqa o‘zbekcha izoh' }).fill('ㅎ va ㄷ birlashib ㅌ kabi aytiladi.');
        await noteEditor.getByRole('button', { name: 'Tasdiqlash', exact: true }).click();
        await expect(noteEditor.getByRole('status')).toHaveText('Talaffuz tasdiqlandi.');
        // Simulate a previously published Korean-only note through the real API, without AI or API mocks.
        const saved = (await api(`/api/admin/units/${unitId}`)).words.find(w => w.id === word.id);
        await api(endpoint, 'PUT', { reading: '조타', explanationUz: 'ㅎ과 ㄷ이 만나 ㅌ으로 발음됩니다.', reviewed: true, version: saved.version });
        await page.goto(`/bolimlar/${unitId}`);
        await expect(page.locator('.pronunciation-summary > summary')).toContainText('Talaffuzga e’tibor', { timeout: 30_000 });
        const row = page.locator('.word-table tbody > tr').filter({ has: page.getByText('좋다', { exact: true }) }).first();
        const ear = row.getByRole('button', { name: '좋다: talaffuz farqini ko‘rish' });
        await expect(ear).toHaveAttribute('aria-expanded', 'false');
        await ear.focus();
        await page.keyboard.press('Enter');
        await expect(ear).toHaveAttribute('aria-expanded', 'true');
        const note = page.locator('.word-table .pronunciation-body');
        await expect(note).toContainText('[조타]');
        await expect(note).toContainText('«좋다» so‘zi «조타» deb o‘qiladi.');
        await expect(note).not.toContainText('발음됩니다');
        await expect(note).toContainText('Yozishda 좋다 yoziladi.');
        const premium = await api('/api/me/premium');
        await expect(note.getByRole('link', { name: /Talaffuzni mashq qilish/ })).toHaveAttribute('href', premium.isPremium ? 'sinov/ogzaki' : 'premium?f=talaffuz');
        const box = await ear.boundingBox();
        expect(box.width).toBeGreaterThanOrEqual(44);
        expect(box.height).toBeGreaterThanOrEqual(44);
        expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
        await page.screenshot({ path: `test-results/pronunciation-${test.info().project.name}.png`, fullPage: true });
        await expect(note.locator('audio')).toHaveCount(0); // no automatic paid playback
        await page.getByRole('radio', { name: '한국어', exact: true }).click();
        await expect(page.locator('.word-table .pronunciation-ear')).toHaveCount(0);
        const unit = await api(`/api/units/${unitId}`);
        const item = unit.words.find(w => w.word === '좋다');
        let session = (await api('/api/sessions', 'POST', { minutes: null, restart: true, learningItemIds: [item.learningItemId] })).session;
        await page.goto('/mashq/kartochka');
        for (let i = 0; i < 2; i++) {
            await expect(page.getByRole('button', { name: 'Javobni ko‘rsatish', exact: true })).toBeVisible({ timeout: 30_000 });
            if (session.current.direction === 'UzbekToKorean') {
                await expect(page.locator('.pronunciation-ear')).toHaveCount(0);
            } else {
                await expect(page.locator('.pronunciation-ear')).toHaveCount(1);
            }
            await page.getByRole('button', { name: 'Javobni ko‘rsatish', exact: true }).click();
            await expect(page.locator('.pronunciation-ear')).toHaveCount(1);
            const answered = page.waitForResponse(r => /\/api\/sessions\/.*\/answers$/.test(r.url()) && r.request().method() === 'POST');
            await page.getByRole('button', { name: /Oson esladim/ }).click();
            session = (await (await answered).json()).session;
        }
        expect(errors).toEqual([]);
    } finally {
        const fresh = await api(`/api/admin/units/${unitId}`);
        const version = fresh.words.find(w => w.id === word.id).version;
        await api(endpoint, 'PUT', { reading: original.reading, explanationUz: original.explanationUz, reviewed: original.reviewed, version });
    }
});
