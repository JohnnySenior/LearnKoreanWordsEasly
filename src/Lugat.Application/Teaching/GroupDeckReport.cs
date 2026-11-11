using Lugat.Application.Abstractions;
using Lugat.Application.Mistakes;
using Lugat.Contracts;
using Lugat.Domain.Learning;
using Lugat.Domain.Teaching;
using Lugat.Domain.Testing;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Teaching;

/// <summary>
/// What a teacher sees about their own decks (L-68): for each learner of the group, how many of the deck's words are
/// learned, how much they practised them this week and their deck tests; for each word, how many learned it and how
/// often it was missed. Mistakes on a deck word are named after the deck, not the course unit the word may also be in.
/// Only the teacher's own open groups and current members (spec §13.8); counts and scores, never the answers.
/// </summary>
public sealed class GroupDeckReport(IAppDbContext db, TimeProvider time, MistakeService mistakes)
{
    public const int WeekDays = 7;

    public async Task<GroupDeckReportDto> ReportAsync(Guid teacherId, Guid deckId, CancellationToken ct)
    {
        var deck = await (from d in db.GroupDecks
                          join g in db.Groups on d.GroupId equals g.Id
                          where d.Id == deckId && d.TeacherUserId == teacherId && g.TeacherUserId == teacherId && g.ClosedAt == null
                              && d.Status != GroupDeckStatus.Archived
                          select d).AsNoTracking().SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Lug‘at topilmadi.");
        var members = await (from m in db.GroupMembers
                             join u in db.Users on m.UserId equals u.Id
                             where m.GroupId == deck.GroupId && m.LeftAt == null && u.DeletedAt == null
                             select new { u.Id, u.DisplayName }).ToListAsync(ct);
        var deckWords = await db.GroupDeckWords.AsNoTracking().Where(w => w.GroupDeckId == deckId).OrderBy(w => w.Order).ToListAsync(ct);
        var ids = members.Select(m => m.Id).ToList();
        var links = await LinksAsync(ids, [deckId], ct);
        var states = await StatesAsync(links, ct);

        var since = time.GetUtcNow().AddDays(-WeekDays);
        var itemIds = links.Select(l => l.ItemId).Distinct().ToList();
        var reviewed = (await db.ReviewEvents
                .Where(e => ids.Contains(e.OwnerId) && itemIds.Contains(e.LearningItemId) && e.UndoneAt == null && e.OccurredAt >= since && e.Answer != ReviewAnswer.TechnicalError)
                .Select(e => new { e.OwnerId, e.LearningItemId, e.CardId }).Distinct().ToListAsync(ct))
            .Where(e => links.Any(l => l.Owner == e.OwnerId && l.ItemId == e.LearningItemId))
            .GroupBy(e => e.OwnerId).ToDictionary(g => g.Key, g => g.Select(e => e.CardId).Distinct().Count());
        var tests = (await TestsAsync(ids, [deckId], ct)).ToLookup(t => t.Owner);
        var now = time.GetUtcNow();
        var done = await DoneAsync(db, deckId, ids, Cutoff(deck, now), ct);

        var students = members.Select(m =>
        {
            var mine = links.Where(l => l.Owner == m.Id).ToList();
            var s = mine.Select(l => states.GetValueOrDefault((m.Id, l.ItemId))).ToList();
            var runs = tests[m.Id].OrderByDescending(t => t.At).ToList();
            return new DeckStudentDto(m.Id, m.DisplayName, mine.Count, s.Count(x => x == WordState.Learned), s.Count(x => x == WordState.Learning), s.Count(x => x == WordState.New),
                reviewed.GetValueOrDefault(m.Id), runs.Count, runs.FirstOrDefault()?.Percent, runs.FirstOrDefault()?.At, runs.Count == 0 ? null : runs.Max(r => r.Percent),
                done.GetValueOrDefault(m.Id));
        }).OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        var missed = await mistakes.WordsOfAsync(ids, ct);
        var wordStats = deckWords.Select(w =>
        {
            var on = links.Where(l => l.DeckWordId == w.Id).ToList();
            var errors = on.Select(l => missed.GetValueOrDefault(l.Owner)?.FirstOrDefault(x => x.LearningItemId == l.ItemId)).OfType<MistakeWordDto>().ToList();
            return new DeckWordStatDto(w.Korean, w.Uzbek, on.Count(l => states.GetValueOrDefault((l.Owner, l.ItemId)) == WordState.Learned),
                errors.Sum(e => e.Mistakes), errors.Count);
        }).OrderByDescending(w => w.Mistakes).ThenByDescending(w => w.Learners).ThenBy(w => w.Learned).ToList();
        return new GroupDeckReportDto(deck.Id, deck.Name, deckWords.Count, students, wordStats, deck.DueOn, deck.DueAt is { } due && due <= now);
    }

