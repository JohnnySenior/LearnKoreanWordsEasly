using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Learning;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Study;

/// <summary>
/// Results screen numbers (spec UX08, SRS06). "Seen", "recalled on your own" and "learned" are kept apart, and each
/// direction is counted separately, so a high score in one direction does not hide a weak one.
/// </summary>
public sealed class ProgressService(IAppDbContext db, TimeProvider time)
{
    public const int HardestCount = 10;

    public async Task<ProgressDto> GetAsync(Guid ownerId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var rows = await (
                from c in db.Cards
                join s in db.LearningItems on c.LearningItemId equals s.Id
                where c.OwnerId == ownerId && s.IsActive
                select new { Card = c, s.Korean, s.Uzbek })
            .AsNoTracking()
            .ToListAsync(ct);

        var senses = rows.GroupBy(r => r.Card.LearningItemId).ToList();
        int Recalled(ReviewDirection d) => rows.Count(r => r.Card.Direction == d && r.Card.IndependentSuccessDays > 0);

        var learned = senses.Count(g =>
            g.Count() == 2 && g.All(r => Mastery.IsMastered(r.Card.ToState(), now)));

        var hardest = senses
            .Select(g => new HardWordDto(
                g.First().Korean,
                g.First().Uzbek,
                g.Where(r => r.Card.Direction == ReviewDirection.KoreanToUzbek).Sum(r => r.Card.Lapses),
                g.Where(r => r.Card.Direction == ReviewDirection.UzbekToKorean).Sum(r => r.Card.Lapses)))
            .Where(h => h.ForgotKoreanToUzbek + h.ForgotUzbekToKorean > 0)
            .OrderByDescending(h => h.ForgotKoreanToUzbek + h.ForgotUzbekToKorean)
            .Take(HardestCount)
            .ToList();

        var weekAgo = now.AddDays(-7);
        var reviews = await db.ReviewEvents.CountAsync(e => e.OwnerId == ownerId && e.UndoneAt == null && e.OccurredAt >= weekAgo, ct);

        return new ProgressDto(
            senses.Count,
            senses.Count(g => g.Any(r => r.Card.Phase != LearningPhase.New)),
            Recalled(ReviewDirection.KoreanToUzbek),
            Recalled(ReviewDirection.UzbekToKorean),
            learned,
            rows.Count(r => r.Card.Phase != LearningPhase.New && r.Card.DueAt <= now),
            reviews,
            hardest);
    }
}
