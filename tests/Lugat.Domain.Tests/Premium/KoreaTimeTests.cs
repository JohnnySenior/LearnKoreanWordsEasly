namespace Lugat.Domain.Tests.Premium;

public class KoreaTimeTests
{
    [Fact]
    public void Days_and_months_turn_at_midnight_in_Korea_not_in_UTC()
    {
        var lateUtc = new DateTimeOffset(2026, 9, 30, 15, 30, 0, TimeSpan.Zero); // 1 October, 00:30 in Seoul

        Assert.Equal(new DateOnly(2026, 10, 1), KoreaTime.Day(lateUtc));
        Assert.Equal(new DateOnly(2026, 10, 1), KoreaTime.MonthOf(lateUtc));
        Assert.Equal(new DateOnly(2026, 9, 1), KoreaTime.MonthOf(lateUtc.AddHours(-1)));
    }

    [Fact]
    public void A_day_starts_at_midnight_Seoul_given_in_UTC()
    {
        var start = KoreaTime.StartOf(new DateOnly(2026, 10, 1));

        Assert.Equal(new DateTimeOffset(2026, 9, 30, 15, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(TimeSpan.Zero, start.Offset); // PostgreSQL takes only UTC
    }
}
