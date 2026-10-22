namespace Lugat.Domain.Essays;

/// <summary>The four writing questions of TOPIK II (쓰기), D-49.</summary>
public enum EssayTask
{
    /// <summary>A practical text (message, notice) with two gaps ㉠ ㉡. 10 points.</summary>
    Q51 = 51,

    /// <summary>An explanatory paragraph with two gaps ㉠ ㉡. 10 points.</summary>
    Q52 = 52,

    /// <summary>Describe survey or statistics data in 200–300 characters. 30 points.</summary>
    Q53 = 53,

    /// <summary>An argumentative essay of 600–700 characters on a topic with guiding questions. 50 points.</summary>
    Q54 = 54,
}

public enum EssaySource
{
    /// <summary>Written by the AI in TOPIK style; never shown as an official question (TEST03).</summary>
    Ai,

    /// <summary>Pasted by the learner (from the teacher or a book).</summary>
    Own,
}

public enum EssayStatus
{
    Writing,
    Checked,
}

/// <param name="InstructionKo">The question's instruction in Korean, as on the paper.</param>
/// <param name="Text">51–52: the text with ㉠ and ㉡; 53: the data; 54: the topic and guiding questions.</param>
/// <param name="ModelAnswers">The AI's own answers (51–52: one per gap; 53–54: one text), shown after checking.</param>
public sealed record EssayPrompt(string InstructionKo, string Text, List<string> ModelAnswers);

public sealed record EssayCriterion(string Name, int Score, int Max, string CommentUz);

public sealed record EssayCorrection(string Original, string Corrected, string ExplanationUz);

/// <param name="Improved">The learner's text rewritten well, keeping their ideas.</param>
public sealed record EssayFeedback(
    int Score,
    int Max,
    List<EssayCriterion> Criteria,
    List<EssayCorrection> Corrections,
    string SummaryUz,
    string? Improved);

/// <summary>One TOPIK II writing attempt (D-49): the question, the learner's answer and the AI's marking.</summary>
public sealed class EssayAttempt
{
    /// <summary>Chosen by the client, so a double tap never makes two attempts (API01).</summary>
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public EssayTask Task { get; set; }

    public EssaySource Source { get; set; }

    public EssayPrompt Prompt { get; set; } = new("", "", []);

    /// <summary>51–52: the two gap answers; 53–54: one text.</summary>
    public List<string> Answers { get; set; } = [];

    public EssayStatus Status { get; set; }

    public EssayFeedback? Feedback { get; set; }

    public string? Provider { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CheckedAt { get; set; }

    public uint Version { get; set; }

    public static int MaxScore(EssayTask task) => task switch
    {
        EssayTask.Q53 => 30,
        EssayTask.Q54 => 50,
        _ => 10,
    };

    /// <summary>Length the paper asks for, counted like 원고지 cells (spaces included); null for the gap questions.</summary>
    public static (int Min, int Max)? Length(EssayTask task) => task switch
    {
        EssayTask.Q53 => (200, 300),
        EssayTask.Q54 => (600, 700),
        _ => null,
    };

    public static bool HasGaps(EssayTask task) => task is EssayTask.Q51 or EssayTask.Q52;
}
