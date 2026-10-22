using Lugat.Domain.Testing;

namespace Lugat.Domain.Tests.Testing;

public class OralGradingTests
{
    private static TestQuestion Say(string korean, string? note = null) =>
        new(0, TestQuestionKind.OralKorean, "ma’no", null, null, [], korean, note, null, null, null, false);

    [Theory]
    [InlineData("도서관", "도서관.", true, false)] // recognition adds punctuation
    [InlineData("도서관", "도서곤", true, true)] // three syllables, one sound off: accepted but "qiyin"
    [InlineData("가다", "바다", false, false)] // two syllables, one sound off: another word
    [InlineData("가다", "가다", true, false)]
    [InlineData("학교", "", false, false)]
    public void What_was_heard_is_graded_strictly_for_short_words(string expected, string heard, bool correct, bool hard)
    {
        var (ok, isHard, note) = TestGrader.GradeOral(Say(expected), heard);

        Assert.Equal((correct, hard), (ok, isHard));
        if (!ok)
        {
            Assert.NotNull(note);
        }
    }

    [Fact]
    public void The_book_note_is_accepted_as_part_of_the_word()
    {
        Assert.True(TestGrader.GradeOral(Say("공부", "하다"), "공부하다").IsCorrect);
    }
}
