using Lugat.Application;
using Lugat.Application.Pronunciation;

namespace Lugat.Application.Tests.Pronunciation;

public class PronunciationTests
{
    [Theory]
    [InlineData("들어가다", "드러가다")]
    [InlineData("먹어요", "머거요")]
    [InlineData("옷이", "오시")]
    [InlineData("있어요", "이써요")]
    public void Korean_prose_for_an_exact_simple_liaison_becomes_an_Uzbek_note(string korean, string reading)
    {
        const string legacy = "받침 ’ㄹ’ 다음 모음 ’ㅓ’가 이어지며 연음화: ㄹ이 다음 음절 초성으로 이동";
        var note = PronunciationExplanation.ForWord(korean, reading, legacy);
        Assert.Equal($"Bo‘g‘in oxiridagi undosh keyingi unliga ulanib o‘qiladi. «{korean}» so‘zi «{reading}» deb o‘qiladi.", note);
        Assert.Equal(note, PronunciationExplanation.ForWord(korean, reading, note));
        // L-83: the learner gets the named rule instead of the bare "X is read Y".
        var learner = PronunciationService.Learner(korean, reading, legacy, true)!;
        Assert.Null(learner.ExplanationUz);
        Assert.Contains(learner.Rules!, r => r.NameUz == "Ulanish" && r.NameKo == "연음");
        Assert.Null(PronunciationService.Learner(korean, reading, legacy, false));
    }

    [Theory]
    [InlineData("국물", "궁물")]
    [InlineData("같이", "가치")]
    [InlineData("좋아요", "조아요")]
    [InlineData("읽어요", "일거요")]
    public void Other_changes_get_only_the_existing_reading_without_a_guessed_rule(string korean, string reading)
    {
        var note = PronunciationExplanation.ForWord(korean, reading, "한국어 설명입니다. 소리가 바뀝니다.");
        Assert.Equal($"«{korean}» so‘zi «{reading}» deb o‘qiladi.", note);
    }

    [Fact]
    public void Uzbek_notes_and_missing_readings_are_not_replaced_with_guesses()
    {
        const string note = "ㄱ tovushi ㅁ oldida ㅇ kabi aytiladi. Misol: 국물 → 궁물; 한국말 → 한궁말.";
        Assert.Equal(note, PronunciationExplanation.ForWord("국물", "궁물", note));
        Assert.Null(PronunciationExplanation.ForWord("국물", "궁물", null));
        Assert.Null(PronunciationExplanation.ForWord("국물", null, "한국어 설명입니다."));
        Assert.Equal("So‘z yozilganidek o‘qiladi.", PronunciationExplanation.ForWord("사람", "사람", "그대로 읽습니다."));
    }

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
