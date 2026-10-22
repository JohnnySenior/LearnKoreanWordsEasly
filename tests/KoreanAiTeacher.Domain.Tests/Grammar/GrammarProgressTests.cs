using KoreanAiTeacher.Domain.Grammar;

namespace KoreanAiTeacher.Domain.Tests.Grammar;

public class GrammarProgressTests
{
    private static GrammarAttempt A(int day, bool good) => new(new DateOnly(2026, 10, day), good);

    [Fact]
    public void Opening_and_writing_move_the_item_forward()
    {
        Assert.Equal(GrammarMastery.New, GrammarProgress.Of(false, []));
        Assert.Equal(GrammarMastery.Seen, GrammarProgress.Of(true, []));
        Assert.Equal(GrammarMastery.Practising, GrammarProgress.Of(true, [A(1, false)]));
        Assert.Equal(GrammarMastery.Practising, GrammarProgress.Of(false, [A(1, true)])); // a sentence counts even without the page
    }

    [Fact]
    public void Independent_needs_three_good_days_and_a_good_last_sentence()
    {
        Assert.Equal(GrammarMastery.Practising, GrammarProgress.Of(true, [A(1, true), A(1, true), A(1, true), A(2, true)]));
        Assert.Equal(GrammarMastery.Independent, GrammarProgress.Of(true, [A(1, true), A(2, false), A(2, true), A(5, true)]));
        Assert.Equal(GrammarMastery.Practising, GrammarProgress.Of(true, [A(1, true), A(2, true), A(5, true), A(6, false)]));
        Assert.Equal(3, GrammarProgress.GoodDays([A(1, true), A(2, true), A(5, true), A(6, false)]));
    }
}
