using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Learning;
using Lugat.Domain.Testing;
using Lugat.Domain.Writing;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Mistakes;

/// <summary>
/// The mistakes notebook (D-41): every recent result of a word, from cards, tests (oral included), the sentence
/// ladder, writing and listening, folded per word. Nothing new is stored; it is read from the existing history.
/// </summary>
public sealed class MistakeService(IAppDbContext db, TimeProvider time)
{
    public const int Days = 60;

    public const int MaxItems = 100;

    private sealed record Result(Guid Owner, Guid Id, DateTimeOffset At, bool Ok, string Source);

    public Task<MistakesDto> GetAsync(Guid ownerId, CancellationToken ct) => GetAsync(ownerId, Days, ct);

    /// <param name="days">The window; the weak-points summary (D-54) reads the last 7 or 30 days.</param>
    public async Task<MistakesDto> GetAsync(Guid ownerId, int days, CancellationToken ct) =>
        (await GetManyAsync([ownerId], days, listening: true, ct))[ownerId];

    /// <summary>
    /// The words of several learners at once (a teacher's group, spec §9.2): the same queries for all of them, so the
    /// number of database round trips does not grow with the group (audit 2026-10-26). Without listening mistakes.
    /// </summary>
    public async Task<Dictionary<Guid, IReadOnlyList<MistakeWordDto>>> WordsOfAsync(IReadOnlyCollection<Guid> owners, CancellationToken ct) =>
        (await GetManyAsync(owners, Days, listening: false, ct)).ToDictionary(p => p.Key, p => p.Value.Words);

    private async Task<Dictionary<Guid, MistakesDto>> GetManyAsync(IReadOnlyCollection<Guid> owners, int days, bool listening, CancellationToken ct)
    {
        var ids = owners.Distinct().ToList();
        var since = time.GetUtcNow().AddDays(-days);
        var words = new List<Result>();

        // Cards: "Eslay olmadim" is a mistake; a technical error is not a result at all (SRS04).
        var reviews = await db.ReviewEvents
            .Where(e => ids.Contains(e.OwnerId) && e.UndoneAt == null && e.OccurredAt >= since && e.Answer != ReviewAnswer.TechnicalError)
            .Select(e => new { e.OwnerId, e.LearningItemId, e.OccurredAt, e.Answer })
            .AsNoTracking()
            .ToListAsync(ct);
        words.AddRange(reviews.Select(e => new Result(e.OwnerId, e.LearningItemId, e.OccurredAt, e.Answer != ReviewAnswer.Forgot, "kartochka")));

        // Tests: wrong answers (unanswered ones too, as in the score); oral technical failures are left out (D-38).
        var runs = await db.TestRuns
            .Where(r => ids.Contains(r.OwnerId) && r.Status == TestRunStatus.Finished && r.FinishedAt >= since)
            .Select(r => new { r.OwnerId, r.Purpose, r.FinishedAt, r.Questions, r.Answers })
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
                if (q.LearningItemId is { } sense)
                {
                    words.Add(new Result(run.OwnerId, sense, at, ok, q.Kind switch
                    {
                        TestQuestionKind.OralKorean or TestQuestionKind.OralMeaning => "og‘zaki",
                        TestQuestionKind.WordUsage => "qo‘llanish",
                        _ when run.Purpose != TestPurpose.Practice => "bo‘lim testi", // spec §4.4
                        _ => "sinov",
                    }));
                }
            }
        }

        // Sentence ladder (D-47): a translation tests the word.
        var ladders = await db.SentenceLadders
            .Where(l => ids.Contains(l.OwnerId) && l.CreatedAt >= since)
            .Select(l => new { l.OwnerId, l.Items, l.Answers })
            .AsNoTracking()
            .ToListAsync(ct);
        foreach (var ladder in ladders)
        {
            foreach (var a in ladder.Answers.Where(a => a.Step == Domain.Ladder.LadderStep.Translate))
            {
                var item = ladder.Items.Single(i => i.Index == a.Index);
                if (item.LearningItemId is { } sense)
                {
                    words.Add(new Result(ladder.OwnerId, sense, a.AnsweredAt, a.IsCorrect, "zinapoya"));
                }
            }
        }

        // Writing practice: a wrong word is a mistake; a spelling slip or a synonym is not.
        var writing = await db.WritingAttempts
            .Where(w => ids.Contains(w.OwnerId) && w.CreatedAt >= since)
            .Select(w => new { w.OwnerId, w.LearningItemId, w.CreatedAt, w.Verdict })
            .AsNoTracking()
            .ToListAsync(ct);
        words.AddRange(writing.Select(w => new Result(w.OwnerId, w.LearningItemId, w.CreatedAt, w.Verdict != WritingVerdict.Wrong, "yozma")));

        // Listening (D-53): wrong answers, newest first; a question has no word of its own.
        var heard = listening
            ? (await db.ListeningSets.Where(l => ids.Contains(l.OwnerId) && l.CreatedAt >= since).AsNoTracking().ToListAsync(ct))
                .SelectMany(l => l.Answers.Where(a => !a.IsCorrect).Select(a => (l.OwnerId, Item: new MistakeListeningDto(l.Id, a.Index, l.Items[a.Index].QuestionKo, l.Items[a.Index].Options[l.Items[a.Index].Answer], a.AnsweredAt))))
                .ToList()
            : [];

        var folds = words.GroupBy(r => r.Owner).ToDictionary(g => g.Key, g => Fold(g.ToList()));
        var senseIds = folds.Values.SelectMany(f => f).Select(f => f.Id).Distinct().ToList();
        // A learner's word is an active item they have cards for (LearnerItems.Of), checked for all of them at once.
        var held = (await db.Cards.Where(c => ids.Contains(c.OwnerId) && senseIds.Contains(c.LearningItemId))
                .Select(c => new { c.OwnerId, c.LearningItemId }).Distinct().AsNoTracking().ToListAsync(ct))
            .Select(c => (c.OwnerId, c.LearningItemId)).ToHashSet();
        var senses = await db.LearningItems.Where(i => i.IsActive && senseIds.Contains(i.Id))
            .AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var units = await Study.LearnerItems.UnitLabelsAsync(db, senses.Values.Select(s => s.UnitId), ct);

        return ids.ToDictionary(owner => owner, owner => new MistakesDto(
            days,
            folds.GetValueOrDefault(owner, []).Where(f => senses.ContainsKey(f.Id) && held.Contains((owner, f.Id))).Take(MaxItems)
                .Select(f => new MistakeWordDto(f.Id, senses[f.Id].Korean, senses[f.Id].Uzbek,
                    senses[f.Id].UnitId is { } u ? units.GetValueOrDefault(u) : null, f.Mistakes, f.LastMistakeAt, f.Sources, f.Improving))
                .ToList(),
            heard.Where(h => h.OwnerId == owner).Select(h => h.Item).OrderByDescending(m => m.At).Take(MaxItems).ToList()));
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
