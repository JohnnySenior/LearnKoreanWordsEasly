using static KoreanAiTeacher.Application.Testing.TestQuestionGenerator;
using KoreanAiTeacher.Application.Abstractions;
using KoreanAiTeacher.Application.Ai;
using KoreanAiTeacher.Contracts;
using KoreanAiTeacher.Domain.Grammar;
using KoreanAiTeacher.Domain.Testing;
using KoreanAiTeacher.Domain.Vocabulary;
using KoreanAiTeacher.Domain.Writing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KoreanAiTeacher.Application.Testing;

/// <summary>
/// Practice tests from the learner's own words and grammar (spec TEST01–TEST04, D-30). Choice questions are built
/// locally; gap sentences come from one AI call per test. When the AI is down the test still works, with choice
/// questions only. Tests never change the card schedule.
/// </summary>
public sealed class TestService(IAppDbContext db, TestQuestionGenerator generator, BookTestService books, Speech.SpeakingService speaking, Vocabulary.UsageService usage, TimeProvider time, IOperationLock operations)
{
    public const string PromptVersion = "test-gaps-v1";

    public static readonly int[] Counts = [10, 20, 30];

    public const string Blank = "___";


    public async Task<TestOptionsDto> OptionsAsync(Guid ownerId, CancellationToken ct)
    {
        var tables = await db.WordSenses
            .Where(s => s.OwnerId == ownerId && s.IsActive)
            .GroupBy(s => s.TableNumber)
            .OrderBy(g => g.Key)
            .Select(g => new TestTableOptionDto(g.Key, g.Count()))
            .ToListAsync(ct);
        var sets = await db.GrammarSets
            .Where(s => s.OwnerId == ownerId)
            .OrderBy(s => s.Number)
            .Select(s => new TestGrammarSetOptionDto(s.Id, s.Number, s.Items.Count))
            .ToListAsync(ct);
        var grammar = await db.GrammarItems.CountAsync(i => i.OwnerId == ownerId && i.Status == GrammarItemStatus.Approved, ct);
        return new TestOptionsDto(tables, sets, grammar);
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
        if (kinds.Contains(TestQuestionKind.BookChoice))
        {
            return TestCategory.Book;
        }

        if (kinds.Overlaps([TestQuestionKind.OralKorean, TestQuestionKind.OralMeaning]))
        {
            return TestCategory.Oral;
        }

        var grammar = kinds.Overlaps([TestQuestionKind.GrammarChoice, TestQuestionKind.GrammarMeaning]);
        var words = kinds.Overlaps([TestQuestionKind.WordMeaning, TestQuestionKind.WordKorean, TestQuestionKind.WordCloze, TestQuestionKind.WordClozeTyped, TestQuestionKind.WordUsage]);
        return grammar && words ? TestCategory.Mixed : grammar ? TestCategory.Grammar : TestCategory.Vocabulary;
    }

    public async Task<TestDto> GetAsync(Guid ownerId, Guid id, CancellationToken ct) => ToDto(await FindAsync(ownerId, id, ct));

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
            { BookTestId: { } bookTestId } => await BuildFromBookAsync(ownerId, bookTestId, request, ct),
            { BookCategory: { } category } => await BuildFromCategoryAsync(ownerId, category, request, ct),
            { Oral: true } => await BuildOralAsync(ownerId, request, ct),
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

        if (run.TimeLimitMinutes is { } limit && time.GetUtcNow() > run.CreatedAt.AddMinutes(limit).AddSeconds(30))
        {
            throw new ConflictException("Vaqt tugadi. Sinovni yakunlang.");
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

        if (question.Options.Count > 0 && question.Kind != TestQuestionKind.WordClozeTyped && !question.Options.Contains(given))
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
    /// Lesson readiness (TEST02, D-38): each word of the chosen tables once, half of them Uzbek → Korean said aloud and
    /// checked by speech recognition, half Korean → Uzbek said aloud and checked by the learner. No AI text is used.
    /// </summary>
    /// <summary>
    /// "Which word is right?" (D-46): questions prepared with each word's usage, one per word first (words shuffled),
    /// then second questions of the same words when more are wanted. No AI call here.
    /// </summary>
    private async Task<TestRun> BuildUsageAsync(Guid ownerId, CreateTestRequest request, CancellationToken ct)
    {
        var count = Counts.Contains(request.Count) ? request.Count : Counts[0];
        var all = await usage.QuestionsAsync(ownerId, request.TableNumbers?.ToList(), ct);
        if (all.Count == 0)
        {
            throw new ValidationException("Bu jadvallar uchun qo‘llanish hali tayyorlanmagan. Mashq → So‘z qo‘llanishi sahifasida tayyorlang.");
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
                null,
                true))
            .ToList();
        var tables = chosen.Select(x => x.Word.TableNumber).Distinct().Order().ToList();
        return new TestRun
        {
            Title = $"Qo‘llanish · {(tables.Count <= 3 ? string.Join(", ", tables) + "-jadval" : $"{tables.Count} ta jadval")}",
            Questions = questions,
        };
    }

