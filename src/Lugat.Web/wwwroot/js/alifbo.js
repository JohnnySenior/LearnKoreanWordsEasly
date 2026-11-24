// L-93: /alifbo — plays the recorded letter sounds and checks the "O‘qib ko‘ring" answers. The page is whole without
// it: the play buttons stay hidden and the questions stay readable.

// The visitor's choice among the options: which one is right and whether the choice was it.
export function grade(options, chosen) {
    const right = options.findIndex(o => o.right);
    return { right, correct: chosen === right };
}

export function scoreText(total, found) {
    return `${total} ta so‘zdan ${found} tasini to‘g‘ri o‘qidingiz.`;
}

function mountAudio() {
    const buttons = [...document.querySelectorAll('.say[data-audio]')];
    if (buttons.length === 0 || typeof Audio === 'undefined') return;
    const audio = new Audio();
    let current = null;
    const stop = () => { current?.classList.remove('playing'); current = null; };
    audio.addEventListener('ended', stop);
    audio.addEventListener('error', stop);
    for (const button of buttons) {
        button.hidden = false;
        button.addEventListener('click', () => {
            stop();
            current = button;
            button.classList.add('playing');
            audio.src = button.dataset.audio;
            audio.play().catch(stop);
        });
    }
}

function mountQuiz() {
    const score = document.getElementById('score');
    const questions = [...document.querySelectorAll('.quiz-q')];
    let answered = 0, found = 0;
    for (const q of questions) {
        const buttons = [...q.querySelectorAll('.opt')];
        buttons.forEach((button, chosen) => button.addEventListener('click', () => {
            if (q.dataset.done) return;
            q.dataset.done = '1';
            const { right, correct } = grade(buttons.map(b => ({ right: b.dataset.right === '1' })), chosen);
            buttons.forEach((b, n) => { b.disabled = true; if (n === right) b.classList.add('right'); });
            if (!correct) button.classList.add('wrong');
            const verdict = q.querySelector('.verdict');
            verdict.textContent = correct ? '✓ To‘g‘ri.' : `✗ To‘g‘risi: ${buttons[right].textContent}`;
            verdict.className = 'verdict ' + (correct ? 'ok' : 'bad');
            verdict.hidden = false;
            answered++; if (correct) found++;
            if (score && answered === questions.length) {
                score.textContent = scoreText(score.dataset.total, found);
                score.hidden = false;
            }
        }));
    }
}

if (typeof document !== 'undefined') {
    try { mountAudio(); } catch { }
    mountQuiz();
}
