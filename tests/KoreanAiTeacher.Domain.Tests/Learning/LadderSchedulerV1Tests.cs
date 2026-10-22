using KoreanAiTeacher.Domain.Learning;

namespace KoreanAiTeacher.Domain.Tests.Learning;

public class LadderSchedulerV1Tests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 0, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Day1 = new(2026, 10, 1);

    private readonly LadderSchedulerV1 _scheduler = new();

    private ScheduleResult Apply(LearningState s, ReviewAnswer a, DateTimeOffset now, bool assisted = false, DateOnly? day = null) =>
        _scheduler.Apply(s, a, assisted, now, day ?? DateOnly.FromDateTime(now.UtcDateTime));

    [Fact]
    public void New_card_is_due_immediately()
    {
        Assert.True(LearningState.New().IsDue(T0));
    }

    [Fact]
    public void Forgot_returns_in_15_minutes_and_relearns()
    {
        var r = Apply(LearningState.New(), ReviewAnswer.Forgot, T0);

        Assert.Equal(LearningPhase.Relearning, r.State.Phase);
        Assert.Equal(T0.AddMinutes(15), r.State.DueAt);
        Assert.Equal(0, r.State.Lapses); // a new card being forgotten is not a lapse
        Assert.False(r.CountsAsIndependentSuccess);
    }

    [Fact]
    public void Forgetting_a_review_card_is_a_lapse_and_next_success_restarts_at_5h()
    {
        var review = new LearningState { Phase = LearningPhase.Review, Stage = 3, Interval = TimeSpan.FromDays(7), DueAt = T0 };

        var forgot = Apply(review, ReviewAnswer.Forgot, T0).State;
        Assert.Equal(1, forgot.Lapses);
        Assert.Equal(T0, forgot.LastLapseAt);

        var again = Apply(forgot, ReviewAnswer.Easy, T0.AddMinutes(20)).State;
        Assert.Equal(LearningPhase.Review, again.Phase);
        Assert.Equal(TimeSpan.FromHours(5), again.Interval);
        Assert.Equal(0, again.Stage);
    }

    [Theory]
    [InlineData(ReviewAnswer.Hard, 5)]
    [InlineData(ReviewAnswer.Easy, 24)]
    public void New_card_first_success(ReviewAnswer answer, int expectedHours)
    {
        var r = Apply(LearningState.New(), answer, T0);

        Assert.Equal(LearningPhase.Review, r.State.Phase);
        Assert.Equal(TimeSpan.FromHours(expectedHours), r.State.Interval);
        Assert.Equal(T0.AddHours(expectedHours), r.State.DueAt);
        Assert.True(r.CountsAsIndependentSuccess);
    }

    [Fact]
    public void Easy_on_review_moves_one_stage_up_the_ladder()
    {
        var s = LearningState.New();
        var now = T0;
        var seen = new List<TimeSpan>();

        for (var i = 0; i < 8; i++)
        {
            s = Apply(s, ReviewAnswer.Easy, now).State;
            seen.Add(s.Interval);
            now = s.DueAt!.Value;
        }

        Assert.Equal(
            new[] { 1d, 3, 7, 14, 30, 60, 60, 60 }.Select(TimeSpan.FromDays),
            seen);
    }

    [Fact]
    public void Hard_on_review_halves_interval_without_raising_stage()
    {
        var s = new LearningState { Phase = LearningPhase.Review, Stage = 4, Interval = TimeSpan.FromDays(14), DueAt = T0 };

        var r = Apply(s, ReviewAnswer.Hard, T0).State;

        Assert.Equal(4, r.Stage);
        Assert.Equal(TimeSpan.FromDays(7), r.Interval);
    }

    [Fact]
    public void Hard_on_review_never_goes_below_5_hours()
    {
        var s = new LearningState { Phase = LearningPhase.Review, Stage = 0, Interval = TimeSpan.FromHours(5), DueAt = T0 };

        Assert.Equal(TimeSpan.FromHours(5), Apply(s, ReviewAnswer.Hard, T0).State.Interval);
    }

    [Fact]
    public void Drill_before_due_does_not_promote()
    {
        var s = Apply(LearningState.New(), ReviewAnswer.Easy, T0).State; // due in 1 day

        var early = Apply(s, ReviewAnswer.Easy, T0.AddHours(1));

        Assert.False(early.WasDue);
        Assert.False(early.CountsAsIndependentSuccess);
        Assert.Equal(s.Interval, early.State.Interval);
        Assert.Equal(s.DueAt, early.State.DueAt);
        Assert.Equal(s.Stage, early.State.Stage);
    }

    [Fact]
    public void Forgot_during_early_drill_still_counts()
    {
        var s = Apply(LearningState.New(), ReviewAnswer.Easy, T0).State;
        s = Apply(s, ReviewAnswer.Easy, s.DueAt!.Value).State; // review, 3 days

        var r = Apply(s, ReviewAnswer.Forgot, T0.AddDays(1).AddHours(2));

        Assert.Equal(LearningPhase.Relearning, r.State.Phase);
        Assert.Equal(1, r.State.Lapses);
    }

    [Fact]
    public void Assisted_success_is_not_independent_and_does_not_jump()
    {
        var review = new LearningState { Phase = LearningPhase.Review, Stage = 3, Interval = TimeSpan.FromDays(7), DueAt = T0 };

        var r = Apply(review, ReviewAnswer.Easy, T0, assisted: true);

        Assert.False(r.CountsAsIndependentSuccess);
        Assert.Equal(3, r.State.Stage);
        Assert.True(r.State.Interval <= review.Interval);
        Assert.Equal(0, r.State.IndependentSuccessDays);
    }

    [Fact]
    public void Technical_error_changes_nothing()
    {
        var s = Apply(LearningState.New(), ReviewAnswer.Easy, T0).State;

        var r = Apply(s, ReviewAnswer.TechnicalError, s.DueAt!.Value);

        Assert.Same(s, r.State);
        Assert.False(r.CountsAsIndependentSuccess);
    }

    [Fact]
    public void Success_days_are_counted_per_distinct_local_day()
    {
        var s = LearningState.New();
        s = Apply(s, ReviewAnswer.Hard, T0, day: Day1).State;               // 5h later, still day 1 locally
        s = Apply(s, ReviewAnswer.Easy, s.DueAt!.Value, day: Day1).State;
        Assert.Equal(1, s.IndependentSuccessDays);

        s = Apply(s, ReviewAnswer.Easy, s.DueAt!.Value, day: Day1.AddDays(3)).State;
        Assert.Equal(2, s.IndependentSuccessDays);
    }

    [Fact]
    public void State_records_algorithm_version()
    {
        Assert.Equal("ladder-v1", Apply(LearningState.New(), ReviewAnswer.Easy, T0).State.AlgorithmVersion);
    }
}
