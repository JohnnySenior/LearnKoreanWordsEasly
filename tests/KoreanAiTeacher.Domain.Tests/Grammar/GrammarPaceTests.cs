using KoreanAiTeacher.Domain.Grammar;

namespace KoreanAiTeacher.Domain.Tests.Grammar;

public class GrammarPaceTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);

    [Theory]
    [InlineData(20, 2, 7)]   // 3 days (today included): 7, 7, 6
    [InlineData(20, 0, 20)]  // due today: everything
    [InlineData(20, -3, 20)] // overdue: everything
    [InlineData(6, 9, 1)]
    [InlineData(0, 2, 0)]
    public void Spreads_what_is_left_over_the_days_until_the_deadline(int notStarted, int dueInDays, int expected) =>
        Assert.Equal(expected, GrammarPace.TodayTarget(notStarted, Today, Today.AddDays(dueInDays)));

    [Fact]
    public void Without_a_deadline_a_few_a_day() =>
        Assert.Equal((5, 3), (GrammarPace.TodayTarget(20, Today, null), GrammarPace.TodayTarget(3, Today, null)));
}
