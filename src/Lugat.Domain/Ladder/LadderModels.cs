namespace Lugat.Domain.Ladder;

/// <summary>The three rungs of the sentence ladder (D-47), from recognising to producing.</summary>
public enum LadderStep
{
    /// <summary>Choose the particle or verb ending that fills the gap.</summary>
    Fill,

    /// <summary>Put the sentence's word groups (어절) in order.</summary>
    Order,

    /// <summary>Write the Korean of a new Uzbek sentence built the same way; the AI checks it.</summary>
    Translate,
}

/// <param name="SentenceWithGap">The Korean sentence with the particle or ending replaced by "___".</param>
/// <param name="Answer">What fills the gap.</param>
/// <param name="Wrong">Look-alike particles or endings (을/를 → 이/가, -아서 → -고).</param>
/// <param name="Kind">"qo‘shimcha" or "fe’l shakli", shown with the question.</param>
public sealed record LadderFocus(string SentenceWithGap, string Answer, List<string> Wrong, string Kind, string ExplanationUz);

/// <summary>
/// One sentence of the ladder: filled and ordered as it is, then a twin sentence built the same way but with other
/// words is translated, so the learner builds it rather than recalls it.
/// </summary>
public sealed record LadderItem(
    int Index,
    string Korean,
    string Uzbek,
    LadderFocus Focus,
    string TwinUzbek,
    string TwinKorean,
    Guid? LearningItemId);

/// <param name="ErrorTypes">For a translation: what went wrong (qo‘shimcha, fe’l shakli, so‘z tanlash, so‘z tartibi, imlo).</param>
/// <param name="Corrected">The learner's sentence corrected, or the right order / form.</param>
public sealed record LadderAnswer(
    int Index,
    LadderStep Step,
    string Given,
    bool IsCorrect,
    List<string> ErrorTypes,
    string? Corrected,
    string? ExplanationUz,
    DateTimeOffset AnsweredAt);

/// <summary>A sentence ladder (D-47): a few sentences from the learner's words, three rungs each.</summary>
public sealed class SentenceLadder
{
    /// <summary>Chosen by the client, so a double tap never makes two ladders (API01).</summary>
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string Title { get; set; } = "";

    public List<LadderItem> Items { get; set; } = [];

    public List<LadderAnswer> Answers { get; set; } = [];

    public string Provider { get; set; } = "";

    public string? Model { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public uint Version { get; set; }

    /// <summary>Fill for every sentence, then order, then translate.</summary>
    public (LadderStep Step, int Index)? Next()
    {
        foreach (var step in Enum.GetValues<LadderStep>())
        {
            foreach (var item in Items)
            {
                if (!Answers.Any(a => a.Step == step && a.Index == item.Index))
                {
                    return (step, item.Index);
                }
            }
        }

        return null;
    }
}

/// <summary>
/// "Tuzoqlar daftari" (D-47): a word-for-word mistake the AI found in a translation ("biznes ochdim" → 사업을 열었어요
/// instead of 사업을 시작했어요). Kept until the learner marks it learned.
/// </summary>
public sealed class TrapNote
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid? LadderId { get; set; }

    /// <summary>The Uzbek the learner translated.</summary>
    public string Uzbek { get; set; } = "";

    /// <summary>What the learner wrote, e.g. 열었어요.</summary>
    public string Wrong { get; set; } = "";

    /// <summary>What a Korean says, e.g. 시작했어요.</summary>
    public string Right { get; set; } = "";

    public string ExplanationUz { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}
