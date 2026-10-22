using Lugat.Domain.Imports;

namespace Lugat.Domain.Grammar;

public enum GrammarImportStatus
{
    Queued,
    Extracting,

    /// <summary>All pages read; items wait for the learner's check (spec GR01, IMP03).</summary>
    NeedsReview,
    Failed,
}

/// <summary>Reading a page range of an uploaded grammar book with the AI (spec GR01, D-24).</summary>
public sealed class GrammarImport
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid SourceDocumentId { get; set; }

    public SourceDocument? SourceDocument { get; set; }

    /// <summary>PDF page numbers (1-based), inclusive.</summary>
    public int FromPage { get; set; }

    public int ToPage { get; set; }

    /// <summary>Last page already read; a restart continues after it.</summary>
    public int? DonePage { get; set; }

    public GrammarImportStatus Status { get; set; }

    public int Attempts { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}

public enum GrammarItemStatus
{
    NeedsReview,
    Approved,
    Excluded,
}

public sealed record GrammarExample(string Korean, string Uzbek);

/// <summary>
/// One grammar point as read from the book (spec GR01, GR02, §12 Grammar). The Korean form and label are what the
/// page shows; the Uzbek fields are the AI's simple explanation and must be checked before use.
/// </summary>
public sealed class GrammarItem
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid SourceDocumentId { get; set; }

    public Guid GrammarImportId { get; set; }

    public int PdfPage { get; set; }

    /// <summary>Order across the book: page × 1000 + position on the page.</summary>
    public int OrderInDocument { get; set; }

    /// <summary>Number printed in the book, if any.</summary>
    public int? Number { get; set; }

    /// <summary>E.g. 연결어미, 종결어미, 유사 문법.</summary>
    public string? Section { get; set; }

    /// <summary>The form as printed, e.g. -(으)ㄹ수록.</summary>
    public string Form { get; set; } = "";

    /// <summary>Other forms of the same point or, on "유사 문법" pages, grammar with the same meaning.</summary>
    public string? Similar { get; set; }

    /// <summary>The book's meaning/function label, in Korean as printed.</summary>
    public string? MeaningKo { get; set; }

    public string MeaningUz { get; set; } = "";

    public string? ConnectionUz { get; set; }

    public string? RestrictionUz { get; set; }

    public List<GrammarExample> Examples { get; set; } = [];

    public GrammarItemStatus Status { get; set; }

    /// <summary>The AI was unsure or its answer failed a check; the learner should look closely (IMP03).</summary>
    public bool Flagged { get; set; }

    public string? Warning { get; set; }

    /// <summary>The AI's fuller explanation and extra examples, made once when the learner opens the item (D-26).</summary>
    public GrammarGuide? Guide { get; set; }

    /// <summary>First time the learner opened the item page ("Ko‘rib chiqildi", spec GR03).</summary>
    public DateTimeOffset? SeenAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public uint Version { get; set; }
}

/// <summary>
/// Written by the AI, not copied from the book, so the page labels it as such (spec GR02, AI03). Stored with the item
/// so opening it again costs nothing.
/// </summary>
public sealed record GrammarGuide(
    string ExplanationUz,
    string? FormationUz,
    string? CommonMistakeUz,
    List<GrammarExample> Examples,
    string Provider,
    string? Model,
    string? Warning,
    DateTimeOffset CreatedAt);

/// <summary>How far the learner is with one grammar point (spec GR03). Derived from what they did, never set by hand.</summary>
public enum GrammarMastery
{
    New,

    /// <summary>"Ko‘rib chiqildi": the item page was opened.</summary>
    Seen,

    /// <summary>"Mashq qilinyapti": at least one sentence written.</summary>
    Practising,

    /// <summary>"Mustaqil qo‘llayapti": see <see cref="GrammarProgress"/>.</summary>
    Independent,
}

/// <summary>One checked sentence: when (learner's local day) and whether the grammar was used correctly.</summary>
public readonly record struct GrammarAttempt(DateOnly Day, bool Good);

/// <summary>
/// Rule for "Mustaqil qo‘llayapti" (technical baseline, D-27; may change like SRS06): correct use of the grammar on at
/// least 3 different days, and the latest sentence is correct. A new mistake moves it back to "Mashq qilinyapti".
/// </summary>
public static class GrammarProgress
{
    public const int RequiredGoodDays = 3;

    /// <param name="attempts">Oldest first.</param>
    public static GrammarMastery Of(bool seen, IReadOnlyList<GrammarAttempt> attempts)
    {
        if (attempts.Count == 0)
        {
            return seen ? GrammarMastery.Seen : GrammarMastery.New;
        }

        return GoodDays(attempts) >= RequiredGoodDays && attempts[^1].Good ? GrammarMastery.Independent : GrammarMastery.Practising;
    }

    public static int GoodDays(IReadOnlyList<GrammarAttempt> attempts) => attempts.Where(a => a.Good).Select(a => a.Day).Distinct().Count();
}

public enum GrammarQuestionKind
{
    /// <summary>"Soddaroq tushuntir".</summary>
    Simpler,

    /// <summary>"Yana misol".</summary>
    MoreExamples,

    /// <summary>"O‘xshashidan farqi".</summary>
    Difference,

    /// <summary>The learner's own question.</summary>
    Question,
}

/// <summary>
/// One question about a grammar point and the AI's answer (spec GR05). Kept with the item, so a useful explanation
/// can be read again later.
/// </summary>
public sealed class GrammarNote
{
    /// <summary>Chosen by the client and reused on retries, so a retry never pays twice (spec API01).</summary>
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid GrammarItemId { get; set; }

    public GrammarQuestionKind Kind { get; set; }

    public string? Question { get; set; }

    public string AnswerUz { get; set; } = "";

    public List<GrammarExample> Examples { get; set; } = [];

    public string Provider { get; set; } = "";

    public string? Model { get; set; }

    public string PromptVersion { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// "To‘plam": the grammar points the teacher gave to learn together, with an optional deadline (D-28). Not tied to a
/// lesson: the teacher's pace changes (90 points in a few days at the start of the book, a handful later).
/// </summary>
public sealed class GrammarSet
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>1, 2, 3 … per learner, for the title "1-to‘plam".</summary>
    public int Number { get; set; }

    /// <summary>The learner's local date by which the set should be learned.</summary>
    public DateOnly? DueDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<GrammarSetItem> Items { get; set; } = [];

    public uint Version { get; set; }
}

/// <summary>A grammar point belongs to at most one set.</summary>
public sealed class GrammarSetItem
{
    public Guid GrammarSetId { get; set; }

    public Guid GrammarItemId { get; set; }

    public Guid OwnerId { get; set; }

    public int Position { get; set; }
}

/// <summary>How much of a set to do today (D-28).</summary>
public static class GrammarPace
{
    /// <summary>Without a deadline, this many new points a day are suggested.</summary>
    public const int DefaultPerDay = 5;

    /// <param name="notStarted">Points without any sentence yet, counted at the start of today.</param>
    /// <returns>New points to start today; everything left when the deadline is today or has passed.</returns>
    public static int TodayTarget(int notStarted, DateOnly today, DateOnly? due)
    {
        if (notStarted <= 0)
        {
            return 0;
        }

        if (due is not { } d)
        {
            return Math.Min(notStarted, DefaultPerDay);
        }

        var daysLeft = d.DayNumber - today.DayNumber + 1; // today counts
        return daysLeft <= 1 ? notStarted : (notStarted + daysLeft - 1) / daysLeft;
    }
}
