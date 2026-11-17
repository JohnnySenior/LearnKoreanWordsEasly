using Lugat.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Pronunciation;

/// <summary>
/// L-82: writes the readable Uzbek form of old pronunciation notes (L-80, L-81) into the database once, so the notes
/// are clean where they are stored, not only where they are shown. Runs at start; a note already in its readable form
/// is left alone, so a second run changes nothing. A note whose readable form would be empty is kept as it is.
/// Reviews, readings, versions of content and progress are not touched.
/// </summary>
public static class PronunciationNoteBackfill
{
    public static async Task<int> RunAsync(IAppDbContext db, CancellationToken ct)
    {
        var changed = 0;
        foreach (var w in await db.CatalogWords.Where(w => w.PronunciationNoteUz != null).ToListAsync(ct))
        {
            changed += Apply(w.Word, w.PronunciationKo, w.PronunciationNoteUz, note => w.PronunciationNoteUz = note);
        }

        foreach (var w in await db.GroupDeckWords.Where(w => w.PronunciationNoteUz != null).ToListAsync(ct))
        {
            changed += Apply(w.Korean, w.PronunciationKo, w.PronunciationNoteUz, note => w.PronunciationNoteUz = note);
        }

        var personal = await (from p in db.PersonalWords
                              join i in db.LearningItems on p.LearningItemId equals i.Id
                              where p.PronunciationNoteUz != null
                              select new { Word = p, i.Korean }).ToListAsync(ct);
        foreach (var x in personal)
        {
            changed += Apply(x.Korean, x.Word.PronunciationKo, x.Word.PronunciationNoteUz, note => x.Word.PronunciationNoteUz = note);
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return changed;
    }

    private static int Apply(string korean, string? reading, string? note, Action<string> set)
    {
        var readable = PronunciationExplanation.ForWord(korean, reading, note);
        if (readable is null || readable == note)
        {
            return 0;
        }

        set(readable);
        return 1;
    }
}
