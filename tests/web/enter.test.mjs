import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../../src/Lugat.Web/wwwroot/js/app.js', import.meta.url), 'utf8');
const app = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

function el(tagName, { disabled = false, visible = true, enter = false, focusVisible = false } = {}) {
    return {
        tagName, disabled, isContentEditable: false,
        offsetParent: visible ? {} : null,
        hasAttribute: name => name === 'data-enter' && enter,
        matches: selector => selector === ':focus-visible' && focusVisible,
    };
}

test('Enter presses the first visible, enabled main button', () => {
    const disabled = el('BUTTON', { enter: true, disabled: true });
    const hidden = el('BUTTON', { enter: true, visible: false });
    const next = el('BUTTON', { enter: true });
    assert.equal(app.enterTarget(null, [disabled, hidden, next]), next);
    assert.equal(app.enterTarget(el('BODY'), []), null);
});

test('a field and a button reached with Tab keep their own Enter', () => {
    const next = el('BUTTON', { enter: true });
    assert.equal(app.enterTarget(el('INPUT'), [next]), null, 'the answer is sent from the field');
    assert.equal(app.enterTarget(el('TEXTAREA'), [next]), null);
    assert.equal(app.enterTarget(el('BUTTON', { focusVisible: true }), [next]), null);
    assert.equal(app.enterTarget(el('INPUT', { disabled: true }), [next]), next, 'after checking, the field is locked');
});

test('a button clicked with the mouse (the microphone) does not take Enter from the main button', () => {
    const next = el('BUTTON', { enter: true });
    assert.equal(app.enterTarget(el('BUTTON'), [next]), next);
});