    /// <summary>The teacher's published decks one learner has, with their progress and last test (the learner's page).</summary>
    public async Task<IReadOnlyList<StudentDeckDto>> StudentDecksAsync(Guid teacherId, Guid learnerId, CancellationToken ct)
    {
        var decks = await (from d in db.GroupDecks
                           join g in db.Groups on d.GroupId equals g.Id
                           join m in db.GroupMembers on g.Id equals m.GroupId
                           where d.TeacherUserId == teacherId && g.ClosedAt == null && d.Status == GroupDeckStatus.Published && m.UserId == learnerId && m.LeftAt == null
                           orderby d.PublishedAt
                           select new { d.Id, d.Name, d.DueOn, d.DueAt }).Distinct().ToListAsync(ct);
        var deckIds = decks.Select(d => d.Id).ToList();
        var links = await LinksAsync([learnerId], deckIds, ct);
        var states = await StatesAsync(links, ct);
        var tests = (await TestsAsync([learnerId], deckIds, ct)).ToLookup(t => t.DeckId);
        var result = decks.Select(d =>
        {
            var mine = links.Where(l => l.DeckId == d.Id).ToList();
            var last = tests[d.Id].OrderByDescending(t => t.At).FirstOrDefault();
            return new StudentDeckDto(d.Id, d.Name, mine.Count, mine.Count(l => states.GetValueOrDefault((l.Owner, l.ItemId)) == WordState.Learned), last?.Percent, last?.At, d.DueOn);
        }).ToList();
        var now = time.GetUtcNow();
        for (var i = 0; i < result.Count; i++)
        {
            var due = decks[i].DueAt is { } at && at < now ? at : now;
            result[i] = result[i] with { Done = (await DoneAsync(db, result[i].DeckId, [learnerId], due, ct))[learnerId] };
        }

        return result;
    }

    /// <summary>
    /// Renames a mistake's "unit" to the deck's name when the word reached the learner through one of this teacher's
    /// decks: the teacher gave the word, so "A-9" would only confuse (Johnny, 2026-11-11).
    /// </summary>
    public async Task<Dictionary<(Guid Owner, Guid Item), string>> DeckLabelsAsync(Guid teacherId, IReadOnlyCollection<Guid> learners, CancellationToken ct)
    {
        var ids = learners.ToList();
        return (await (from p in db.PersonalWords
                       join d in db.GroupDecks on p.GroupDeckId equals d.Id
                       where ids.Contains(p.OwnerId) && d.TeacherUserId == teacherId && d.Status == GroupDeckStatus.Published
                       select new { p.OwnerId, p.LearningItemId, d.Name }).ToListAsync(ct))
            .GroupBy(x => (x.OwnerId, x.LearningItemId)).ToDictionary(g => g.Key, g => g.First().Name);
    }

    /// <summary>
    /// The homework of each learner for one deck (L-71): words seen on a card (any answer but a technical error, not
    /// undone) and the deck test, up to <paramref name="cutoff"/> — the due time, or now without one. Done when every
    /// word was seen and a test was taken; <see cref="DeckDoneDto.DoneAt"/> is when the later of the two first held.
    /// </summary>
    public static async Task<Dictionary<Guid, DeckDoneDto>> DoneAsync(IAppDbContext db, Guid deckId, IReadOnlyCollection<Guid> learners, DateTimeOffset cutoff, CancellationToken ct)
    {
        var ids = learners.ToList();
        var links = await (from p in db.PersonalWords
                           join i in db.LearningItems on p.LearningItemId equals i.Id
                           where ids.Contains(p.OwnerId) && p.GroupDeckId == deckId && p.GroupDeckWordId != null && i.IsActive
                           select new { p.OwnerId, p.LearningItemId }).ToListAsync(ct);
        var items = links.Select(l => l.LearningItemId).Distinct().ToList();
        var firstSeen = (await db.ReviewEvents
                .Where(e => ids.Contains(e.OwnerId) && items.Contains(e.LearningItemId) && e.UndoneAt == null && e.OccurredAt <= cutoff && e.Answer != ReviewAnswer.TechnicalError)
                .GroupBy(e => new { e.OwnerId, e.LearningItemId })
                .Select(g => new { g.Key.OwnerId, g.Key.LearningItemId, At = g.Min(e => e.OccurredAt) }).ToListAsync(ct))
            .ToDictionary(x => (x.OwnerId, x.LearningItemId), x => x.At);
        var tests = (await db.TestRuns.AsNoTracking()
                .Where(r => ids.Contains(r.OwnerId) && r.GroupDeckId == deckId && r.Status == TestRunStatus.Finished && r.FinishedAt <= cutoff)
                .Select(r => new { r.OwnerId, r.FinishedAt, r.Questions, r.Answers }).ToListAsync(ct))
            .Select(r => (r.OwnerId, At: r.FinishedAt!.Value,
                Percent: Catalog.UnitTestService.ScorePercent(r.Questions.Count - r.Answers.Count(a => a.Technical), r.Answers.Count(a => a.IsCorrect))))
            .ToLookup(t => t.OwnerId);
        return ids.ToDictionary(id => id, id =>
        {
            var mine = links.Where(l => l.OwnerId == id).Select(l => l.LearningItemId).Distinct().ToList();
            var seen = mine.Where(i => firstSeen.ContainsKey((id, i))).Select(i => firstSeen[(id, i)]).ToList();
            var runs = tests[id].OrderBy(t => t.At).ToList();
            var allSeen = mine.Count > 0 && seen.Count == mine.Count;
            var done = allSeen && runs.Count > 0;
            DateTimeOffset? doneAt = done ? new[] { seen.Max(), runs.First().At }.Max() : null;
            return new DeckDoneDto(mine.Count, seen.Count, runs.Count > 0, runs.Count == 0 ? null : runs[^1].Percent, done, doneAt);
        });
    }

