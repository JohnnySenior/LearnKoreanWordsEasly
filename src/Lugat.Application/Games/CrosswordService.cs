using System.Text.RegularExpressions;
using Lugat.Application.Abstractions;
using Lugat.Application.Catalog;
using Lugat.Application.Speech;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Lugat.Domain.Games;
using Lugat.Domain.Vocabulary;
using Microsoft.EntityFrameworkCore;

namespace Lugat.Application.Games;

/// <summary>
/// Crosswords (L-55): built from any word of the learner's open units, kept on the server one at a time, answered
/// and hinted here so the Korean of an open word never reaches the client. A game: no card is rescheduled; a revealed
/// word goes to the mistakes notebook.
/// </summary>
public sealed partial class CrosswordService(IAppDbContext db, TimeProvider time, UnitProgressService units, SpeechService speech)
{
    /// <summary>Two open units are needed: one unit's words cross too rarely (four words on average, L-55).</summary>
    public const int UnitsForCrossword = 2;

    private static readonly (CrosswordSize Size, string Title, int Words, int Units)[] Sizes =
    [
        (CrosswordSize.Small, "Kichik", 8, 2),
        (CrosswordSize.Medium, "O‘rta", 12, 2),
        (CrosswordSize.Large, "Katta", 16, 3),
    ];

    public async Task<CrosswordHomeDto> HomeAsync(Guid ownerId, CancellationToken ct)
    {
        var progress = await units.SyncAsync(ownerId, ct);
        var open = progress.Count;
        var active = await ActiveAsync(ownerId, ct);
        var current = await units.CurrentAsync(ownerId, ct);
        var unitIds = progress.Select(p => p.UnitId).ToList();
        var words = (await Rows(unitIds).ToListAsync(ct)).Where(r => Hangul().IsMatch(r.Korean))
            .GroupBy(r => r.Unit).ToDictionary(g => g.Key, g => g.Count());
        var list = await db.Units.Where(u => unitIds.Contains(u.Id)).OrderBy(u => u.Level).ThenBy(u => u.Number)
            .Select(u => new { u.Id, u.Level, u.Number }).ToListAsync(ct);
        // The teachers' decks (L-68): a crossword from one of them needs no open unit.
        var decks = (await Vocabulary.PersonalWordService.GroupDecksAsync(db, ownerId, ct))
            .Where(d => d.Words >= MinDeckWords).Select(d => new WordDeckOptionDto(d.Key, d.Name, d.Words)).ToList();
        return new CrosswordHomeDto(open, UnitsForCrossword,
            Sizes.Select(s => new CrosswordSizeDto(s.Size, s.Title, s.Words, s.Units, open >= s.Units || decks.Count > 0)).ToList(),
            active is null ? null : ToDto(active), current?.Id,
            list.Select(u => new CrosswordUnitDto(u.Id, Study.LearnerItems.Label(u.Level, u.Number), words.GetValueOrDefault(u.Id))).ToList(),
            decks);
    }

    /// <summary>A teacher's deck needs this many words for a crossword (fewer seldom cross).</summary>
    public const int MinDeckWords = 6;

    /// <summary>How many of the learner's last crosswords count as recent: their words wait until the others are used.</summary>
    public const int RecentCrosswords = 2;

    private sealed class WordRow
    {
        public Guid Id { get; init; }
        public string Korean { get; init; } = "";
        public string Uzbek { get; init; } = "";
        public Guid Unit { get; init; }
        public string? ExampleKo { get; init; }
        public string? ExampleUz { get; init; }
    }

    /// <summary>Catalog items are shared: the words of these units are the same for every learner.</summary>
    private IQueryable<WordRow> Rows(List<Guid> unitIds) =>
        from i in db.LearningItems
        join w in db.CatalogWords on i.CatalogWordId equals w.Id
        where i.IsActive && i.Kind == LearningItemKind.Catalog && i.UnitId != null && unitIds.Contains(i.UnitId.Value)
        select new WordRow { Id = i.Id, Korean = i.Korean, Uzbek = i.Uzbek, Unit = i.UnitId!.Value, ExampleKo = w.ExampleKo, ExampleUz = w.ExampleUz };

