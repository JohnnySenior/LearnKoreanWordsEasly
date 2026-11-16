import { test, expect } from '@playwright/test';

// Real browser → real API → PostgreSQL → rendered result. No API, AI or audio response is mocked.
// Uses the existing Development login and checked sample catalog, never a production account.
test('a learner signs in, studies a card, passes a unit and reloads the saved result', async ({ page }) => {
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => localStorage.setItem('lugat.onboarded', '1'));
    await page.goto('/');
    await page.getByRole('button', { name: 'Dev sifatida kirish' }).click({ timeout: 90_000 });
    await expect.poll(() => page.evaluate(() => localStorage.getItem('lugat.refresh'))).toBeTruthy();
    await page.goto('/bolimlar');
    const first = page.locator('a.unit-item[href^="bolimlar/"]').first();
    await first.click();
    await expect(page.locator('.word-table tbody tr')).toHaveCount(25);
    const rows = await page.locator('.word-table tbody tr').allTextContents();
    expect(rows.length).toBe(25);

    // Use the checked catalog DTO (available to any learner), never hidden test answers.
    const unitId = page.url().split('/').pop();
    const unit = await page.evaluate(async id => {
        // The normal page load has already opened the unit; capture the DTO through the app's token refresh.
        const tokens = await fetch('/api/auth/refresh', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken: localStorage.getItem('lugat.refresh') }) }).then(r => r.json());
        const response = await fetch(`/api/units/${id}`, { headers: { Authorization: `Bearer ${tokens.accessToken}` } });
        if (!response.ok) throw new Error(`Unit API: ${response.status}`);
        return response.json();
    }, unitId);

    await page.locator('.stepper button').nth(1).click();
    await page.getByRole('button', { name: /Kartochkani boshlash/ }).click();
    await page.getByRole('button', { name: 'Javobni ko‘rsatish', exact: true }).click();
    const savedCard = page.waitForResponse(r => /\/api\/sessions\/.*\/answers$/.test(r.url()) && r.request().method() === 'POST');
    await page.getByRole('button', { name: /Oson esladim/ }).click();
    expect((await savedCard).ok()).toBeTruthy();

    await page.goto(`/bolimlar/${unitId}`);
    await page.locator('.stepper button').nth(3).click();
    const created = page.waitForResponse(r => r.url().endsWith(`/api/units/${unitId}/tests`) && r.request().method() === 'POST');
    await page.getByRole('button', { name: /Testni (boshlash|qayta topshirish)/ }).click();
    const run = await (await created).json();
    const key = text => text.replace(/[’‘ʻʼ]/g, "'").replace(/\s+/g, ' ').trim();
    expect(run.questions.every(q => q.correct === null)).toBeTruthy();
    expect(JSON.stringify(run)).not.toContain('acceptedAnswers');
    for (let i = 0; i < run.questions.length; i++) {
        const q = run.questions[i];
        expect(q.kind).not.toMatch(/Listening/); // No paid speech service is configured in this smoke environment.
        const word = q.kind === 'WordMeaning'
            ? [...unit.words].sort((a, b) => b.word.length - a.word.length).find(w => q.prompt.startsWith(w.word))
            : [...unit.words].sort((a, b) => b.translationUz.length - a.translationUz.length).find(w => key(q.prompt).startsWith(key(w.translationUz)));
        expect(word, `Catalog entry for ${q.prompt}`).toBeTruthy();
        const savedAnswer = page.waitForResponse(r => r.url().endsWith(`/api/tests/${run.id}/answers`) && r.request().method() === 'POST');
        if (q.kind === 'WordKoreanTyped') {
            await page.locator('input[lang="ko"]').fill(word.word);
            await page.getByRole('button', { name: 'Keyingisi', exact: true }).click();
        } else {
            const answer = q.kind === 'WordMeaning'
                ? q.options.find(o => key(o) === key(word.translationUz))
                : q.options.find(o => o.startsWith(word.word));
            expect(answer).toBeTruthy();
            await page.locator('button.choice').nth(q.options.indexOf(answer)).click();
        }
        expect((await savedAnswer).ok()).toBeTruthy();
    }
    const finish = page.getByRole('button', { name: 'Yakunlash va natijani ko‘rish' });
    // The final typed answer is sent by Next; the client then exposes the finish button.
    if (!(await finish.isVisible())) await page.getByRole('button', { name: 'Keyingisi', exact: true }).click();
    await finish.click();
    await expect(page.getByRole('heading', { name: 'Bo‘lim tugadi. Keyingi bo‘lim ochildi.' })).toBeVisible();
    await page.reload();
    await expect(page.getByRole('link', { name: 'Keyingi bo‘limni boshlash' })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
    await page.getByRole('link', { name: 'Keyingi bo‘limni boshlash' }).click();
    await expect(page.locator('.word-table tbody tr')).toHaveCount(25);
    expect(errors).toEqual([]);
});
