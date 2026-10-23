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
    [InlineData(25, true, 10, 10, 5)]
    [InlineData(25, false, 13, 12, 0)]
    [InlineData(29, true, 12, 11, 6)]
    [InlineData(14, true, 6, 5, 3)]
    public void Question_types_follow_the_spec_proportions(int words, bool listening, int meaning, int korean, int heard)
    {
        var unit = Enumerable.Range(0, words).Select(i => W($"{(char)('가' + i)}다", $"ma’no {i}")).ToList();

        var questions = UnitTestGenerator.Build(unit, unit, listening, new Random(7));

        Assert.Equal(words, questions.Count);
        Assert.Equal(unit.Select(w => w.ItemId).Order(), questions.Select(q => q.LearningItemId!.Value).Order()); // one per word
        Assert.Equal(meaning, questions.Count(q => q.Kind == TestQuestionKind.WordMeaning));
        Assert.Equal(korean, questions.Count(q => q.Kind == TestQuestionKind.WordKorean));
        Assert.Equal(heard, questions.Count(q => q.Kind == TestQuestionKind.WordListening));
        Assert.All(questions, q => Assert.Contains(q.Correct, q.Options));
        Assert.All(questions.Where(q => q.Kind == TestQuestionKind.WordListening), q => Assert.DoesNotContain(q.BaseForm!, q.Prompt));
    }
}
