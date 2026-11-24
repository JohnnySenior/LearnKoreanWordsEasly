import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

// L-93: the /alifbo page script.
const source = await readFile(new URL('../../src/Lugat.Web/wwwroot/js/alifbo.js', import.meta.url), 'utf8');
const { grade, scoreText } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

test('the chosen reading is checked against the one marked right', () => {
    const options = [{ right: false }, { right: true }, { right: false }];
    assert.deepEqual(grade(options, 1), { right: 1, correct: true });
    assert.deepEqual(grade(options, 2), { right: 1, correct: false });
});

test('the result is a calm count, no ranking or streak', () => {
    assert.equal(scoreText(5, 3), '5 ta so‘zdan 3 tasini to‘g‘ri o‘qidingiz.');
});
