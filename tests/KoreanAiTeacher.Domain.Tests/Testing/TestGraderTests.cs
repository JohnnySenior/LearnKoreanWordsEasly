using KoreanAiTeacher.Domain.Testing;

namespace KoreanAiTeacher.Domain.Tests.Testing;

public class TestGraderTests
{
    private static TestQuestion Q(TestQuestionKind kind, string correct, string? baseForm = null) =>
        new(0, kind, "p", "어제 학교에 ___.", null, ["갔어요", "먹었어요", "봤어요", "잤어요"], correct, baseForm, null, null, null, true);

    [Fact]
    public void Options_must_match_exactly()
    {
        Assert.True(TestGrader.Grade(Q(TestQuestionKind.WordCloze, "갔어요"), "갔어요").IsCorrect);
        Assert.False(TestGrader.Grade(Q(TestQuestionKind.WordCloze, "갔어요"), "먹었어요").IsCorrect);
        Assert.Equal((false, "Javob berilmagan"), TestGrader.Grade(Q(TestQuestionKind.WordMeaning, "maktab"), "  "));
    }

    [Fact]
    public void A_typed_gap_accepts_the_form_in_the_sentence_or_the_dictionary_form()
    {
        var q = Q(TestQuestionKind.WordClozeTyped, "갔어요", "가다");

        Assert.Equal((true, null), TestGrader.Grade(q, " 갔어요 "));
        Assert.Equal((true, "So‘z to‘g‘ri. Gapdagi shakli: 갔어요"), TestGrader.Grade(q, "가다"));
        Assert.Equal((false, "Imlo xatosi"), TestGrader.Grade(q, "갓어요"));
        Assert.Equal((false, null), TestGrader.Grade(q, "먹었어요"));
    }
}