    private async Task<TestRun> BuildOralAsync(Guid ownerId, CreateTestRequest request, CancellationToken ct)
    {
        if (!OralCounts.Contains(request.Count))
        {
            throw new ValidationException("Og‘zaki sinov 20 ta savol yoki barcha so‘zlar bo‘lsin.");
        }

        var tables = (request.TableNumbers ?? []).Distinct().Order().ToList();
        if (tables.Count == 0)
        {
            throw new ValidationException("Og‘zaki sinov uchun jadval tanlang.");
        }

        var words = await db.WordSenses.Where(s => s.OwnerId == ownerId && s.IsActive && tables.Contains(s.TableNumber)).ToListAsync(ct);
        if (words.Count == 0)
        {
            throw new ValidationException("Tanlangan jadvallarda tasdiqlangan so‘z yo‘q.");
        }

        var chosen = Shuffle(words);
        if (request.Count > 0)
        {
            chosen = chosen.Take(request.Count).ToList();
        }

        // Directions alternate over the shuffled words, so both are covered about equally (TEST02).
        var questions = chosen.Select((w, i) => i % 2 == 0
                ? new TestQuestion(i, TestQuestionKind.OralKorean, w.Uzbek, null, null, [], w.Korean, w.KoreanNote, null, w.Id, null, false)
                : new TestQuestion(i, TestQuestionKind.OralMeaning, w.Korean, null, w.Uzbek, [TestGrader.Knew, TestGrader.DidNotKnow], TestGrader.Knew, null, null, w.Id, null, false))
            .ToList();
        questions = Shuffle(questions).Select((q, i) => q with { Index = i }).ToList();
        return new TestRun
        {
            Title = $"Og‘zaki tayyorlik · {string.Join(", ", tables)}-jadval",
            Questions = questions,
        };
    }

    public async Task<TestDto> FinishAsync(Guid ownerId, Guid id, CancellationToken ct)
    {
        var run = await FindAsync(ownerId, id, ct);
        if (run.Status == TestRunStatus.Finished)
        {
            return ToDto(run);
        }

        run.Status = TestRunStatus.Finished;
        run.FinishedAt = time.GetUtcNow();
        await SaveAsync(ct);
        return ToDto(run);
    }

    /// <summary>How many recent finished book runs the history looks at.</summary>
    public const int HistoryRuns = 100;