    public async Task<CrosswordDto> StartAsync(Guid ownerId, StartCrosswordRequest request, CancellationToken ct)
    {
        if (await db.Crosswords.SingleOrDefaultAsync(x => x.Id == request.Id, ct) is { } same)
        {
            return same.OwnerId == ownerId ? ToDto(same) : throw new ConflictException("Bu krossvord boshqa foydalanuvchiga tegishli.");
        }

        var size = Sizes.SingleOrDefault(s => s.Size == request.Size);
        if (size.Title is null)
        {
            throw new ValidationException("Krossvord o‘lchami noto‘g‘ri.");
        }

        List<WordRow> rows;
        if (Vocabulary.PersonalWordService.GroupDeckId(request.Deck) is { } deckId)
        {
            rows = await DeckRows(ownerId, deckId).AsNoTracking().ToListAsync(ct);
            if (rows.Count < MinDeckWords)
            {
                throw new ValidationException($"Krossvord uchun lug‘atda kamida {MinDeckWords} ta so‘z bo‘lsin.");
            }
        }
        else
        {
            rows = await UnitRowsAsync(ownerId, request, size, ct);
        }

        return await BuildAsync(ownerId, request, size.Words, rows, ct);
    }

    /// <summary>The words of a teacher's deck the learner has (L-68), with the example the deck gave.</summary>
    private IQueryable<WordRow> DeckRows(Guid ownerId, Guid deckId) =>
        from p in db.PersonalWords
        join i in db.LearningItems on p.LearningItemId equals i.Id
        where p.OwnerId == ownerId && p.GroupDeckId == deckId && i.IsActive
        select new WordRow { Id = i.Id, Korean = i.Korean, Uzbek = i.Uzbek, ExampleKo = p.ExampleKo, ExampleUz = p.ExampleUz }; // no unit: Guid.Empty by default

    private async Task<List<WordRow>> UnitRowsAsync(Guid ownerId, StartCrosswordRequest request, (CrosswordSize Size, string Title, int Words, int Units) size, CancellationToken ct)
    {
        var progress = await units.SyncAsync(ownerId, ct);
        if (progress.Count < size.Units)
        {
            throw new ValidationException($"{size.Title} krossvord {size.Units}-bo‘lim ochilganda ochiladi.");
        }

        var open = progress.Select(p => p.UnitId).ToList();
        var unitIds = request.UnitIds is { Count: > 0 } chosen ? chosen.Distinct().ToList() : open;
        if (unitIds.Any(u => !open.Contains(u)))
        {
            throw new ValidationException("Faqat ochilgan bo‘limlarni tanlash mumkin.");
        }

        var least = Math.Max(UnitsForCrossword, size.Units);
        if (unitIds.Count < least)
        {
            throw new ValidationException($"{size.Title} krossvord uchun kamida {least} ta bo‘lim tanlang.");
        }

        return await Rows(unitIds).AsNoTracking().ToListAsync(ct);
    }

    private async Task<CrosswordDto> BuildAsync(Guid ownerId, StartCrosswordRequest request, int wordCount, List<WordRow> rows, CancellationToken ct)
    {
        // Every word of the chosen units counts the same (L-55); only words of the last crosswords wait their turn.
        var recent = (await db.Crosswords.Where(x => x.OwnerId == ownerId).OrderByDescending(x => x.CreatedAt).Take(RecentCrosswords)
                .AsNoTracking().ToListAsync(ct))
            .SelectMany(x => x.Words).Select(w => w.LearningItemId).ToHashSet();
        var pool = rows.Where(r => Hangul().IsMatch(r.Korean))
            .Select(r => new CrosswordCandidate(r.Id, r.Korean, Clue(r.Uzbek), r.ExampleKo, r.ExampleUz, recent.Contains(r.Id)))
            .ToList();
        if (pool.Count < 2)
        {
            throw new ValidationException("Krossvord uchun so‘z yetarli emas.");
        }

        var (gridRows, gridCols, words) = CrosswordGenerator.Build(pool, wordCount, Random.Shared);
        if (words.Count < 2)
        {
            // A short teacher's list whose words share no syllable (L-68): no crossword, rather than a single word.
            throw new ValidationException("Bu so‘zlar bir-biri bilan kesishmaydi, krossvord tuzib bo‘lmadi. Boshqa mashqni tanlang.");
        }

        var now = time.GetUtcNow();
        foreach (var old in await db.Crosswords.Where(x => x.OwnerId == ownerId && x.Status == CrosswordStatus.Active).ToListAsync(ct))
        {
            old.Status = CrosswordStatus.Abandoned;
            old.UpdatedAt = now;
        }

        var crossword = new Crossword
        {
            Id = request.Id, OwnerId = ownerId, Size = request.Size, Status = CrosswordStatus.Active,
            Rows = gridRows, Cols = gridCols, Words = words, CreatedAt = now, UpdatedAt = now,
        };
        db.Crosswords.Add(crossword);
        await db.SaveChangesAsync(ct);
        return ToDto(crossword);
    }

