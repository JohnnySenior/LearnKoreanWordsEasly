using static Lugat.Application.Testing.TestQuestionGenerator;
using Lugat.Application.Abstractions;
using Lugat.Application.Ai;
using Lugat.Contracts;
using Lugat.Domain.Testing;
using Lugat.Domain.Vocabulary;
using Lugat.Domain.Writing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lugat.Application.Testing;

/// <summary>
/// Practice tests from the learner's own words (spec TEST01–TEST04, D-30). Choice questions are built
/// locally; gap sentences come from one AI call per test. When the AI is down the test still works, with choice
/// questions only. Tests never change the card schedule.
/// </summary>
public sealed class TestService(
    IAppDbContext db,
    TestQuestionGenerator generator,
    Speech.SpeakingService speaking,
    Speech.SpeechService speech,
    Vocabulary.UsageService usage,
    Catalog.UnitTestService unitTests,
    Catalog.UnitProgressService units,
    Premium.PremiumService premium,
    TimeProvider time,
    IOperationLock operations)
{
    public const string PromptVersion = "test-gaps-v1";

    public static readonly int[] Counts = [10, 20, 30];

    public const string Blank = "___";


    public async Task<TestOptionsDto> OptionsAsync(Guid ownerId, CancellationToken ct)
    {
        var units = await Study.LearnerItems.Of(db, ownerId)
            .Where(s => s.UnitId != null)
            .GroupBy(s => s.UnitId!.Value)
            .Select(g => new { UnitId = g.Key, Order = g.Min(s => s.Order), Words = g.Count() })
            .ToListAsync(ct);
        var labels = await Study.LearnerItems.UnitLabelsAsync(db, units.Select(u => (Guid?)u.UnitId), ct);
        return new TestOptionsDto(units.OrderBy(u => u.Order)
            .Select(u => new TestTableOptionDto(u.UnitId, labels.GetValueOrDefault(u.UnitId, "—"), u.Words)).ToList(),
            await Vocabulary.PersonalWordService.DeckOptionsAsync(db, ownerId, ct));
    }

    /// <summary>"A-1, A-2" for up to three units, otherwise "5 ta bo‘lim"; own words add "Mening so‘zlarim" (L-51).</summary>
    private async Task<string> UnitTitleAsync(IEnumerable<LearningItem> words, CancellationToken ct, bool own = false)
    {
        var ordered = words.Where(w => w.UnitId is not null).GroupBy(w => w.UnitId).OrderBy(g => g.Min(w => w.Order)).Select(g => g.Key).ToList();
        var labels = await Study.LearnerItems.UnitLabelsAsync(db, ordered, ct);
        var units = ordered.Count == 0 ? null
            : ordered.Count <= 3 ? string.Join(", ", ordered.Select(u => labels.GetValueOrDefault(u!.Value, "—"))) + "-bo‘lim" : $"{ordered.Count} ta bo‘lim";
        return own ? units is null ? "Mening so‘zlarim" : $"{units} + Mening so‘zlarim" : units ?? "—";
    }

    /// <summary>
    /// The words of the chosen units and own-word decks (L-51), each once. A word of an own deck may also be a unit's
    /// word; it is asked once.
    /// </summary>
    private async Task<(List<LearningItem> Words, bool Own)> ChosenWordsAsync(Guid ownerId, CreateTestRequest request, CancellationToken ct)
    {
        var tables = (request.UnitIds ?? []).Distinct().ToList();
        var decks = (request.Decks ?? []).Distinct().ToList();
        if (tables.Count == 0 && decks.Count == 0)
        {
            throw new ValidationException(request.Oral ? "Og‘zaki sinov uchun bo‘lim tanlang." : "Sinov uchun bo‘lim tanlang.");
        }

        var own = Vocabulary.PersonalWordService.DeckItems(db, ownerId, decks);
        var words = await Study.LearnerItems.Of(db, ownerId)
            .Where(s => (s.UnitId != null && tables.Contains(s.UnitId.Value)) || own.Contains(s.Id))
            .ToListAsync(ct);
        return (words, decks.Count > 0);
    }

    /// <summary>How many tests the history shows (newest first).</summary>
    public const int HistoryLimit = 100;

    public async Task<IReadOnlyList<TestSummaryDto>> ListAsync(Guid ownerId, CancellationToken ct, TestCategory? category = null)
    {
        var runs = await db.TestRuns.Where(r => r.OwnerId == ownerId).OrderByDescending(r => r.CreatedAt).Take(HistoryLimit * 2).ToListAsync(ct);
        return runs
            .Select(r => new TestSummaryDto(
                r.Id, r.Title, r.Status, r.CreatedAt, r.Questions.Count, r.Answers.Count,
                r.Status == TestRunStatus.Finished ? r.Answers.Count(a => a.IsCorrect) : null,
                CategoryOf(r),
                r.Questions.Count - r.Answers.Count(a => a.Technical)))
            .Where(r => category is null || r.Category == category)
            .Take(HistoryLimit)
            .ToList();
    }

    /// <summary>A test's kind from its questions (D-39); no column needed, so old tests are sorted too.</summary>
    public static TestCategory CategoryOf(TestRun run)
    {
        var kinds = run.Questions.Select(q => q.Kind).ToHashSet();
        return kinds.Overlaps([TestQuestionKind.OralKorean, TestQuestionKind.OralMeaning]) ? TestCategory.Oral : TestCategory.Vocabulary;
    }

    public async Task<TestDto> GetAsync(Guid ownerId, Guid id, CancellationToken ct) => await DtoAsync(await FindAsync(ownerId, id, ct), ct);

    public async Task<TestDto> CreateAsync(Guid ownerId, CreateTestRequest request, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync($"test:{request.Id}", ct);
        if (await db.TestRuns.SingleOrDefaultAsync(r => r.Id == request.Id, ct) is { } existing)
        {
            return existing.OwnerId == ownerId ? ToDto(existing) : throw new NotFoundException("Sinov topilmadi.");
        }

        var run = request switch
        {
            { RetryOf: { } retryOf } => await RetryMistakesAsync(ownerId, retryOf, ct),
            { Oral: true } => await BuildOralAsync(ownerId, request, ct), // checked for Premium inside
            { Usage: true } => await BuildUsageAsync(ownerId, request, ct),
            _ => await BuildAsync(ownerId, request, ct),
        };
        run.Id = request.Id;
        run.OwnerId = ownerId;
        run.Status = TestRunStatus.InProgress;
        run.CreatedAt = time.GetUtcNow();
        db.TestRuns.Add(run);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same request finished in parallel (double tap): return the saved one.
            db.DiscardChanges();
            return ToDto(await FindAsync(ownerId, request.Id, ct));
        }

        return ToDto(run);
    }

    public async Task<TestDto> AnswerAsync(Guid ownerId, Guid id, TestAnswerRequest request, CancellationToken ct)
    {
        var run = await FindAsync(ownerId, id, ct);
        if (run.Status != TestRunStatus.InProgress)
        {
            throw new ConflictException("Sinov yakunlangan, javobni o‘zgartirib bo‘lmaydi.");
        }

        var question = run.Questions.SingleOrDefault(q => q.Index == request.Index) ?? throw new ValidationException("Bunday savol yo‘q.");
        if (question.Kind == TestQuestionKind.OralKorean)
        {
            throw new ValidationException("Bu savolga ovoz bilan javob bering.");
        }

        var given = (request.Answer ?? "").Trim();
        if (given.Length > 200)
        {
            throw new ValidationException("Javob juda uzun.");
        }

        if (question.Options.Count > 0 && !TestGrader.IsTyped(question.Kind) && !question.Options.Contains(given))
        {
            throw new ValidationException("Variantlardan birini tanlang.");
        }

        // Graded now but shown only at the end (TEST03).
        var (ok, note) = TestGrader.Grade(question, given);
        run.Answers = run.Answers.Where(a => a.Index != request.Index)
            .Append(new TestAnswer(request.Index, given, ok, note, time.GetUtcNow()))
            .OrderBy(a => a.Index)
            .ToList();
        await SaveAsync(ct);
        return ToDto(run);
    }

    /// <summary>
    /// An oral answer (TEST02, D-38): the recording goes to speech recognition with the expected word as reference;
    /// what was heard is graded against it. Nothing is stored when no speech was heard, so the learner can say it again.
    /// The verdict stays hidden until the end (TEST03); the transcript is shown so the learner knows what was heard (AU03).
    /// </summary>
    public async Task<TestDto> OralAnswerAsync(Guid ownerId, Guid id, int index, byte[] wav, bool assisted, CancellationToken ct)
    {
        var (run, question) = await OralQuestionAsync(ownerId, id, index, ct);
        var heard = await speaking.AssessAsync(ownerId, wav, question.Correct, ct);
        if (!heard.Recognized || string.IsNullOrWhiteSpace(heard.RecognizedText))
        {
            throw new ValidationException(heard.Message ?? "Hech narsa eshitilmadi. Mikrofonga yaqinroq qilib qayta ayting.");
        }

        var text = heard.RecognizedText.Trim();
        var (ok, hard, note) = TestGrader.GradeOral(question, text);
        Store(run, new TestAnswer(index, text, ok, note, time.GetUtcNow(), assisted, hard, false, heard.PronScore ?? heard.Accuracy));
        await SaveAsync(ct);
        return ToDto(run);
    }

    /// <summary>The microphone or the speech service failed: recorded apart, never as a wrong answer (TEST03, SRS04).</summary>
    public async Task<TestDto> OralTechnicalAsync(Guid ownerId, Guid id, int index, CancellationToken ct)
    {
        var (run, _) = await OralQuestionAsync(ownerId, id, index, ct);
        Store(run, new TestAnswer(index, "", false, "Texnik xato: hisobga olinmadi", time.GetUtcNow(), Technical: true));
        await SaveAsync(ct);
        return ToDto(run);
    }

    private async Task<(TestRun Run, TestQuestion Question)> OralQuestionAsync(Guid ownerId, Guid id, int index, CancellationToken ct)
    {
        var run = await FindAsync(ownerId, id, ct);
        if (run.Status != TestRunStatus.InProgress)
        {
            throw new ConflictException("Sinov yakunlangan, javobni o‘zgartirib bo‘lmaydi.");
        }

        var question = run.Questions.SingleOrDefault(q => q.Index == index) ?? throw new ValidationException("Bunday savol yo‘q.");
        return question.Kind == TestQuestionKind.OralKorean
            ? (run, question)
            : throw new ValidationException("Bu savol ovoz bilan javob berilmaydi.");
    }

    private static void Store(TestRun run, TestAnswer answer) =>
        run.Answers = run.Answers.Where(a => a.Index != answer.Index).Append(answer).OrderBy(a => a.Index).ToList();

    /// <summary>Oral readiness lengths: a short check, or 0 = every word of the tables once.</summary>
    public static readonly int[] OralCounts = [0, 20];

    /// <summary>
    /// "Which word is right?" (D-46): questions prepared with each word's usage, one per word first (words shuffled),
    /// then second questions of the same words when more are wanted. No AI call here.
    /// </summary>
    private async Task<TestRun> BuildUsageAsync(Guid ownerId, CreateTestRequest request, CancellationToken ct)
    {
        var count = Counts.Contains(request.Count) ? request.Count : Counts[0];
        var reported = await QuestionReportService.ReportedSentencesAsync(db, ct);
        var all = (await usage.QuestionsAsync(ownerId, request.UnitIds?.ToList(), ct, request.Decks))
            .Where(x => !reported.Contains(x.Question.Sentence)) // reported as wrong, until the admin resolves it (L-37)
            .ToList();
        if (all.Count == 0)
        {
            throw new ValidationException("Bu bo‘limlar uchun qo‘llanish hali tayyorlanmagan. Mashq → So‘z qo‘llanishi sahifasida tayyorlang.");
        }

        var chosen = all.GroupBy(x => x.Word.Id)
            .OrderBy(_ => Random.Shared.Next())
            .SelectMany(g => g.Select((x, rank) => (x.Word, x.Question, Rank: rank)))
            .OrderBy(x => x.Rank)
            .Take(count)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        var questions = chosen.Select((x, i) => new TestQuestion(
                i,
                TestQuestionKind.WordUsage,
                "O‘zbekcha gapga mos so‘zni tanlang",
                x.Question.Sentence,
                x.Question.Uzbek,
                Shuffle([x.Question.Answer, .. x.Question.Wrong]),
                x.Question.Answer,
                null,
                x.Question.ExplanationUz,
                x.Word.Id,
                true))
            .ToList();
        var tables = await UnitTitleAsync(chosen.Select(x => x.Word), ct);
        return new TestRun
        {
            Title = $"Qo‘llanish · {tables}",
            Questions = questions,
        };
    }

    /// <summary>
    /// Oral readiness (TEST02, D-38): each word of the chosen tables once, half of them Uzbek → Korean said aloud and
    /// checked by speech recognition, half Korean → Uzbek said aloud and checked by the learner. No AI text is used.
    /// </summary>
    private async Task<TestRun> BuildOralAsync(Guid ownerId, CreateTestRequest request, CancellationToken ct)
    {
        await premium.RequireAsync(ownerId, ct); // spoken answers are checked by Azure pronunciation (spec §7.1)
        if (!OralCounts.Contains(request.Count))
        {
            throw new ValidationException("Og‘zaki sinov 20 ta savol yoki barcha so‘zlar bo‘lsin.");
        }

        var (words, own) = await ChosenWordsAsync(ownerId, request, ct);
        if (words.Count == 0)
        {
            throw new ValidationException("Tanlangan bo‘limlarda so‘z yo‘q.");
        }

        var chosen = Shuffle(words);
        if (request.Count > 0)
        {
            chosen = chosen.Take(request.Count).ToList();
        }

        // "Talaffuz" is speaking only (L-49): the Uzbek meaning is shown, the learner says the Korean word and
        // Azure grades it. The self-checked "say the meaning" question is no longer asked here.
        var questions = chosen
            .Select((w, i) => new TestQuestion(i, TestQuestionKind.OralKorean, w.Uzbek, null, null, [], w.Korean, w.KoreanNote, null, w.Id, false))
            .ToList();
        return new TestRun
        {
            Title = $"Talaffuz · {await UnitTitleAsync(words, ct, own)}",
            Questions = questions,
        };
    }

    /// <summary>
    /// Ends the test; answers can no longer change. A unit test's outcome (pass, next unit) is applied in the same
    /// transaction, once: finishing again, or twice at the same moment, returns the same result (spec §13.5).
    /// </summary>
    public async Task<TestDto> FinishAsync(Guid ownerId, Guid id, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync($"test-finish:{id}", ct);
        var run = await FindAsync(ownerId, id, ct);
        if (run.Status == TestRunStatus.Finished)
        {
            return await DtoAsync(run, ct);
        }

        await using var tx = await db.BeginTransactionAsync(ct);
        run.Status = TestRunStatus.Finished;
        run.FinishedAt = time.GetUtcNow();
        await SaveAsync(ct);
        await unitTests.ApplyAsync(run, ct);
        db.TestRuns.Update(run); // still tracked even if opening units had to re-read its own state
        await SaveAsync(ct);
        if (run.Questions.Any(q => q.Kind == TestQuestionKind.WordUsage))
        {
            // A finished "Qaysi so‘z to‘g‘ri?" round ticks the "Mashq" step of its words' units (L-42).
            var items = run.Questions.Select(q => q.LearningItemId).OfType<Guid>().Distinct().ToList();
            var unitIds = await db.LearningItems.Where(i => items.Contains(i.Id)).Select(i => i.UnitId).Distinct().ToListAsync(ct);
            await units.MarkPracticedAsync(ownerId, unitIds, writingRound: false, ct);
        }

        await tx.CommitAsync(ct);
        return await DtoAsync(run, ct);
    }

    public const int ShareWords = 3;

    /// <summary>
    /// The result with its share card (L-59): a passed final unit test gets the unit and up to <see cref="ShareWords"/>
    /// of its words answered right in this test, most frequent first. Not for "Bilaman", a failed test, or none right.
    /// </summary>
    private async Task<TestDto> DtoAsync(TestRun run, CancellationToken ct)
    {
        var dto = ToDto(run);
        if (dto.Unit is not { Purpose: TestPurpose.UnitFinal, Passed: true } unit)
        {
            return dto;
        }

        var right = run.Answers.Where(a => a.IsCorrect).Select(a => a.Index).ToHashSet();
        var items = run.Questions.Where(q => right.Contains(q.Index)).Select(q => q.LearningItemId).OfType<Guid>().Distinct().ToList();
        var words = await (from i in db.LearningItems
                           join w in db.CatalogWords on i.CatalogWordId equals w.Id
                           where items.Contains(i.Id)
                           orderby w.FreqRank ?? int.MaxValue, w.OrderInUnit
                           select new { i.Korean, w.Romanization, i.Uzbek })
            .Take(ShareWords).AsNoTracking().ToListAsync(ct);
        var shown = await db.Units.Where(u => u.Id == unit.UnitId).Select(u => new { u.Level, u.Number, u.WordCount }).SingleOrDefaultAsync(ct);
        if (words.Count == 0 || shown is null)
        {
            return dto;
        }

        var share = new UnitShareDto(shown.Level.ToString(), shown.Number, shown.WordCount,
            words.Select(w => new UnitShareWordDto(w.Korean, w.Romanization, w.Uzbek.Split(';')[0].Trim())).ToList());
        return dto with { Unit = unit with { Share = share } };
    }

    /// <summary>The recording of a listening question (spec §4.4): the word is heard, never sent as text before the end.</summary>
    public async Task<byte[]> AudioAsync(Guid ownerId, Guid id, int index, CancellationToken ct)
    {
        var run = await FindAsync(ownerId, id, ct);
        var question = run.Questions.SingleOrDefault(q => q.Index == index && TestGrader.IsListening(q.Kind))
            ?? throw new NotFoundException("Tinglash savoli topilmadi.");
        return await speech.GetAsync(ownerId, question.BaseForm, ct);
    }

    private async Task<TestRun> FindAsync(Guid ownerId, Guid id, CancellationToken ct) =>
        await db.TestRuns.SingleOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, ct) ?? throw new NotFoundException("Sinov topilmadi.");

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Sinov boshqa oynada o‘zgardi. Sahifani yangilang.");
        }
    }

    private async Task<TestRun> RetryMistakesAsync(Guid ownerId, Guid retryOf, CancellationToken ct)
    {
        var source = await FindAsync(ownerId, retryOf, ct);
        if (source.Status != TestRunStatus.Finished)
        {
            throw new ConflictException("Avval sinovni yakunlang.");
        }

        var wrong = source.Questions.Where(q => source.Answers.FirstOrDefault(a => a.Index == q.Index) is not { IsCorrect: true }).ToList();
        if (wrong.Count == 0)
        {
            throw new ValidationException("Bu sinovda xato yo‘q.");
        }

        return new TestRun
        {
            Title = $"{source.Title} · xatolar",
            Questions = Shuffle(wrong)
                .Select((q, i) => q with { Index = i, Options = q.Kind == TestQuestionKind.OralMeaning ? q.Options : Shuffle(q.Options) })
                .ToList(),
            RetryOfId = source.Id,
            Provider = source.Provider,
        };
    }

    private async Task<TestRun> BuildAsync(Guid ownerId, CreateTestRequest request, CancellationToken ct)
    {
        if (!Counts.Contains(request.Count))
        {
            throw new ValidationException("Savollar soni 10, 20 yoki 30 bo‘lsin.");
        }

        var (words, own) = await ChosenWordsAsync(ownerId, request, ct);
        if (words.Count < 4)
        {
            throw new ValidationException("Tanlangan bo‘limlarda kamida 4 ta so‘z bo‘lishi kerak.");
        }

        var plan = Plan(words, request.Count);
        var aiTasks = plan.Where(p => p.Kind is TestQuestionKind.WordCloze or TestQuestionKind.WordClozeTyped).ToList();
        var (gaps, provider, warning) = await generator.AskForGapsAsync(ownerId, aiTasks, words, ct);

        var questions = new List<TestQuestion>();
        foreach (var p in plan)
        {
            var index = questions.Count;
            var q = gaps.TryGetValue(p.Slot, out var gap)
                ? FromGap(index, p, gap)
                : Local(index, p with { Kind = LocalKind(p.Kind) }, words);
            if (q is not null)
            {
                questions.Add(q);
            }
        }

        if (questions.Count == 0)
        {
            throw new ValidationException("Savol tuzib bo‘lmadi: variantlar uchun kamida 4 ta so‘z kerak.");
        }

        var run = new TestRun { Title = await UnitTitleAsync(words, ct, own), Questions = questions, Provider = provider, Warning = warning };
        // Only the teacher's deck chosen: the test is that deck's, named after it (L-68).
        if ((request.UnitIds ?? []).Count == 0 && request.Decks is [var only] && Vocabulary.PersonalWordService.GroupDeckId(only) is { } deckId
            && await db.GroupDecks.AsNoTracking().Where(d => d.Id == deckId).Select(d => d.Name).FirstOrDefaultAsync(ct) is { } name)
        {
            (run.GroupDeckId, run.Title) = (deckId, name);
        }

        return run;
    }

    private static string Cut(string text, int max) => text.Length > max ? text[..max] : text;

    private static List<T> Shuffle<T>(List<T> items)
    {
        var copy = items.ToList();
        Random.Shared.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(copy));
        return copy;
    }

    public static TestDto ToDto(TestRun run)
    {
        var finished = run.Status == TestRunStatus.Finished;
        var answers = run.Answers.ToDictionary(a => a.Index);
        var questions = run.Questions.OrderBy(q => q.Index).Select(q =>
        {
            var a = answers.GetValueOrDefault(q.Index);
            var correct = finished ? a?.IsCorrect ?? false : (bool?)null;
            return new TestQuestionDto(
                q.Index,
                q.Kind,
                q.Prompt,
                q.Sentence,
                // The translation makes the gap's meaning clear: without it two options can both fit (있다 / 없다
                // in "저는 오늘 집에 ___", L-37). For an oral meaning question it is what the learner checks against.
                finished || q.Kind is TestQuestionKind.WordCloze or TestQuestionKind.WordClozeTyped or TestQuestionKind.OralMeaning or TestQuestionKind.WordUsage ? q.Translation : null,
                q.Options,
                q.AiMade,
                a?.Given,
                finished ? q.Correct : null,
                correct,
                finished ? a?.Note ?? (a is null ? "Javob berilmagan" : null) : null,
                finished ? q.Explanation : null,
                // Oral Korean: the first syllable, shown only when the learner asks for it (then marked "yordam bilan").
                q.Kind == TestQuestionKind.OralKorean ? q.Correct[..1] + "…" : null,
                a?.Assisted ?? false,
                finished && (a?.Hard ?? false),
                a?.Technical ?? false,
                finished ? a?.PronScore : null);
        }).ToList();
        var byKind = finished
            ? questions.GroupBy(q => q.Kind).Select(g => new TestKindScoreDto(g.Key, g.Count(q => q.IsCorrect == true), g.Count())).ToList()
            : [];
        var unit = run.UnitId is { } unitId && run.PassMark is { } mark
            ? new UnitTestInfoDto(run.Purpose, unitId, mark, finished ? run.Passed : null, finished ? run.OpenedUnitId : null,
                finished ? run.Questions.Where(q => !(answers.GetValueOrDefault(q.Index)?.IsCorrect ?? false)).Select(q => q.LearningItemId).OfType<Guid>().ToList() : [])
            : null;
        return new TestDto(run.Id, run.Title, run.Status, run.CreatedAt, run.FinishedAt, run.Warning, run.Provider, questions,
            run.Answers.Count, finished ? run.Answers.Count(a => a.IsCorrect) : null, byKind, unit);
    }

}
