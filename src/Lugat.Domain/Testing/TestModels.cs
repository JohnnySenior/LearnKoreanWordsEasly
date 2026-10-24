using Lugat.Domain.Writing;

namespace Lugat.Domain.Testing;

/// <summary>Question types of a practice test (spec TEST01, D-30).</summary>
public enum TestQuestionKind
{
    /// <summary>Korean word → choose the Uzbek meaning.</summary>
    WordMeaning,

    /// <summary>Uzbek meaning → choose the Korean word.</summary>
    WordKorean,

    /// <summary>A sentence with a gap → choose the word that fits (TOPIK style, AI-made).</summary>
    WordCloze,

    /// <summary>A sentence with a gap and its translation → type the missing word (AI-made).</summary>
    WordClozeTyped,

    /// <summary>Oral readiness (TEST02, D-38): Uzbek meaning shown → the learner says the Korean word; speech recognition checks it.</summary>
    OralKorean,

    /// <summary>Oral readiness: Korean word shown and played → the learner says the meaning aloud, then checks themselves.</summary>
    OralMeaning,

    /// <summary>
    /// "Which word is right?" (D-46): an Uzbek sentence and its Korean with a gap; the options are the right word and
    /// the ones a word-for-word translation would give (데려왔어요 / 가져왔어요). Prepared once per word, no AI per test.
    /// </summary>
    WordUsage,

    /// <summary>
    /// Unit test (spec §4.4): the Korean word is heard, not shown, and chosen among Korean words. The text to say is
    /// kept in <see cref="TestQuestion.BaseForm"/>, which never reaches the client; the audio comes from the server.
    /// </summary>
    WordListening,

    /// <summary>Unit test (L-38): the Uzbek meaning is shown → the learner types the Korean word.</summary>
    WordKoreanTyped,

    /// <summary>Unit test (L-38): the word is only heard, as in <see cref="WordListening"/> → the learner types it.</summary>
    WordListeningTyped,
}

/// <summary>Why a test was taken. Unit tests open units; practice tests change nothing (TEST04).</summary>
public enum TestPurpose
{
    Practice,

    /// <summary>The unit's final test (spec §4.4): ≥ 80% passes the unit and opens the next.</summary>
    UnitFinal,

    /// <summary>"Bilaman — testni topshirish" (spec §4.5) for the next locked unit.</summary>
    UnitSkip,
}

public enum TestRunStatus
{
    InProgress,
    Finished,
}

/// <param name="Correct">The right option, or for a typed gap the word as it stands in the sentence.</param>
/// <param name="BaseForm">Typed gap: the dictionary form, also accepted (with a note on the form).</param>
/// <param name="AiMade">Written by the AI: shown as "TOPIK uslubidagi mashq", never as an official test (TEST03).</param>
public sealed record TestQuestion(
    int Index,
    TestQuestionKind Kind,
    string Prompt,
    string? Sentence,
    string? Translation,
    List<string> Options,
    string Correct,
    string? BaseForm,
    string? Explanation,
    Guid? LearningItemId,
    bool AiMade);

/// <param name="Note">Why a typed answer was or was not accepted ("imlo xatosi", "so‘z to‘g‘ri, shakli: …").</param>
/// <param name="Assisted">A hint (first syllable) was shown before answering (TEST03 "yordam bilan").</param>
/// <param name="Hard">Accepted, but only close: speech recognition heard a near form (TEST03 "qiyin").</param>
/// <param name="Technical">Microphone or speech service failed; not counted as a knowledge result (TEST03, SRS04).</param>
/// <param name="PronScore">Azure pronunciation score for an oral answer (AU04), when reported.</param>
public sealed record TestAnswer(
    int Index,
    string Given,
    bool IsCorrect,
    string? Note,
    DateTimeOffset AnsweredAt,
    bool Assisted = false,
    bool Hard = false,
    bool Technical = false,
    double? PronScore = null);

