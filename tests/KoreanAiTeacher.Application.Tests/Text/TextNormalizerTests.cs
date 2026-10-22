using KoreanAiTeacher.Application.Text;

namespace KoreanAiTeacher.Application.Tests.Text;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("o’jarlik", "o'jarlik")]
    [InlineData("oʻjarlik", "o'jarlik")]
    [InlineData("o`jarlik", "o'jarlik")]
    [InlineData("  Qochmoq,   oldini olmoq ", "qochmoq, oldini olmoq")]
    public void Uzbek_apostrophes_space_and_case_are_normalised(string input, string expected) =>
        Assert.Equal(expected, TextNormalizer.Key(input));

    [Fact]
    public void Decomposed_hangul_equals_composed() =>
        Assert.Equal(TextNormalizer.Key("한"), TextNormalizer.Key("한"));

    [Fact]
    public void Korean_endings_are_kept() =>
        Assert.NotEqual(TextNormalizer.Key("피하다"), TextNormalizer.Key("피해"));
}
