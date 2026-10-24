using Lugat.Application.Catalog;
using Lugat.Domain.Testing;

namespace Lugat.Application.Tests.Catalog;

public class UnitTestGeneratorTests
{
    private static UnitTestWord W(string korean, string uzbek, string pos = "명", string? note = null) =>
        new(Guid.NewGuid(), korean, note is null ? korean : $"{korean} {note}", uzbek, pos);

    [Fact]
    public void Wrong_options_are_never_a_homonym_or_a_synonym_of_the_answer()
    {
        var again = W("또", "yana", "부");
        var pool = new List<UnitTestWord>
        {
            again,
            W("더", "ko‘proq; yana", "부"), // synonym: shares "yana"
            W("또", "yana bir", "부", "(2)"), // homonym: written the same
            W("나", "men (do‘stona)", "대"),
            W("저", "men", "대"), // synonym of 나 through "men"
            W("자주", "tez-tez", "부"),
            W("다시", "qaytadan", "부"),
            W("빨리", "tez", "부"),
            W("학교", "maktab"),
            W("집", "uy"),
        };

        for (var seed = 0; seed < 50; seed++)
        {
            var wrong = UnitTestGenerator.Distractors(again, pool, new Random(seed));
            Assert.Equal(3, wrong.Count);
            Assert.DoesNotContain(wrong, w => w.Korean is "또" or "더");
            Assert.All(wrong, w => Assert.Equal("부", w.Pos)); // the same part of speech while there are enough

            var me = UnitTestGenerator.Distractors(pool[3], pool, new Random(seed));
            Assert.DoesNotContain(me, w => w.Korean == "저");
        }
    }

    [Theory]
    [InlineData(25, true, 6, 6, 8, 2, 3)]
    [InlineData(25, false, 8, 7, 10, 0, 0)]
    [InlineData(29, true, 7, 7, 9, 3, 3)]
    [InlineData(14, true, 4, 3, 4, 1, 2)]
    public void Question_types_follow_the_proportions(int words, bool listening, int meaning, int korean, int typed, int heard, int heardTyped)
    {
        var unit = Enumerable.Range(0, words).Select(i => W($"{(char)('가' + i)}다", $"ma’no {i}")).ToList();

        var questions = UnitTestGenerator.Build(unit, unit, listening, new Random(7));

        Assert.Equal(words, questions.Count);
        Assert.Equal(unit.Select(w => w.ItemId).Order(), questions.Select(q => q.LearningItemId!.Value).Order()); // one per word
        Assert.Equal(meaning, questions.Count(q => q.Kind == TestQuestionKind.WordMeaning));
        Assert.Equal(korean, questions.Count(q => q.Kind == TestQuestionKind.WordKorean));
        Assert.Equal(typed, questions.Count(q => q.Kind == TestQuestionKind.WordKoreanTyped));
        Assert.Equal(heard, questions.Count(q => q.Kind == TestQuestionKind.WordListening));
        Assert.Equal(heardTyped, questions.Count(q => q.Kind == TestQuestionKind.WordListeningTyped));
        Assert.All(questions.Where(q => !TestGrader.IsTyped(q.Kind)), q => Assert.Contains(q.Correct, q.Options));
        Assert.All(questions.Where(q => TestGrader.IsTyped(q.Kind)), q => Assert.Empty(q.Options));
        Assert.All(questions.Where(q => TestGrader.IsListening(q.Kind)), q => Assert.DoesNotContain(q.BaseForm!, q.Prompt));
    }

    [Fact]
    public void A_typed_question_says_which_word_is_meant_when_two_share_the_meaning()
    {
        // L-37: 안 and 아니다 are both "emas"; typing from Uzbek needs the hint.
        var an = W("안", "emas", "부");
        var anida = W("아니다", "emas", "형");
        var unit = new List<UnitTestWord> { an, anida, W("집", "uy"), W("학교", "maktab"), W("가다", "bormoq", "동") };

        var typed = Enumerable.Range(0, 30)
            .SelectMany(seed => UnitTestGenerator.Build(unit, unit, false, new Random(seed)))
            .Where(q => q.Kind == TestQuestionKind.WordKoreanTyped)
            .ToList();

        Assert.Contains(typed, q => q.Correct == "안" && q.Prompt == "emas (ravish · shu ma’noli boshqa so‘z ham bor)");
        Assert.Contains(typed, q => q.Correct == "집" && q.Prompt == "uy");
    }

    [Theory]
    [InlineData("집", true, null)]
    [InlineData(" 집 ", true, null)]
    [InlineData("짐", false, "Imlo xatosi")]
    [InlineData("학교", false, null)]
    public void A_typed_unit_answer_must_be_the_word_itself(string given, bool ok, string? note)
    {
        var q = new TestQuestion(0, TestQuestionKind.WordKoreanTyped, "uy", null, null, [], "집", null, null, null, false);

        Assert.Equal((ok, note), TestGrader.Grade(q, given));
    }
}