    public async Task<CrosswordAnswerResultDto> AnswerAsync(Guid ownerId, Guid id, CrosswordAnswerRequest request, CancellationToken ct)
    {
        var crossword = await PlayAsync(ownerId, id, ct);
        var word = Word(crossword, request.Index);
        if (word.State != CrosswordWordState.Open)
        {
            return new CrosswordAnswerResultDto(word.State == CrosswordWordState.Found, ToDto(crossword));
        }

        var given = Regex.Replace(request.Answer ?? "", @"\s+", "");
        if (given != word.Korean)
        {
            return new CrosswordAnswerResultDto(false, ToDto(crossword));
        }

        Set(crossword, request.Index, word with { State = CrosswordWordState.Found, AnsweredAt = time.GetUtcNow() });
        await SaveAsync(crossword, ct);
        return new CrosswordAnswerResultDto(true, ToDto(crossword));
    }

    /// <summary>Hints get stronger: the example sentence, one syllable (any hidden one), then the whole word.</summary>
    public async Task<CrosswordDto> HintAsync(Guid ownerId, Guid id, CrosswordHintRequest request, CancellationToken ct)
    {
        var crossword = await PlayAsync(ownerId, id, ct);
        var word = Word(crossword, request.Index);
        if (word.State != CrosswordWordState.Open)
        {
            return ToDto(crossword);
        }

        var now = time.GetUtcNow();
        switch (request.Hint)
        {
            case CrosswordHint.Example:
                if (Blanked(word) is null)
                {
                    throw new ValidationException("Bu so‘zga misol gap yo‘q.");
                }

                word = word with { SawExample = true };
                break;
            case CrosswordHint.Syllable:
                var known = Known(crossword);
                var hidden = Enumerable.Range(0, word.Syllables().Length).Where(i => !known.Contains(word.Cell(i))).ToList();
                if (hidden.Count == 0)
                {
                    return ToDto(crossword);
                }

                // Several left: the first hidden one; the very last one is opened too, so a learner is never stuck.
                word = word with { Shown = [.. word.Shown ?? [], hidden[0]] };
                break;
            case CrosswordHint.Reveal:
                word = word with { State = CrosswordWordState.Revealed, AnsweredAt = now };
                break;
            default:
                throw new ValidationException("Noma’lum yordam.");
        }

        Set(crossword, request.Index, word);
        await SaveAsync(crossword, ct);
        return ToDto(crossword);
    }

    /// <summary>The word's sound (the usual Korean voice). Free, as on the cards: the page loads it with the clue.</summary>
    public async Task<byte[]> AudioAsync(Guid ownerId, Guid id, int index, CancellationToken ct) =>
        await speech.GetAsync(ownerId, Word(await FindAsync(ownerId, id, ct), index).Korean, ct);