    /// <summary>The cut-off for "done": the due time, or now while there is none.</summary>
    public static DateTimeOffset Cutoff(GroupDeck deck, DateTimeOffset now) => deck.DueAt is { } due && due < now ? due : now;

    private enum WordState { New, Learning, Learned }

    private sealed record Link(Guid Owner, Guid DeckId, Guid DeckWordId, Guid ItemId);

    private sealed record DeckTest(Guid Owner, Guid DeckId, int Percent, DateTimeOffset At);

    /// <summary>Which learning item each learner practises for each word of the decks (active items only).</summary>
    private async Task<List<Link>> LinksAsync(List<Guid> learners, List<Guid> deckIds, CancellationToken ct) =>
        (await (from p in db.PersonalWords
                join i in db.LearningItems on p.LearningItemId equals i.Id
                where learners.Contains(p.OwnerId) && p.GroupDeckId != null && deckIds.Contains(p.GroupDeckId.Value) && p.GroupDeckWordId != null && i.IsActive
                select new { p.OwnerId, p.GroupDeckId, p.GroupDeckWordId, p.LearningItemId }).ToListAsync(ct))
            .Select(x => new Link(x.OwnerId, x.GroupDeckId!.Value, x.GroupDeckWordId!.Value, x.LearningItemId)).ToList();

    /// <summary>New (no card answered), learned (both cards mastered, as in "Mening so‘zlarim") or learning.</summary>
    private async Task<Dictionary<(Guid, Guid), WordState>> StatesAsync(List<Link> links, CancellationToken ct)
    {
        var owners = links.Select(l => l.Owner).Distinct().ToList();
        var items = links.Select(l => l.ItemId).Distinct().ToList();
        var now = time.GetUtcNow();
        return (await db.Cards.AsNoTracking().Where(c => owners.Contains(c.OwnerId) && items.Contains(c.LearningItemId)).ToListAsync(ct))
            .GroupBy(c => (c.OwnerId, c.LearningItemId))
            .ToDictionary(g => g.Key, g => g.Count() == 2 && g.All(c => Mastery.IsMastered(c.ToState(), now)) ? WordState.Learned
                : g.All(c => c.Phase == LearningPhase.New) ? WordState.New : WordState.Learning);
    }

    private async Task<List<DeckTest>> TestsAsync(List<Guid> learners, List<Guid> deckIds, CancellationToken ct) =>
        (await db.TestRuns.AsNoTracking()
            .Where(r => learners.Contains(r.OwnerId) && r.GroupDeckId != null && deckIds.Contains(r.GroupDeckId.Value) && r.Status == TestRunStatus.Finished)
            .Select(r => new { r.OwnerId, r.GroupDeckId, r.FinishedAt, r.Questions, r.Answers }).ToListAsync(ct))
            .Select(r => new DeckTest(r.OwnerId, r.GroupDeckId!.Value,
                Catalog.UnitTestService.ScorePercent(r.Questions.Count - r.Answers.Count(a => a.Technical), r.Answers.Count(a => a.IsCorrect)), r.FinishedAt!.Value))
            .ToList();
}
