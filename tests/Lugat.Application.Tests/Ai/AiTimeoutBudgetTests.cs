using Lugat.Application.Ai;

namespace Lugat.Application.Tests.Ai;

public class AiTimeoutBudgetTests
{
    [Theory]
    [InlineData(75, 80, 2, 40)] // reserve a fallback while the first provider is running
    [InlineData(75, 79, 1, 75)] // fast primary failure returns its unused time to fallback
    [InlineData(75, 39, 1, 39)] // a slow primary cannot extend the overall deadline
    [InlineData(20, 79, 1, 20)] // explicit short requests still stay short
    [InlineData(75, 0, 1, 0)]
    [InlineData(75, -1, 1, 0)]
    public void Attempts_share_only_the_remaining_time(int request, int remaining, int providers, int expected)
    {
        Assert.Equal(TimeSpan.FromSeconds(expected), AiGateway.AttemptTimeout(TimeSpan.FromSeconds(request), TimeSpan.FromSeconds(remaining), providers));
    }
}
