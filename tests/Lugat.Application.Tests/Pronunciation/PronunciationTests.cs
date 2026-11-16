using Lugat.Application;
using Lugat.Application.Pronunciation;

namespace Lugat.Application.Tests.Pronunciation;

public class PronunciationTests
{
    [Theory]
    [InlineData("거센소리되기", "undoshning kuchli nafas bilan aytilishi")]
    [InlineData("격음화", "undoshning kuchli nafas bilan aytilishi")]
    [InlineData("경음화", "undoshning tarangroq aytilishi")]
    [InlineData("비음화", "undoshning burun tovushiga aylanishi")]
    [InlineData("연음", "oxirgi undoshning keyingi bo‘g‘inga ulanib aytilishi")]
    public void Legacy_parenthesized_rule_labels_are_readable_without_rewriting_sound_examples(string label, string expected)
    {
        var note = $"ㄱ + ㅎ birikmasi ㅋ kabi aytiladi ({label}). Misol: (학교 → 학꾜).";
        var readable = PronunciationExplanation.Readable(note);
        Assert.Equal($"ㄱ + ㅎ birikmasi ㅋ kabi aytiladi ({expected}). Misol: (학교 → 학꾜).", readable);
        Assert.Equal(readable, PronunciationExplanation.Readable(readable));
        Assert.Equal(readable, PronunciationService.Check("학교", "학꾜", note, true).Explanation);
        Assert.Equal(readable, PronunciationService.Learner("학교", "학꾜", note, true)!.ExplanationUz);
    }

    [Fact]
    public void Explanations_without_labels_and_null_are_preserved()
    {
        Assert.Null(PronunciationExplanation.Readable(null));
        const string note = "ㄱ tovushi ㅁ oldida ㅇ kabi aytiladi. 국물 → 궁물.";
        Assert.Equal(note, PronunciationExplanation.Readable(note));
    }

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
