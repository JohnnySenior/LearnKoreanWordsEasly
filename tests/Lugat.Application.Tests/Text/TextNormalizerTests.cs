using Lugat.Application.Text;

namespace Lugat.Application.Tests.Text;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("o’jarlik", "o'jarlik")]
    [InlineData("oʻjarlik", "o'jarlik")]
    [InlineData("o`jarlik", "o'jarlik")]
    [InlineData("  Qochmoq,   oldini olmoq ", "qochmoq, oldini olmoq")]
    public void Uzbek_apostrophes_space_and_case_are_normalised(string input, string expected) =>
        Assert.Equal(expected, TextNormalizer.Key(input));

    [Theory]
    [InlineData("bo'lmoq", "bo‘lmoq")]
    [InlineData("O`zbek ma'no", "O‘zbek ma’no")]
    [InlineData("g’alaba  va  san’at ", "g‘alaba va san’at")]
    [InlineData("o‘g‘il", "o‘g‘il")]
    public void Uzbek_text_is_stored_with_one_apostrophe_form(string input, string expected) =>
        Assert.Equal(expected, TextNormalizer.Uzbek(input));

    [Fact]
    public void Decomposed_hangul_equals_composed() =>
        Assert.Equal(TextNormalizer.Key("한"), TextNormalizer.Key("한"));

    [Fact]
    public void Korean_endings_are_kept() =>
        Assert.NotEqual(TextNormalizer.Key("피하다"), TextNormalizer.Key("피해"));
}
