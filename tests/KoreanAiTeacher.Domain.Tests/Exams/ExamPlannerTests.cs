using KoreanAiTeacher.Domain.Exams;

namespace KoreanAiTeacher.Domain.Tests.Exams;

public class ExamPlannerTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);

    [Fact]
    public void Without_a_date_there_is_no_plan()
    {
        Assert.Equal(ExamPhase.None, ExamPlanner.Plan(Today, null, 100, 10).Phase);
    }

    [Fact]
    public void New_material_is_spread_over_the_days_before_the_final_week()
    {
        // 15 November: 46 days left, 39 days for new material.
        var plan = ExamPlanner.Plan(Today, new DateOnly(2026, 11, 15), newWords: 780, grammarLeft: 40);

        Assert.Equal(ExamPhase.Study, plan.Phase);
        Assert.Equal((46, 39), (plan.DaysLeft, plan.StudyDays));
        Assert.Equal(20, plan.WordsPerDay); // 780 / 39
        Assert.Equal(8, plan.GrammarPerWeek); // 40 × 7 / 39 = 7.2, rounded up
        Assert.Equal(1, plan.MocksPerWeek);
    }

    [Fact]
    public void Paces_round_up_so_nothing_is_left_for_the_final_week()
    {
        var plan = ExamPlanner.Plan(Today, Today.AddDays(10), newWords: 7, grammarLeft: 1);

        Assert.Equal(3, plan.StudyDays);
        Assert.Equal(3, plan.WordsPerDay);
        Assert.Equal(3, plan.GrammarPerWeek);
        Assert.Equal(2, plan.MocksPerWeek); // under five weeks: two timed tests a week
    }

    [Fact]
    public void Nothing_left_means_a_zero_pace()
    {
        var plan = ExamPlanner.Plan(Today, Today.AddDays(60), 0, 0);

        Assert.Equal((0, 0), (plan.WordsPerDay, plan.GrammarPerWeek));
    }

    [Theory]
    [InlineData(7, ExamPhase.FinalWeek)]
    [InlineData(1, ExamPhase.FinalWeek)]
    [InlineData(0, ExamPhase.ExamDay)]
    [InlineData(-1, ExamPhase.Past)]
    [InlineData(8, ExamPhase.Study)]
    public void The_last_week_brings_no_new_material(int days, ExamPhase phase)
    {
        var plan = ExamPlanner.Plan(Today, Today.AddDays(days), 500, 20);

        Assert.Equal(phase, plan.Phase);
        if (phase != ExamPhase.Study)
        {
            Assert.Equal((0, 0), (plan.WordsPerDay, plan.GrammarPerWeek));
        }
    }
}
