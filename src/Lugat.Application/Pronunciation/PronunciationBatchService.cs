using System.Text.Json;
using Lugat.Application.Abstractions;
using Lugat.Application.Ai;
using Lugat.Application.Catalog;
using Lugat.Application.Teaching;
using Lugat.Contracts;
using Lugat.Domain.Teaching;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Pronunciation;

/// <summary>Prepare readings once per list, review them together. No AI draft is automatically approved.</summary>
public sealed class PronunciationBatchService(IAppDbContext db, AiGateway ai, IOperationLock operations, TimeProvider time)
{
    public const int WordsPerCall = 10;
    public sealed record Word(Guid Id, string Korean, string? Meaning, uint Version);
    public sealed record Draft(Guid Id, string? Korean, string? Reading, string? ExplanationUz);
    private sealed record Answer(List<Draft>? Words);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task DraftAsync(Guid userId, Guid listId, bool group, CancellationToken ct)
    {
        await AccessAsync(userId, listId, group, ct);
        await using var lease = await operations.AcquireAsync(group ? "group-deck:" + listId : "pronunciation-unit:" + listId, ct);
        var words = group
            ? await db.GroupDeckWords.AsNoTracking().Where(w => w.GroupDeckId == listId && !w.PronunciationReviewed && w.PronunciationKo == null && w.PronunciationAskedAt == null)
                .OrderBy(w => w.Order).Take(WordsPerCall).Select(w => new Word(w.Id, w.Korean, w.Uzbek, w.Version)).ToListAsync(ct)
            : await db.CatalogWords.AsNoTracking().Where(w => w.UnitId == listId && !w.PronunciationReviewed && w.PronunciationKo == null && w.PronunciationAskedAt == null)
                .OrderBy(w => w.OrderInUnit).Take(WordsPerCall).Select(w => new Word(w.Id, w.Word, w.TranslationUz, w.Version)).ToListAsync(ct);
        if (words.Count == 0) return;
        var result = await ai.CompleteJsonAsync(userId, BuildRequest(words, group), ct);
        var drafts = Parse(result.Response.Json, words);
        if (drafts.Count == 0)
        {
            await ai.RejectContentAsync(result, "Talaffuz ro‘yxati tekshiruvdan o‘tmadi.");
            throw new ServiceUnavailableException("AI yaroqli talaffuzlar ro‘yxatini qaytarmadi. Saqlangan takliflar saqlanib qoladi; qayta urinib ko‘ring.");
        }
        // Re-read after the provider returns: never overwrite an editor's concurrent work, even with null drafts.
        db.DiscardChanges();
        var versions = words.ToDictionary(w => w.Id, w => w.Version);
        var ids = drafts.Keys.ToList();
        var now = time.GetUtcNow();
        if (group)
        {
            foreach (var word in await db.GroupDeckWords.Where(w => w.GroupDeckId == listId && ids.Contains(w.Id)).ToListAsync(ct))
            {
                if (word.Version != versions[word.Id]) continue;
                var draft = drafts[word.Id];
                (word.PronunciationKo, word.PronunciationNoteUz, word.PronunciationAskedAt) = (draft.Reading, draft.ExplanationUz, now);
            }
        }
        else
        {
            await using var catalogLease = await operations.AcquireAsync(CatalogAdminService.LockKey, ct);
            foreach (var word in await db.CatalogWords.Where(w => w.UnitId == listId && ids.Contains(w.Id)).ToListAsync(ct))
            {
                if (word.Version != versions[word.Id]) continue;
                var draft = drafts[word.Id];
                (word.PronunciationKo, word.PronunciationNoteUz, word.PronunciationAskedAt, word.UpdatedAt) = (draft.Reading, draft.ExplanationUz, now, now);
                word.ContentVersion++;
            }
            await SaveAsync(ct);
            return;
        }
        await SaveAsync(ct);
    }

