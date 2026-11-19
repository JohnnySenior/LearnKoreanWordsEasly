using Lugat.Application.Abstractions;
using Lugat.Application.Pronunciation;
using Lugat.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Catalog;

/// <summary>One step of the content job: what happened, and whether the job ends (done, or stopped on a problem).</summary>
public sealed record AutopilotStep(string Message, bool Finished, bool Stopped = false, int? PublishedUnit = null);

/// <summary>
/// L-87: the AI prepares and publishes a level's units without an editor reading each word. One call to
/// <see cref="StepAsync"/> does one piece of work (one AI call or one publication), so the background job can stop
/// between any two. A unit is published only when the server's own rules pass every word: the card's fields
/// (<see cref="CatalogContent"/>, already checked when drafted) are complete. AI-drafted trap scenes are confirmed,
/// and a reading is confirmed only when the standard rules explain it (<see cref="HangulRules"/>); a reading they do
/// not explain, or one the AI was unsure of, waits for the editor and learners do not see it meanwhile.
/// The introduction lesson (audio) is not part of it.
/// </summary>
public sealed class ContentAutopilotService(IAppDbContext db, CatalogAdminService admin, UnitPreparationService preparation, PronunciationBatchService pronunciations, IOperationLock operations, TimeProvider time)
{
    public async Task<AutopilotStep> StepAsync(Guid userId, CatalogLevel level, CancellationToken ct)
    {
        var units = await db.Units.AsNoTracking().Where(u => u.Level == level).OrderBy(u => u.Number)
            .Select(u => new { u.Id, u.Number, u.Status }).ToListAsync(ct);
        if (units.Count == 0)
        {
            var split = await admin.SplitAsync(level, ct);
            return split.Units == 0
                ? new($"{level} darajada bo‘limga bo‘ladigan so‘z yo‘q. Avval rasmiy ro‘yxatni import qiling.", true, true)
                : new($"{level} daraja {split.Units} ta bo‘limga bo‘lindi.", false);
        }

        var next = units.FirstOrDefault(u => u.Status != UnitStatus.Published);
        if (next is null)
        {
            return new($"{level} darajaning hamma {units.Count} ta bo‘limi nashrda.", true);
        }

        var unit = await admin.UnitAsync(next.Id, ct);
        if (UnitPreparationService.Remaining(unit) > 0)
        {
            if (unit.Remaining == 0 && unit.ScenesRemaining == 0)
            {
                // Readings last: a word whose answer fails the checks is left to the editor, not asked forever.
                await pronunciations.DraftAsync(userId, next.Id, false, ct, leaveRejected: true);
                var left = UnitPreparationService.ReadingsRemaining(await admin.UnitAsync(next.Id, ct));
                return new($"{next.Number}-bo‘lim: talaffuzlar, yana {left} ta qoldi.", false);
            }

            var prepared = await preparation.NextAsync(userId, next.Id, ct);
            return new($"{next.Number}-bo‘lim: {prepared.Step.ToLowerInvariant()}, yana {prepared.Remaining} ta qoldi.", false);
        }

        var (incomplete, unsure) = await ApproveAsync(next.Id, ct);
        if (incomplete.Count > 0)
        {
            return new($"{next.Number}-bo‘lim to‘xtadi: {string.Join(", ", incomplete)} so‘zining kartasi to‘liq emas. Bo‘lim sahifasida to‘ldiring yoki qoralamani qayta so‘rang, keyin davom ettiring.", true, true);
        }

        await admin.PublishAsync(next.Id, ct);
        return new($"{next.Number}-bo‘lim nashr qilindi.{(unsure > 0 ? $" {unsure} ta so‘zning talaffuzi sizning tekshiruvingizni kutadi." : "")}", false, PublishedUnit: next.Number);
    }

    /// <summary>Confirms what the server's rules pass; returns the words whose card is incomplete and the number of readings left to the editor.</summary>
    public async Task<(List<string> Incomplete, int UnsureReadings)> ApproveAsync(Guid unitId, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync(CatalogAdminService.LockKey, ct);
        var words = await db.CatalogWords.Where(w => w.UnitId == unitId).OrderBy(w => w.OrderInUnit).ToListAsync(ct);
        var now = time.GetUtcNow();
        var incomplete = new List<string>();
        var unsure = 0;
        foreach (var word in words)
        {
            if (word.TrapScenePending)
            {
                (word.TrapScenePending, word.UpdatedAt) = (false, now);
                word.ContentVersion++;
            }

            if (!word.PronunciationReviewed)
            {
                if (word.PronunciationKo is { } reading && HangulRules.Explain(word.Word, reading).Complete)
                {
                    (word.PronunciationReviewed, word.UpdatedAt) = (true, now);
                    word.ContentVersion++;
                }
                else
                {
                    unsure++;
                }
            }

            if (word.Status == CatalogWordStatus.Reviewed)
            {
                continue;
            }

            if (!CatalogAdminService.Fields(word).IsComplete)
            {
                incomplete.Add(word.Word);
                continue;
            }

            (word.Status, word.ReviewedByUserId, word.ReviewedAt, word.ReviewedByAi, word.UpdatedAt) = (CatalogWordStatus.Reviewed, null, now, true, now);
        }

        await db.SaveChangesAsync(ct);
        return (incomplete, unsure);
    }
}