    /// <summary>
    /// Book test history (D-35): each finished run with book questions on a 100-point scale, and accuracy by question
    /// type over all of them. Unanswered questions count as wrong, as in the exam.
    /// </summary>
    public async Task<BookHistoryDto> BookHistoryAsync(Guid ownerId, CancellationToken ct)
    {
        var runs = (await db.TestRuns.Where(r => r.OwnerId == ownerId && r.Status == TestRunStatus.Finished)
                .OrderByDescending(r => r.FinishedAt).Take(HistoryRuns * 2).AsNoTracking().ToListAsync(ct))
            .Where(r => r.Questions.Any(q => q.Kind == TestQuestionKind.BookChoice))
            .Take(HistoryRuns)
            .ToList();

        var readyTests = await db.BookTests.Where(t => t.OwnerId == ownerId && t.Status == BookTestStatus.Ready).Select(t => t.Id).ToListAsync(ct);
        var questionIds = runs.SelectMany(r => r.Questions).Where(q => q.Book is not null).Select(q => q.Book!.BookQuestionId).Distinct().ToList();
        var testIds = (await db.BookQuestions.Where(q => questionIds.Contains(q.Id)).Select(q => q.BookTestId).Distinct().ToListAsync(ct))
            .Union(readyTests).ToList();
        var categories = await books.CategoriesAsync(ownerId, testIds, ct);
        var available = await db.BookQuestions
            .Where(q => q.OwnerId == ownerId && readyTests.Contains(q.BookTestId) && !q.Excluded)
            .Select(q => q.Id)
            .ToListAsync(ct);

        var tally = new Dictionary<BookCategory, (int Correct, int Total)>();
        var answered = 0;
        foreach (var run in runs)
        {
            foreach (var q in run.Questions.Where(q => q.Book is not null))
            {
                // A question deleted with its book test keeps no type; it still counts in the run's score.
                if (!categories.TryGetValue(q.Book!.BookQuestionId, out var category))
                {
                    continue;
                }

                var ok = run.Answers.Any(a => a.Index == q.Index && a.IsCorrect);
                var (c, t) = tally.GetValueOrDefault(category);
                tally[category] = (c + (ok ? 1 : 0), t + 1);
                answered++;
            }
        }

        var availableByCategory = available.Where(categories.ContainsKey).GroupBy(id => categories[id]).ToDictionary(g => g.Key, g => g.Count());
        var byCategory = tally
            .Select(x => new BookCategoryScoreDto(x.Key, BookCategories.Label(x.Key), x.Value.Correct, x.Value.Total,
                x.Value.Correct * 100 / x.Value.Total, availableByCategory.GetValueOrDefault(x.Key)))
            .OrderBy(x => x.Percent)
            .ThenByDescending(x => x.Total)
            .ToList();

        var list = runs.Select(r =>
        {
            var book = r.Questions.Where(q => q.Kind == TestQuestionKind.BookChoice).ToList();
            var correct = book.Count(q => r.Answers.Any(a => a.Index == q.Index && a.IsCorrect));
            int? minutes = r.TimeLimitMinutes is { } limit && r.FinishedAt is { } done
                ? Math.Min(limit, (int)Math.Ceiling((done - r.CreatedAt).TotalMinutes))
                : null;
            return new BookRunDto(r.Id, r.Title, r.FinishedAt!.Value, correct, book.Count, correct * 100 / Math.Max(1, book.Count),
                r.TimeLimitMinutes is not null, minutes, r.TimeLimitMinutes);
        }).ToList();

        return new BookHistoryDto(list, byCategory, answered);
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

    /// <summary>
    /// A test from the book's questions (GR06, D-33): all of them or a random subset kept in book order. Exam mode sets
    /// the TOPIK II reading pace (70 minutes for 50 questions).
    /// </summary>
    private async Task<TestRun> BuildFromBookAsync(Guid ownerId, Guid bookTestId, CreateTestRequest request, CancellationToken ct)
    {
        var (test, all) = await books.UsableAsync(ownerId, bookTestId, ct);
        if (all.Count == 0)
        {
            throw new ValidationException("Bu kitob testida testga yaroqli savol yo‘q.");
        }

        if (request.Count < 0 || request.Count > 100)
        {
            throw new ValidationException("Savollar soni 0 (hammasi) dan 100 gacha bo‘lsin.");
        }

        var chosen = request.Count == 0 || request.Count >= all.Count
            ? all
            : Shuffle(all).Take(request.Count).OrderBy(q => q.Number).ToList();
        chosen = chosen.Where(q => q.Options.Count == 4 && q.Answer is >= 1 and <= 4).ToList();
        if (chosen.Count == 0)
        {
            throw new ValidationException("Savollarning javobi topilmadi.");
        }

        var questions = chosen.Select(ToTestQuestion).ToList();
        return new TestRun
        {
            Title = chosen.Count == all.Count ? test.Title : $"{test.Title} · {chosen.Count} ta savol",
            Questions = questions,
            BookTestId = test.Id,
            TimeLimitMinutes = request.Exam ? (int)Math.Ceiling(chosen.Count * BookTestService.SecondsPerQuestion / 60) : null,
            Warning = questions.All(q => q.Book!.OfficialKey)
                ? null
                : "Ba’zi savollarning javobi kitob kalitidan emas, AI yechimidan olingan. Natijada ular alohida belgilanadi.",
        };
    }

    private static TestQuestion ToTestQuestion(BookQuestion q, int index) => new(
        index,
        TestQuestionKind.BookChoice,
        q.Stem,
        q.Passage,
        null,
        q.Options,
        q.Options[q.Answer - 1],
        null,
        q.ExplanationUz,
        null,
        null,
        false,
        new BookQuestionInfo(q.Number, q.PdfPage, q.Instruction, q.Figure, q.HintUz, q.OfficialAnswer is not null, q.Id));

    /// <summary>
    /// Practice on one weak question type (D-35): random questions of that type from all ready book tests, no timer.
    /// Questions answered wrongly before come first, so the practice goes where points were lost.
    /// </summary>
    private async Task<TestRun> BuildFromCategoryAsync(Guid ownerId, BookCategory category, CreateTestRequest request, CancellationToken ct)
    {
        if (request.Count is < 1 or > 30)
        {
            throw new ValidationException("Savollar soni 1 dan 30 gacha bo‘lsin.");
        }

        var all = (await books.UsableOfCategoryAsync(ownerId, category, ct))
            .Where(q => q.Options.Count == 4 && q.Answer is >= 1 and <= 4)
            .ToList();
        if (all.Count == 0)
        {
            throw new ValidationException("Bu turdagi savol kitob testlarida topilmadi.");
        }

        var missed = await MissedBookQuestionsAsync(ownerId, ct);
        var chosen = Shuffle(all.Where(q => missed.Contains(q.Id)).ToList())
            .Concat(Shuffle(all.Where(q => !missed.Contains(q.Id)).ToList()))
            .Take(request.Count)
            .ToList();
        var questions = Shuffle(chosen).Select(ToTestQuestion).ToList();
        return new TestRun
        {
            Title = $"Kitob · {BookCategories.Label(category)}",
            Questions = questions,
            Warning = questions.All(q => q.Book!.OfficialKey)
                ? null
                : "Ba’zi savollarning javobi kitob kalitidan emas, AI yechimidan olingan. Natijada ular alohida belgilanadi.",
        };
    }

    /// <summary>Book questions whose latest answer in a finished run was wrong or missing.</summary>
    private async Task<HashSet<Guid>> MissedBookQuestionsAsync(Guid ownerId, CancellationToken ct)
    {
        var runs = await db.TestRuns.Where(r => r.OwnerId == ownerId && r.Status == TestRunStatus.Finished)
            .OrderByDescending(r => r.FinishedAt).Take(200).AsNoTracking().ToListAsync(ct);
        var latest = new Dictionary<Guid, bool>();
        foreach (var run in runs)
        {
            foreach (var q in run.Questions.Where(q => q.Book is not null))
            {
                latest.TryAdd(q.Book!.BookQuestionId, run.Answers.Any(a => a.Index == q.Index && a.IsCorrect));
            }
        }

        return latest.Where(x => !x.Value).Select(x => x.Key).ToHashSet();
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
            // Book questions keep the book's order and ①–④ numbering: hints and explanations refer to them.
            Questions = (source.BookTestId is null ? Shuffle(wrong) : wrong)
                .Select((q, i) => q with { Index = i, Options = q.Kind is TestQuestionKind.BookChoice or TestQuestionKind.OralMeaning ? q.Options : Shuffle(q.Options) })
                .ToList(),
            RetryOfId = source.Id,
            BookTestId = source.BookTestId,
            Provider = source.Provider,
        };
    }

