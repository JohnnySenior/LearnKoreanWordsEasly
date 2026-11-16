using Lugat.Application.Abstractions;
using Lugat.Application.Pronunciation;
using Lugat.Contracts;

namespace Lugat.Application.Catalog;

/// <summary>One user action prepares missing cards, trap scenes and readings. Publishing and approval stay explicit.</summary>
public sealed class UnitPreparationService(CatalogAdminService admin, CatalogDraftService cards, TrapSceneService traps,
    PronunciationBatchService pronunciations, IOperationLock operations)
{
    public static int ReadingsRemaining(UnitAdminDto unit) => unit.Words.Count(w =>
        w.PronunciationReview is null || w.PronunciationReview is { Reviewed: false, Reading: null, AskedAt: null });

    public static int Remaining(UnitAdminDto unit) => unit.Remaining + unit.ScenesRemaining + ReadingsRemaining(unit);

    public async Task<UnitPreparationDto> NextAsync(Guid userId, Guid unitId, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync("prepare-unit:" + unitId, ct);
        var before = await admin.UnitAsync(unitId, ct);
        UnitAdminDto after;
        string step;
        if (before.Remaining > 0)
        {
            step = "So‘zlar qoralamasi";
            after = await cards.DraftNextAsync(userId, unitId, ct);
            if (after.Remaining >= before.Remaining) throw new ServiceUnavailableException("AI so‘zlar qoralamasini tayyorlay olmadi. Saqlangan ish qoladi; qayta urinib ko‘ring.");
        }
        else if (before.ScenesRemaining > 0)
        {
            step = "Tuzoq sahnalari";
            after = await traps.DraftNextAsync(userId, unitId, ct);
            if (after.ScenesRemaining >= before.ScenesRemaining) throw new ServiceUnavailableException("Tuzoq sahnalarini tayyorlash to‘xtadi. Saqlangan ish qoladi; qayta urinib ko‘ring.");
        }
        else if (ReadingsRemaining(before) > 0)
        {
            step = "Talaffuzlar";
            await pronunciations.DraftAsync(userId, unitId, false, ct);
            after = await admin.UnitAsync(unitId, ct);
            if (ReadingsRemaining(after) >= ReadingsRemaining(before)) throw new ConflictException("So‘zlar o‘zgargan yoki AI javobi eskirgan. Yangilab davom ettiring.");
        }
        else return new(before, "Tayyor", 0, true);
        return new(after, step, Remaining(after), Remaining(after) == 0);
    }
}
