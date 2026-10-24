namespace Lugat.Domain.Testing;

/// <summary>
/// "Bu savolda xato bor" (L-37): a learner says a test question is wrong, e.g. two options both fit. The admin sees it
/// in the panel; until it is resolved, a stored "which word is right?" question with this sentence is not asked again.
/// </summary>
public sealed class QuestionReport
{
    public Guid Id { get; set; }

    public Guid ReporterId { get; set; }

    public Guid TestRunId { get; set; }

    public int QuestionIndex { get; set; }

    public TestQuestionKind Kind { get; set; }

    /// <summary>The question as the learner saw it, kept even when the test is gone.</summary>
    public string Prompt { get; set; } = "";

    public string? Sentence { get; set; }

    public string? Translation { get; set; }

    public List<string> Options { get; set; } = [];

    public string Correct { get; set; } = "";

    public string? Given { get; set; }

    public Guid? LearningItemId { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public Guid? ResolvedById { get; set; }
}