    private async Task<TestRun> BuildAsync(Guid ownerId, CreateTestRequest request, CancellationToken ct)
    {
        if (!Counts.Contains(request.Count))
        {
            throw new ValidationException("Savollar soni 10, 20 yoki 30 bo‘lsin.");
        }

        var tables = (request.TableNumbers ?? []).Distinct().Order().ToList();
        var words = tables.Count == 0
            ? []
            : await db.WordSenses.Where(s => s.OwnerId == ownerId && s.IsActive && tables.Contains(s.TableNumber)).ToListAsync(ct);
        if (tables.Count > 0 && words.Count < 4)
        {
            throw new ValidationException("Tanlangan jadvallarda kamida 4 ta tasdiqlangan so‘z bo‘lishi kerak.");
        }

        List<GrammarItem> grammar = [];
        string? setTitle = null;
        if (request.GrammarSetId is { } setId)
        {
            var set = await db.GrammarSets.Include(s => s.Items).SingleOrDefaultAsync(s => s.Id == setId && s.OwnerId == ownerId, ct)
                ?? throw new NotFoundException("To‘plam topilmadi.");
            var ids = set.Items.Select(i => i.GrammarItemId).ToList();
            grammar = await db.GrammarItems.Where(i => ids.Contains(i.Id) && i.Status == GrammarItemStatus.Approved).ToListAsync(ct);
            setTitle = $"{set.Number}-to‘plam";
        }
        else if (request.AllGrammar)
        {
            grammar = await db.GrammarItems.Where(i => i.OwnerId == ownerId && i.Status == GrammarItemStatus.Approved).ToListAsync(ct);
            setTitle = "grammatika";
        }

        if (words.Count == 0 && grammar.Count == 0)
        {
            throw new ValidationException("Sinov uchun jadval yoki grammatika tanlang.");
        }

        // Distractors for grammar meanings come from all approved points, so a small set still has four options.
        var grammarPool = grammar.Count >= 4
            ? grammar
            : await db.GrammarItems.Where(i => i.OwnerId == ownerId && i.Status == GrammarItemStatus.Approved).ToListAsync(ct);

        var plan = Plan(words, grammar, request.Count);
        var aiTasks = plan.Where(p => p.Kind is TestQuestionKind.WordCloze or TestQuestionKind.WordClozeTyped or TestQuestionKind.GrammarChoice).ToList();
        var (gaps, provider, warning) = await generator.AskForGapsAsync(ownerId, aiTasks, words, grammar, ct);

        var questions = new List<TestQuestion>();
        foreach (var p in plan)
        {
            var index = questions.Count;
            var q = gaps.TryGetValue(p.Slot, out var gap)
                ? FromGap(index, p, gap)
                : Local(index, p with { Kind = LocalKind(p.Kind) }, words, grammarPool);
            if (q is not null)
            {
                questions.Add(q);
            }
        }

        if (questions.Count == 0)
        {
            throw new ValidationException("Savol tuzib bo‘lmadi: variantlar uchun kamida 4 ta so‘z yoki grammatika kerak.");
        }

        var parts = new List<string>();
        if (tables.Count > 0)
        {
            parts.Add($"{string.Join(", ", tables)}-jadval");
        }

        if (setTitle is not null)
        {
            parts.Add(setTitle);
        }

        return new TestRun { Title = string.Join(" · ", parts), Questions = questions, Provider = provider, Warning = warning };
    }

