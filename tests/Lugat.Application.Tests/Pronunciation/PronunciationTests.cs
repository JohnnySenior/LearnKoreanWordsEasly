using Lugat.Application;
using Lugat.Application.Pronunciation;

namespace Lugat.Application.Tests.Pronunciation;

public class PronunciationTests
{
    [Theory]
    [InlineData("국물", "궁물")]
    [InlineData("같이", "가치")]
    [InlineData("학교", "학꾜")]
    [InlineData("한국말", "한궁말")]
    [InlineData("독립", "동닙")]
    [InlineData("연락", "열락")]
    [InlineData("좋다", "조타")]
    [InlineData("읽다", "익따")]
    public void Only_reviewed_differences_are_learner_hints(string spelling, string reading)
    {
        var value = PronunciationService.Check(spelling, reading, "Tovush o‘zgaradi.", true);
        Assert.Null(PronunciationService.Learner(spelling, value.Reading, value.Explanation, false));
        Assert.Equal(reading, PronunciationService.Learner(spelling, value.Reading, value.Explanation, true)!.Reading);
    }

    [Fact]
    public void Unknown_is_distinct_from_a_reviewed_unchanged_reading()
    {
        Assert.Equal((null, null), PronunciationService.Check("사람", null, null, false));
        Assert.Null(PronunciationService.Learner("사람", null, null, false));
        Assert.Equal(("사람", null), PronunciationService.Check("사람", "사람", null, true));
        Assert.Null(PronunciationService.Learner("사람", "사람", null, true));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("gungmul", "Izoh.")]
    [InlineData("[궁물]", "Izoh.")]
    [InlineData("궁물", null)]
    [InlineData("궁물", "Изоҳ")]
    [InlineData("궁물", "Izoh\nYana")]
    public void Invalid_or_incomplete_readings_cannot_be_approved(string? reading, string? explanation)
    {
        Assert.Throws<ValidationException>(() => PronunciationService.Check("국물", reading, explanation, true));
    }
}