    private async Task<Crossword?> ActiveAsync(Guid ownerId, CancellationToken ct) =>
        await db.Crosswords.Where(x => x.OwnerId == ownerId && x.Status == CrosswordStatus.Active)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);

    private async Task<Crossword> FindAsync(Guid ownerId, Guid id, CancellationToken ct) =>
        await db.Crosswords.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, ct)
        ?? throw new NotFoundException("Krossvord topilmadi.");

    private async Task<Crossword> PlayAsync(Guid ownerId, Guid id, CancellationToken ct)
    {
        var crossword = await FindAsync(ownerId, id, ct);
        return crossword.Status == CrosswordStatus.Active ? crossword : throw new ConflictException("Bu krossvord yakunlangan. Yangisini boshlang.");
    }

    private static CrosswordWord Word(Crossword crossword, int index) =>
        index >= 0 && index < crossword.Words.Count ? crossword.Words[index] : throw new NotFoundException("So‘z topilmadi.");

    private static void Set(Crossword crossword, int index, CrosswordWord word) =>
        crossword.Words = crossword.Words.Select((w, i) => i == index ? word : w).ToList();

    /// <summary>Completes words whose every syllable is already on the board, and finishes the game when all are done.</summary>
    private async Task SaveAsync(Crossword crossword, CancellationToken ct)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            var solved = Solved(crossword);
            for (var i = 0; i < crossword.Words.Count; i++)
            {
                var w = crossword.Words[i];
                if (w.State == CrosswordWordState.Open && Enumerable.Range(0, w.Syllables().Length).All(n => solved.Contains(w.Cell(n))))
                {
                    Set(crossword, i, w with { State = CrosswordWordState.Found });
                    changed = true;
                }
            }
        }

        var now = time.GetUtcNow();
        crossword.UpdatedAt = now;
        if (crossword.Words.All(w => w.State != CrosswordWordState.Open))
        {
            (crossword.Status, crossword.FinishedAt) = (CrosswordStatus.Finished, now);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Cells of found or revealed words.</summary>
    private static HashSet<(int, int)> Solved(Crossword crossword) =>
        crossword.Words.Where(w => w.State != CrosswordWordState.Open)
            .SelectMany(w => Enumerable.Range(0, w.Syllables().Length).Select(w.Cell)).ToHashSet();

    /// <summary>Cells whose syllable the learner may see: solved words and syllables opened by a hint.</summary>
    private static HashSet<(int, int)> Known(Crossword crossword)
    {
        var known = Solved(crossword);
        foreach (var w in crossword.Words)
        {
            foreach (var i in w.Shown ?? [])
            {
                known.Add(w.Cell(i));
            }
        }

        return known;
    }

    public static CrosswordDto ToDto(Crossword crossword)
    {
        var known = Known(crossword);
        var letters = new Dictionary<(int, int), string>();
        foreach (var w in crossword.Words)
        {
            var syl = w.Syllables();
            for (var i = 0; i < syl.Length; i++)
            {
                letters[w.Cell(i)] = syl[i];
            }
        }

        var numbers = crossword.Words.GroupBy(w => (w.Row, w.Col)).ToDictionary(g => g.Key, g => g.First().Number);
        var cells = letters.Keys.OrderBy(k => k.Item1).ThenBy(k => k.Item2)
            .Select(k => new CrosswordCellDto(k.Item1, k.Item2, known.Contains(k) ? letters[k] : null, numbers.TryGetValue(k, out var n) ? n : null))
            .ToList();
        var clues = crossword.Words.Select((w, i) =>
        {
            var syl = w.Syllables();
            var open = w.State == CrosswordWordState.Open;
            var blanked = Blanked(w);
            return new CrosswordClueDto(i, w.Number, w.Down, w.Uzbek, syl.Length, w.State, open ? null : w.Korean,
                Enumerable.Range(0, syl.Length).Select(n => known.Contains(w.Cell(n)) ? syl[n] : null).ToList(),
                blanked is not null, w.SawExample || !open ? blanked : null, w.SawExample || !open ? w.ExampleUz : null, w.Hints());
        }).ToList();
        var typed = crossword.Words.Where(w => w.State == CrosswordWordState.Found).ToList();
        return new CrosswordDto(crossword.Id, crossword.Size, crossword.Status, crossword.Rows, crossword.Cols, cells, clues,
            typed.Count, crossword.Words.Count(w => w.State == CrosswordWordState.Revealed), typed.Count(w => w.Hints() == 0));
    }

    /// <summary>The example with the word (or its stem, without 다) blanked; null when it cannot be hidden.</summary>
    private static string? Blanked(CrosswordWord w)
    {
        if (string.IsNullOrWhiteSpace(w.ExampleKo))
        {
            return null;
        }

        foreach (var form in new[] { w.Korean, w.Korean.EndsWith('다') && w.Korean.Length > 2 ? w.Korean[..^1] : null })
        {
            if (form is not null && w.ExampleKo.Contains(form, StringComparison.Ordinal))
            {
                return w.ExampleKo.Replace(form, "＿＿", StringComparison.Ordinal);
            }
        }

        return null;
    }

    /// <summary>The first meaning only, for a short clue ("bor bo‘lmoq; -da bo‘lmoq" → "bor bo‘lmoq").</summary>
    private static string Clue(string uzbek) => uzbek.Split(';')[0].Trim();

    [GeneratedRegex("^[가-힣]{2,8}$")]
    private static partial Regex Hangul();
}
