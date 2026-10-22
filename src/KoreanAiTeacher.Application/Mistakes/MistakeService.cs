using KoreanAiTeacher.Application.Abstractions;
using KoreanAiTeacher.Application.Grammar;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Domain.Grammar;
using KoreanAiTeacher.Domain.Learning;
using KoreanAiTeacher.Domain.Testing;
using KoreanAiTeacher.Domain.Writing;
using Microsoft.EntityFrameworkCore;

namespace KoreanAiTeacher.Application.Mistakes;

/// <summary>
/// The mistakes notebook (D-41): every recent result of a word or a grammar point, from cards, tests (oral included),
/// writing, grammar sentences and listening, folded per item. Nothing new is stored; it is read from the existing history.
/// </summary>
public sealed class MistakeService(IAppDbContext db, GrammarStudyService grammar, TimeProvider time)
{
    public const int Days = 60;

    public const int MaxItems = 100;

    private sealed record Result(Guid Id, DateTimeOffset At, bool Ok, string Source);

    public Task<MistakesDto> GetAsync(Guid ownerId, CancellationToken ct) => GetAsync(ownerId, Days, ct);

    /// <param name="days">The window; the weak-points summary (D-54) reads the last 7 or 30 days.</param>
    public async Task<MistakesDto> GetAsync(Guid ownerId, int days, CancellationToken ct)
    {
        var since = time.GetUtcNow().AddDays(-days);
        var words = new List<Result>();
        var grammarResults = new List<Result>();

        // Cards: "Eslay olmadim" is a mistake; a technical error is not a result at all (SRS04).
        var reviews = await db.ReviewEvents
            .Where(e => e.OwnerId == ownerId && e.UndoneAt == null && e.OccurredAt >= since && e.Answer != ReviewAnswer.TechnicalError)
            .Select(e => new { e.WordSenseId, e.OccurredAt, e.Answer })
            .AsNoTracking()
            .ToListAsync(ct);
        words.AddRange(reviews.Select(e => new Result(e.WordSenseId, e.OccurredAt, e.Answer != ReviewAnswer.Forgot, "kartochka")));

        // Tests: wrong answers (unanswered ones too, as in the score); oral technical failures are left out (D-38).
        var runs = await db.TestRuns
            .Where(r => r.OwnerId == ownerId && r.Status == TestRunStatus.Finished && r.FinishedAt >= since)
            .AsNoTracking()
            .ToListAsync(ct);
        foreach (var run in runs)
        {
            var answers = run.Answers.ToDictionary(a => a.Index);
            foreach (var q in run.Questions)
            {
                var a = answers.GetValueOrDefault(q.Index);
                if (a is { Technical: true })
                {
                    continue;
                }

                var at = a?.AnsweredAt ?? run.FinishedAt!.Value;
                var ok = a?.IsCorrect ?? false;
                if (q.WordSenseId is { } sense)
                {
                    words.Add(new Result(sense, at, ok, q.Kind switch
                    {
                        TestQuestionKind.OralKorean or TestQuestionKind.OralMeaning => "og‘zaki",
                        TestQuestionKind.WordUsage => "qo‘llanish",
                        _ => "sinov",
                    }));
                }
                else if (q.GrammarItemId is { } item)
                {
                    grammarResults.Add(new Result(item, at, ok, "sinov"));
                }
            }
        }

        // Sentence ladder (D-47): a filled gap tests the grammar point; a translation tests the word and the grammar.
        var ladders = await db.SentenceLadders
            .Where(l => l.OwnerId == ownerId && l.CreatedAt >= since)
            .AsNoTracking()
            .ToListAsync(ct);
        foreach (var ladder in ladders)
        {
            foreach (var a in ladder.Answers.Where(a => a.Step != Domain.Ladder.LadderStep.Order))
            {
                var item = ladder.Items.Single(i => i.Index == a.Index);
                if (item.GrammarItemId is { } point)
                {
                    grammarResults.Add(new Result(point, a.AnsweredAt, a.IsCorrect, "zinapoya"));
                }

                if (a.Step == Domain.Ladder.LadderStep.Translate && item.WordSenseId is { } sense)
                {
                    words.Add(new Result(sense, a.AnsweredAt, a.IsCorrect, "zinapoya"));
                }
            }
        }

        // Writing practice: a wrong word is a mistake; a spelling slip or a synonym is not.
        var writing = await db.WritingAttempts
            .Where(w => w.OwnerId == ownerId && w.CreatedAt >= since)
            .Select(w => new { w.WordSenseId, w.CreatedAt, w.Verdict })
            .AsNoTracking()
            .ToListAsync(ct);
        words.AddRange(writing.Select(w => new Result(w.WordSenseId, w.CreatedAt, w.Verdict != WritingVerdict.Wrong, "yozma")));

        // Grammar sentences checked by the AI (GR03): not used correctly is a mistake.
        var zone = (await db.Users.AsNoTracking().SingleAsync(u => u.Id == ownerId, ct)).Zone();
        foreach (var (item, attempts) in await grammar.AttemptsAsync(ownerId, null, ct))
        {
            foreach (var attempt in attempts)
            {
                var at = new DateTimeOffset(attempt.Day.ToDateTime(TimeOnly.MinValue), zone.GetUtcOffset(attempt.Day.ToDateTime(TimeOnly.MinValue)));
                if (at >= since)
                {
                    grammarResults.Add(new Result(item, at, attempt.Good, "gap tuzish"));
                }
            }
        }

        // Listening (D-53): wrong answers, newest first; a question has no word or grammar point of its own.
        var listening = (await db.ListeningSets.Where(l => l.OwnerId == ownerId && l.CreatedAt >= since).AsNoTracking().ToListAsync(ct))
            .SelectMany(l => l.Answers.Where(a => !a.IsCorrect).Select(a => new MistakeListeningDto(l.Id, a.Index, l.Items[a.Index].QuestionKo, l.Items[a.Index].Options[l.Items[a.Index].Answer], a.AnsweredAt)))
            .OrderByDescending(m => m.At)
            .Take(MaxItems)
            .ToList();

        var wordFolds = Fold(words);
        var senseIds = wordFolds.Select(f => f.Id).ToList();
        var senses = await db.WordSenses.Where(s => s.OwnerId == ownerId && s.IsActive && senseIds.Contains(s.Id))
            .AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var grammarFolds = Fold(grammarResults);
        var itemIds = grammarFolds.Select(f => f.Id).ToList();
        var items = await db.GrammarItems.Where(i => i.OwnerId == ownerId && i.Status != GrammarItemStatus.Excluded && itemIds.Contains(i.Id))
            .AsNoTracking().ToDictionaryAsync(i => i.Id, ct);

        return new MistakesDto(
            days,
            wordFolds.Where(f => senses.ContainsKey(f.Id)).Take(MaxItems)
                .Select(f => new MistakeWordDto(f.Id, senses[f.Id].Korean, senses[f.Id].Uzbek, senses[f.Id].TableNumber, f.Mistakes, f.LastMistakeAt, f.Sources, f.Improving))
                .ToList(),
            grammarFolds.Where(f => items.ContainsKey(f.Id)).Take(MaxItems)
                .Select(f => new MistakeGrammarDto(f.Id, items[f.Id].Form, items[f.Id].MeaningUz, f.Mistakes, f.LastMistakeAt, f.Sources, f.Improving))
                .ToList(),
            listening);
    }

    private sealed record Folded(Guid Id, int Mistakes, DateTimeOffset LastMistakeAt, IReadOnlyList<string> Sources, bool Improving);

    /// <summary>Items with at least one mistake: still wrong first (most mistakes, then the most recent), improving ones last.</summary>
    private static List<Folded> Fold(List<Result> results) =>
        results.GroupBy(r => r.Id)
            .Where(g => g.Any(r => !r.Ok))
            .Select(g =>
            {
                var ordered = g.OrderBy(r => r.At).ToList();
                var wrong = ordered.Where(r => !r.Ok).ToList();
                var last = wrong[^1].At;
                return new Folded(g.Key, wrong.Count, last, wrong.Select(r => r.Source).Distinct().ToList(), ordered[^1].Ok && ordered[^1].At > last);
            })
            .OrderBy(f => f.Improving)
            .ThenByDescending(f => f.Mistakes)
            .ThenByDescending(f => f.LastMistakeAt)
            .ToList();
}
