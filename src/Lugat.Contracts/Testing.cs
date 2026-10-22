using Lugat.Domain.Testing;

namespace Lugat.Contracts;

public sealed record TestTableOptionDto(int TableNumber, int Words);

/// <summary>What a test can be built from (GET /api/tests/options).</summary>
public sealed record TestOptionsDto(IReadOnlyList<TestTableOptionDto> Tables);

/// <param name="Id">Chosen by the client once and reused on retries (API01).</param>
/// <param name="RetryOf">Build the test from the mistakes of this finished test (no other fields needed).</param>
public sealed record CreateTestRequest(
    Guid Id,
    IReadOnlyList<int>? TableNumbers,
    int Count,
    Guid? RetryOf = null,
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
    IReadOnlyList<TestKindScoreDto> ByKind);

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
