using KoreanAiTeacher.Domain.Lessons;

namespace KoreanAiTeacher.Domain.Tests.Lessons;

public class LessonCalendarTests
{
    private static readonly TimeZoneInfo Seoul = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");

    private static DateTimeOffset Kst(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, TimeSpan.FromHours(9));

    [Theory]
    [InlineData(9, 28, 10, 9, 29)]  // Monday → Tuesday
    [InlineData(9, 29, 22, 9, 29)]  // Tuesday before 23:00 → same evening
    [InlineData(9, 29, 23, 10, 1)]  // Tuesday exactly 23:00 → Thursday
    [InlineData(10, 1, 23, 10, 3)]  // Thursday after the slot → Saturday
    [InlineData(10, 3, 23, 10, 6)]  // Saturday after the slot → next Tuesday, Sunday is free
    public void Next_slot_is_tuesday_thursday_or_saturday_at_23_kst(int month, int day, int hour, int expectedMonth, int expectedDay)
    {
        var slot = LessonCalendar.NextSlot(Kst(month, day, hour), Seoul);

        Assert.Equal(Kst(expectedMonth, expectedDay, 23), slot);
        Assert.Equal(TimeSpan.Zero, slot.Offset); // stored in UTC (spec SRS05)
    }

    [Fact]
    public void Next_slot_uses_the_learners_own_days()
    {
        var slot = LessonCalendar.NextSlot(Kst(10, 3, 23), Seoul, [DayOfWeek.Wednesday]); // Saturday night

        Assert.Equal(Kst(10, 7, 23), slot); // next Wednesday
    }

    [Fact]
    public void Days_are_deduplicated_monday_first_and_never_empty()
    {
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Sunday], LessonCalendar.Normalize([DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Sunday]));
        Assert.Equal(LessonCalendar.DefaultDays, LessonCalendar.Normalize([(DayOfWeek)9]));
    }
}
