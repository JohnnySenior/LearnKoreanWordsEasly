using KoreanAiTeacher.Domain.Writing;

namespace KoreanAiTeacher.Domain.Tests.Writing;

public class AnswerMatcherTests
{
    [Theory]
    [InlineData("o‘jarlik", "o’jarlik")]
    [InlineData("O'jarlik", "o‘jarlik")]
    [InlineData("  o`jarlik ", "o‘jarlik")]
    [InlineData("ishqalamoq", "1. ishqalamoq; 2. surtmoq")]
    [InlineData("surtmoq", "1. ishqalamoq 2. surtmoq")]
    [InlineData("artmoq", "surtmoq, artmoq")]
    [InlineData("tutun", "tutun (havo)")]
    [InlineData("havo ifloslanishi!", "havo ifloslanishi")]
    public void Uzbek_style_differences_are_not_mistakes(string answer, string expected) =>
        Assert.Equal(MatchResult.Exact, AnswerMatcher.MatchUzbek(answer, expected));

    [Theory]
    [InlineData("ojarlik", "o‘jarlik")]
    [InlineData("ishqalamok", "ishqalamoq")]
    public void One_letter_slip_in_uzbek_is_spelling(string answer, string expected) =>
        Assert.Equal(MatchResult.Spelling, AnswerMatcher.MatchUzbek(answer, expected));

    [Theory]
    [InlineData("tosh", "o‘jarlik")]
    [InlineData("", "o‘jarlik")]
    [InlineData("uy", "uz")] // short words need the exact spelling
    public void Different_uzbek_word_is_no_match(string answer, string expected) =>
        Assert.Equal(MatchResult.NoMatch, AnswerMatcher.MatchUzbek(answer, expected));

    [Theory]
    [InlineData("고집", "고집")]
    [InlineData("고집 을 부리다", "고집을 부리다")]
    [InlineData("매연", "매연 (=연기)")]
    public void Korean_spacing_and_notes_are_not_mistakes(string answer, string expected) =>
        Assert.Equal(MatchResult.Exact, AnswerMatcher.MatchKorean(answer, expected));

    [Theory]
    [InlineData("갓다", "갔다")] // ㅆ → ㅅ: one jamo
    [InlineData("고짐", "고집")]
    public void One_jamo_slip_in_korean_is_spelling(string answer, string expected) =>
        Assert.Equal(MatchResult.Spelling, AnswerMatcher.MatchKorean(answer, expected));

    [Theory]
    [InlineData("연기", "매연")]
    [InlineData("maktab", "학교")]
    public void Different_korean_word_is_no_match(string answer, string expected) =>
        Assert.Equal(MatchResult.NoMatch, AnswerMatcher.MatchKorean(answer, expected));

    [Fact]
    public void Jamo_splits_syllables()
    {
        Assert.Equal(3, AnswerMatcher.Jamo("갔").Length);
        Assert.Equal(2, AnswerMatcher.Jamo("가").Length);
        Assert.Equal(1, AnswerMatcher.Levenshtein(AnswerMatcher.Jamo("갔"), AnswerMatcher.Jamo("갓")));
    }
}
