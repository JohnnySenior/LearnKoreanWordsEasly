using Lugat.Domain.Testing;

namespace Lugat.Contracts;

/// <param name="Unit">"A-3".</param>
public sealed record TestTableOptionDto(Guid UnitId, string Unit, int Words);

/// <param name="Key">"umumiy" for own words in no deck, else the deck id (L-51).</param>
public sealed record WordDeckOptionDto(string Key, string Name, int Words);

/// <summary>What a test can be built from (GET /api/tests/options).</summary>
/// <param name="Decks">The learner's own words ("Mening so‘zlarim"), by deck.</param>
public sealed record TestOptionsDto(IReadOnlyList<TestTableOptionDto> Tables, IReadOnlyList<WordDeckOptionDto>? Decks = null);

/// <param name="Id">Chosen by the client once and reused on retries (API01).</param>
/// <param name="RetryOf">Build the test from the mistakes of this finished test (no other fields needed).</param>
/// <param name="Decks">Own words to include: <see cref="WordDeckOptionDto.Key"/> values (L-51).</param>
public sealed record CreateTestRequest(
    Guid Id,
    IReadOnlyList<Guid>? UnitIds,
    int Count,
    Guid? RetryOf = null,
    bool Oral = false,
    bool Usage = false,
    IReadOnlyList<string>? Decks = null);

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
    string? Hint = null,
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
    UnitTestInfoDto? Unit = null);

/// <param name="PassMark">Right answers needed (spec §4.4: 20 of 25).</param>
/// <param name="Passed">Only once the test is finished.</param>
/// <param name="NextUnitId">The unit the pass opened.</param>
/// <param name="WrongItemIds">Words answered wrong, for "Xato qilgan N ta so‘zni takrorlash" (after the end only).</param>
public sealed record UnitTestInfoDto(
    TestPurpose Purpose,
    Guid UnitId,
    int PassMark,
    bool? Passed,
    Guid? NextUnitId,
    IReadOnlyList<Guid> WrongItemIds);

public enum UnitTestKind
{
    Final,
    Skip,
}

/// <param name="Id">Chosen by the client once and reused on retries (API01).</param>
public sealed record StartUnitTestRequest(Guid Id, UnitTestKind Kind);

/// <summary>What a test was made of, for the history filter (D-39).</summary>
public enum TestCategory
{
    Vocabulary,
    Oral,
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

/// <summary>"Bu savolda xato bor" (L-37); the comment is optional.</summary>
public sealed record QuestionReportRequest(string? Comment);

public sealed record QuestionReportDto(
    Guid Id,
    TestQuestionKind Kind,
    string Prompt,
    string? Sentence,
    string? Translation,
    IReadOnlyList<string> Options,
    string Correct,
    string? Given,
    string? Comment,
    string Reporter,
    DateTimeOffset CreatedAt);
