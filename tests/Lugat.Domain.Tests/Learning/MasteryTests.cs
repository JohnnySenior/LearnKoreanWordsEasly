using Lugat.Domain.Learning;

namespace Lugat.Domain.Tests.Learning;

public class MasteryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static LearningState Strong() => new()
    {
        Phase = LearningPhase.Review,
        Stage = 3,
        Interval = TimeSpan.FromDays(7),
        IndependentSuccessDays = 3,
    };

    [Fact]
    public void Strong_state_is_mastered() => Assert.True(Mastery.IsMastered(Strong(), Now));

    [Fact]
    public void Needs_three_distinct_days() =>
        Assert.False(Mastery.IsMastered(Strong() with { IndependentSuccessDays = 2 }, Now));

    [Fact]
    public void Needs_interval_of_at_least_7_days() =>
        Assert.False(Mastery.IsMastered(Strong() with { Interval = TimeSpan.FromDays(3) }, Now));

    [Fact]
    public void Recent_lapse_blocks_mastery()
    {
        Assert.False(Mastery.IsMastered(Strong() with { LastLapseAt = Now.AddDays(-2) }, Now));
        Assert.True(Mastery.IsMastered(Strong() with { LastLapseAt = Now.AddDays(-8) }, Now));
    }

    [Fact]
    public void Both_directions_are_required()
    {
        var weak = Strong() with { IndependentSuccessDays = 1 };

        Assert.False(Mastery.IsMastered(Strong(), weak, Now));
        Assert.True(Mastery.IsMastered(Strong(), Strong(), Now));
    }
}