    private static string Cut(string text, int max) => text.Length > max ? text[..max] : text;

    private static List<T> Shuffle<T>(List<T> items)
    {
        var copy = items.ToList();
        Random.Shared.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(copy));
        return copy;
    }

    private static TestDto ToDto(TestRun run)
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
                // The translation helps to find a typed word; for choice gaps it would give the answer away. For an
                // oral meaning question it is what the learner checks themselves against after saying it.
                finished || q.Kind is TestQuestionKind.WordClozeTyped or TestQuestionKind.OralMeaning or TestQuestionKind.WordUsage ? q.Translation : null,
                q.Options,
                q.AiMade,
                a?.Given,
                finished ? q.Correct : null,
                correct,
                finished ? a?.Note ?? (a is null ? "Javob berilmagan" : null) : null,
                finished ? q.Explanation : null,
                finished ? q.GrammarItemId : null,
                q.Book?.Number,
                q.Book?.Page,
                q.Book?.Instruction,
                q.Book?.Figure,
                // Oral Korean: the first syllable, shown only when the learner asks for it (then marked "yordam bilan").
                q.Kind == TestQuestionKind.OralKorean ? q.Correct[..1] + "…" : finished ? q.Book?.Hint : null,
                finished ? q.Book?.OfficialKey : null,
                a?.Assisted ?? false,
                finished && (a?.Hard ?? false),
                a?.Technical ?? false,
                finished ? a?.PronScore : null);
        }).ToList();
        var byKind = finished
            ? questions.GroupBy(q => q.Kind).Select(g => new TestKindScoreDto(g.Key, g.Count(q => q.IsCorrect == true), g.Count())).ToList()
            : [];
        return new TestDto(run.Id, run.Title, run.Status, run.CreatedAt, run.FinishedAt, run.Warning, run.Provider, questions,
            run.Answers.Count, finished ? run.Answers.Count(a => a.IsCorrect) : null, byKind, run.TimeLimitMinutes, run.BookTestId);
    }

}