/// <summary>
/// One practice test (spec TEST01–TEST04). Answers stay hidden until the end. A test never changes the card schedule
/// (TEST04: choosing an option is not proof of recall).
/// </summary>
public sealed class TestRun
{
    /// <summary>Chosen by the client, so a double tap or a retry never makes two tests (API01).</summary>
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string Title { get; set; } = "";

    public TestRunStatus Status { get; set; }

    public List<TestQuestion> Questions { get; set; } = [];

    public List<TestAnswer> Answers { get; set; } = [];

    /// <summary>E.g. "AI ishlamadi, faqat variantli savollar tuzildi".</summary>
    public string? Warning { get; set; }

    public string? Provider { get; set; }

    public Guid? RetryOfId { get; set; }

    public TestPurpose Purpose { get; set; }

    /// <summary>The unit of a unit test.</summary>
    public Guid? UnitId { get; set; }

    /// <summary>Right answers needed to pass: ceil(questions × pass percent), fixed when the test is made.</summary>
    public int? PassMark { get; set; }

    /// <summary>Set once, when a unit test is finished and its outcome applied.</summary>
    public bool? Passed { get; set; }

    /// <summary>The unit the pass opened, if any (for "next unit" after the result).</summary>
    public Guid? OpenedUnitId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public uint Version { get; set; }
}

public static class TestGrader
{
    public const string Knew = "Bildim";

    public const string DidNotKnow = "Bilmadim";

    /// <summary>Below this many syllables one sound off is usually another word (가다 / 바다), so only an exact match counts.</summary>
    public const int OralNearMinSyllables = 3;

    /// <summary>
    /// An oral answer (D-38): what speech recognition heard against the expected Korean word. The same form → correct;
    /// for words of three or more syllables a near form (one or two sounds off, as recognition often hears) → correct
    /// but "qiyin", with a note to check the pronunciation; anything else → wrong.
    /// </summary>
    public static (bool IsCorrect, bool Hard, string? Note) GradeOral(TestQuestion q, string heard)
    {
        var wrong = (false, false, string.IsNullOrWhiteSpace(heard) ? "Hech narsa eshitilmadi" : $"Eshitildi: {heard}");
        return AnswerMatcher.MatchKorean(heard ?? "", q.Correct, q.BaseForm) switch
        {
            MatchResult.Exact => (true, false, null),
            MatchResult.Spelling when AnswerMatcher.NormalizeKorean(q.Correct).Length >= OralNearMinSyllables
                => (true, true, $"Yaqin eshitildi: {heard}. Talaffuzni tekshiring."),
            _ => wrong,
        };
    }

    /// <summary>Questions answered by typing the Korean word instead of choosing an option.</summary>
    public static bool IsTyped(TestQuestionKind kind) =>
        kind is TestQuestionKind.WordClozeTyped or TestQuestionKind.WordKoreanTyped or TestQuestionKind.WordListeningTyped;

    /// <summary>Questions where the word is only heard; its text stays on the server until the end.</summary>
    public static bool IsListening(TestQuestionKind kind) => kind is TestQuestionKind.WordListening or TestQuestionKind.WordListeningTyped;

    public static (bool IsCorrect, string? Note) Grade(TestQuestion q, string given)
    {
        var answer = (given ?? "").Trim();
        if (answer.Length == 0)
        {
            return (false, "Javob berilmagan");
        }

        if (!IsTyped(q.Kind))
        {
            return (answer == q.Correct, null);
        }

        return AnswerMatcher.MatchKorean(answer, q.Correct) switch
        {
            MatchResult.Exact => (true, null),
            _ when q.BaseForm is { } b && AnswerMatcher.MatchKorean(answer, b) == MatchResult.Exact
                => (true, $"So‘z to‘g‘ri. Gapdagi shakli: {q.Correct}"),
            MatchResult.Spelling => (false, "Imlo xatosi"),
            _ => (false, null),
        };
    }
}
