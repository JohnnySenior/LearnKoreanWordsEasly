using Lugat.Domain.Testing;

namespace Lugat.Contracts;

public sealed record TestTableOptionDto(int TableNumber, int Words);

public sealed record TestGrammarSetOptionDto(Guid Id, int Number, int Items);

/// <summary>What a test can be built from (GET /api/tests/options).</summary>
public sealed record TestOptionsDto(IReadOnlyList<TestTableOptionDto> Tables, IReadOnlyList<TestGrammarSetOptionDto> GrammarSets, int ApprovedGrammar);

/// <param name="Id">Chosen by the client once and reused on retries (API01).</param>
/// <param name="AllGrammar">All approved grammar points instead of one set.</param>
/// <param name="RetryOf">Build the test from the mistakes of this finished test (no other fields needed).</param>
public sealed record CreateTestRequest(
    Guid Id,
    IReadOnlyList<int>? TableNumbers,
    Guid? GrammarSetId,
    bool AllGrammar,
    int Count,
    Guid? RetryOf = null,
    Guid? BookTestId = null,
    bool Exam = false,
    BookCategory? BookCategory = null,
    bool Oral = false,
    bool Usage = false);

public sealed record TestAnswerRequest(int Index, string Answer);

/// <param name="Given">The learner's answer so far.</param>
/// <param name="Correct">Only after the test is finished (TEST03), like the fields after it.</param>
public sealed record TestQuestionDto(
    int Index,
    TestQuestionKind Kind,
    string Prompt,
    string? Sentence,
    string? Translation,
    IReadOnlyList<string> Options,
    bool AiMade,
    string? Given,
    string? Correct,
    bool? IsCorrect,
    string? Note,
    string? Explanation,
    Guid? GrammarItemId,
    int? Number = null,
    int? Page = null,
    string? Instruction = null,
    string? Figure = null,
    string? Hint = null,
    bool? OfficialKey = null,
    bool Assisted = false,
    bool Hard = false,
    bool Technical = false,
    double? PronScore = null);

public sealed record TestKindScoreDto(TestQuestionKind Kind, int Correct, int Total);

public sealed record TestDto(
    Guid Id,
    string Title,
    TestRunStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt,
    string? Warning,
    string? Provider,
    IReadOnlyList<TestQuestionDto> Questions,
    int Answered,
    int? Score,
    IReadOnlyList<TestKindScoreDto> ByKind,
    int? TimeLimitMinutes = null,
    Guid? BookTestId = null);

/// <summary>What a test was made of, for the history filter (D-39).</summary>
public enum TestCategory
{
    Vocabulary,
    Grammar,

    /// <summary>Words and grammar together (tests made before D-39).</summary>
    Mixed,
    Oral,
    Book,
}

/// <param name="Counted">Questions that count for the score: technical failures of oral answers are left out (D-38).</param>
public sealed record TestSummaryDto(
    Guid Id,
    string Title,
    TestRunStatus Status,
    DateTimeOffset CreatedAt,
    int Total,
    int Answered,
    int? Score,
    TestCategory Category = TestCategory.Vocabulary,
    int Counted = 0);

/// <param name="AnswerFromPage">Pages with the book's answer key (정답); optional.</param>
public sealed record CreateBookTestRequest(string Title, int FromPage, int ToPage, int? AnswerFromPage, int? AnswerToPage);

public sealed record BookQuestionDto(
    Guid Id,
    int Number,
    int PdfPage,
    string? Instruction,
    string? Passage,
    string Stem,
    IReadOnlyList<string> Options,
    string? Figure,
    int AiAnswer,
    int? OfficialAnswer,
    string? HintUz,
    string? ExplanationUz,
    bool Flagged,
    string? Warning,
    bool Excluded,
    uint Version);

/// <param name="Usable">Questions that go into tests (not excluded).</param>
/// <param name="WithKey">Questions whose answer comes from the book's key.</param>
public sealed record BookTestDto(
    Guid Id,
    Guid SourceDocumentId,
    string FileName,
    string Title,
    int FromPage,
    int ToPage,
    int? AnswerFromPage,
    int? AnswerToPage,
    int? DonePage,
    int? AnswerDonePage,
    BookTestStatus Status,
    string? Error,
    int Questions,
    int Usable,
    int WithKey,
    int Flagged,
    DateTimeOffset CreatedAt);

public sealed record UpdateBookQuestionRequest(bool Excluded, uint Version);

/// <param name="Score">On a 100-point scale.</param>
/// <param name="Minutes">Time taken, for exam-mode runs.</param>
public sealed record BookRunDto(Guid Id, string Title, DateTimeOffset FinishedAt, int Correct, int Total, int Score, bool Exam, int? Minutes, int? TimeLimitMinutes);

/// <param name="Available">Usable questions of this type in the learner's ready book tests (for practice).</param>
public sealed record BookCategoryScoreDto(BookCategory Category, string Label, int Correct, int Total, int Percent, int Available);

/// <summary>Book test history (D-35): scores over time and accuracy by TOPIK II reading question type, weakest first.</summary>
public sealed record BookHistoryDto(IReadOnlyList<BookRunDto> Runs, IReadOnlyList<BookCategoryScoreDto> Categories, int Answered);
