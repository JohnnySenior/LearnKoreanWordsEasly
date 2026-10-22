using Lugat.Domain.Imports;

namespace Lugat.Domain.Testing;

public enum BookTestStatus
{
    Queued,
    Extracting,
    Ready,
    Failed,
}

/// <summary>
/// A practice test from the learner's TOPIK book (spec GR06, D-33): question pages and, optionally, the answer key
/// pages, read page by page by the AI. Book text stays in the learner's database only (D-12).
/// </summary>
public sealed class BookTest
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid SourceDocumentId { get; set; }

    public SourceDocument? SourceDocument { get; set; }

    /// <summary>The learner's name for it, e.g. "1-mock test · o‘qish".</summary>
    public string Title { get; set; } = "";

    public int FromPage { get; set; }

    public int ToPage { get; set; }

    public int? AnswerFromPage { get; set; }

    public int? AnswerToPage { get; set; }

    /// <summary>Last question page read.</summary>
    public int? DonePage { get; set; }

    /// <summary>Last answer-key page read.</summary>
    public int? AnswerDonePage { get; set; }

    public BookTestStatus Status { get; set; }

    public int Attempts { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public uint Version { get; set; }
}

/// <summary>One multiple-choice question as the AI read it from a book page.</summary>
public sealed class BookQuestion
{
    public Guid Id { get; set; }

    public Guid BookTestId { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>The number printed in the book.</summary>
    public int Number { get; set; }

    public int PdfPage { get; set; }

    /// <summary>The shared instruction, e.g. "[1~2] (    )에 들어갈 가장 알맞은 것을 고르십시오."</summary>
    public string? Instruction { get; set; }

    /// <summary>The text the question is about (copied for each question of a shared passage).</summary>
    public string? Passage { get; set; }

    public string Stem { get; set; } = "";

    public List<string> Options { get; set; } = [];

    /// <summary>A chart, advert or picture described in Uzbek, with its Korean text; the page number points to the original.</summary>
    public string? Figure { get; set; }

    /// <summary>1–4, the AI's own answer.</summary>
    public int AiAnswer { get; set; }

    /// <summary>1–4 from the book's answer key, when read.</summary>
    public int? OfficialAnswer { get; set; }

    public string? HintUz { get; set; }

    public string? ExplanationUz { get; set; }

    public bool Flagged { get; set; }

    public string? Warning { get; set; }

    /// <summary>Left out of tests (unreadable, a listening or writing task).</summary>
    public bool Excluded { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public uint Version { get; set; }

    public int Answer => OfficialAnswer ?? AiAnswer;
}
