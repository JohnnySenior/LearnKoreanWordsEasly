namespace Lugat.Domain.Listening;

/// <param name="Male">The man speaks (남자); otherwise the woman (여자).</param>
public sealed record ListeningLine(bool Male, string Text);

public sealed record ListeningWord(string Korean, string Uzbek);

/// <summary>One TOPIK II listening question (D-53): the passage is heard, never shown before answering.</summary>
/// <param name="QuestionKo">The instruction as on the exam paper, e.g. "들은 내용과 같은 것을 고르십시오."</param>
/// <param name="Answer">Index of the right option (0–3).</param>
/// <param name="TranslationUz">The passage in Uzbek, shown after answering.</param>
/// <param name="Words">Useful words from the passage with their Uzbek meaning.</param>
public sealed record ListeningItem(
    string QuestionKo,
    List<ListeningLine> Lines,
    List<string> Options,
    int Answer,
    string TranslationUz,
    string ExplanationUz,
    List<ListeningWord> Words);

public sealed record ListeningAnswer(int Index, int Choice, bool IsCorrect, DateTimeOffset AnsweredAt);

/// <summary>A set of AI-written TOPIK II style listening questions (D-53), voiced by the speech service.</summary>
public sealed class ListeningSet
{
    /// <summary>Chosen by the client, so a double tap never makes two sets (API01).</summary>
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string Title { get; set; } = "";

    public List<ListeningItem> Items { get; set; } = [];

    public List<ListeningAnswer> Answers { get; set; } = [];

    public string? Provider { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public uint Version { get; set; }
}
