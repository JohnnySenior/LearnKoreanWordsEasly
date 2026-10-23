using Lugat.Domain.Premium;

namespace Lugat.Domain.Tests.Premium;

public class PremiumMathTests
{
    [Theory]
    [InlineData(5000, 0, 5000)]
    [InlineData(5000, 10, 4500)]
    [InlineData(12000, 15, 10200)]
    [InlineData(5000, 33, 3350)]
    [InlineData(5000, 100, 0)]
    public void Discounts_are_a_percentage_of_the_price(int price, int percent, int expected) =>
        Assert.Equal(expected, PremiumMath.Discounted(price, percent));

    [Theory]
    [InlineData(4500, 30, 1350)]
    [InlineData(10200, 30, 3060)]
    [InlineData(3335, 30, 1001)] // 1000.5 rounds up
    public void The_commission_is_a_share_of_what_was_paid(int amount, int percent, int expected) =>
        Assert.Equal(expected, PremiumMath.Commission(amount, percent));

    [Fact]
    public void A_new_period_starts_where_the_running_one_ends()
    {
        var now = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(now, PremiumMath.NextStart(now, []));
        Assert.Equal(now, PremiumMath.NextStart(now, [now.AddDays(-3)])); // the old one is over
        Assert.Equal(now.AddDays(20), PremiumMath.NextStart(now, [now.AddDays(5), now.AddDays(20)]));
    }
}