    public async Task ReviewAsync(Guid userId, Guid listId, bool group, ReviewPronunciationsRequest request, CancellationToken ct)
    {
        await AccessAsync(userId, listId, group, ct);
        if (request.Words is not { Count: > 0 and <= 200 } || request.Words.Any(w => w is null) || request.Words.Select(w => w.Id).Distinct().Count() != request.Words.Count)
            throw new ValidationException("Tasdiqlash uchun takrorlanmagan so‘zlarni tanlang (1–200 ta).");
        await using var lease = await operations.AcquireAsync(group ? "group-deck:" + listId : CatalogAdminService.LockKey, ct);
        var inputs = request.Words.ToDictionary(w => w.Id);
        var ids = inputs.Keys.ToList();
        var now = time.GetUtcNow();
        if (group)
        {
            var words = await db.GroupDeckWords.Where(w => w.GroupDeckId == listId && ids.Contains(w.Id)).ToListAsync(ct);
            if (words.Count != ids.Count) throw new NotFoundException("Lug‘atdagi so‘zlar topilmadi.");
            // Validate the whole selection before changing anything; a stale row rejects the whole approval.
            var values = words.ToDictionary(w => w.Id, w => Checked(w.Korean, w.Version, inputs[w.Id]));
            foreach (var word in words)
                (word.PronunciationKo, word.PronunciationNoteUz, word.PronunciationReviewed) = (values[word.Id].Reading, values[word.Id].Explanation, true);
            foreach (var copy in await db.PersonalWords.Where(p => p.GroupDeckWordId != null && ids.Contains(p.GroupDeckWordId.Value) && p.FromGroupDeck).ToListAsync(ct))
                (copy.PronunciationKo, copy.PronunciationNoteUz, copy.PronunciationReviewed) = (values[copy.GroupDeckWordId!.Value].Reading, values[copy.GroupDeckWordId.Value].Explanation, true);
        }
        else
        {
            var words = await db.CatalogWords.Where(w => w.UnitId == listId && ids.Contains(w.Id)).ToListAsync(ct);
            if (words.Count != ids.Count) throw new NotFoundException("Bo‘limdagi so‘zlar topilmadi.");
            var values = words.ToDictionary(w => w.Id, w => Checked(w.Word, w.Version, inputs[w.Id]));
            foreach (var word in words)
            {
                (word.PronunciationKo, word.PronunciationNoteUz, word.PronunciationReviewed) = (values[word.Id].Reading, values[word.Id].Explanation, true);
                word.ContentVersion++;
                (word.ReviewedByUserId, word.ReviewedAt, word.UpdatedAt) = (userId, now, now);
            }
        }
        await SaveAsync(ct);
    }

    private static (string? Reading, string? Explanation) Checked(string korean, uint version, PronunciationWordReviewInput input)
    {
        if (input.Version != version) throw new ConflictException("Ro‘yxatdagi so‘z o‘zgargan. Sahifani yangilab qayta tekshiring; hech biri tasdiqlanmadi.");
        return PronunciationService.Check(korean, input.Reading, input.ExplanationUz, true);
    }

    private async Task AccessAsync(Guid userId, Guid listId, bool group, CancellationToken ct)
    {
        if (group)
        {
            if (!await db.GroupDecks.AnyAsync(d => d.Id == listId && d.TeacherUserId == userId && d.Status != GroupDeckStatus.Archived
                && db.Groups.Any(g => g.Id == d.GroupId && g.ClosedAt == null), ct)) throw new NotFoundException("Lug‘at topilmadi.");
        }
        else if (!await db.Units.AnyAsync(u => u.Id == listId, ct)) throw new NotFoundException("Bo‘lim topilmadi.");
    }

    public static Dictionary<Guid, Draft> Parse(string json, IReadOnlyList<Word> words)
    {
        Answer? answer;
        try { answer = JsonSerializer.Deserialize<Answer>(json, Json); }
        catch (JsonException) { return []; }
        var expected = words.ToDictionary(w => w.Id);
        var result = new Dictionary<Guid, Draft>();
        var rows = (answer?.Words ?? []).OfType<Draft>().ToList();
        var duplicates = rows.GroupBy(d => d.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        foreach (var draft in rows)
        {
            if (duplicates.Contains(draft.Id) || !expected.TryGetValue(draft.Id, out var word) || draft.Korean != word.Korean) continue;
            try
            {
                var value = PronunciationService.Check(word.Korean, draft.Reading, draft.ExplanationUz, false);
                result[draft.Id] = draft with { Reading = value.Reading, ExplanationUz = value.Explanation };
            }
            catch (ValidationException) { /* A malformed answer stays unasked, ready to retry. */ }
        }
        return result;
    }

    public static AiJsonRequest BuildRequest(IReadOnlyList<Word> words, bool group = false) => new(
        group ? GroupDeckService.Feature : CatalogDraftService.Feature,
        PronunciationService.Instructions + "\nReturn a words array, one entry per input. Copy each id and korean exactly. A null reading means you are unsure, not unchanged pronunciation.",
        JsonSerializer.Serialize(words.Select(w => new { id = w.Id, korean = w.Korean, meaning = w.Meaning })),
        "pronunciation_list", Schema, 4000, Timeout: TimeSpan.FromSeconds(75));

    private const string Schema = """
        {"type":"object","additionalProperties":false,"required":["words"],"properties":{"words":{"type":"array","maxItems":10,
        "items":{"type":"object","additionalProperties":false,"required":["id","korean","reading","explanationUz"],"properties":{
        "id":{"type":"string"},"korean":{"type":"string"},"reading":{"type":["string","null"]},"explanationUz":{"type":["string","null"]}}}}}}
        """;

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("So‘zlar o‘zgargan. Sahifani yangilab qayta urinib ko‘ring."); }
    }
}
