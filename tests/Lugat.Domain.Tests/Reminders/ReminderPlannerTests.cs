using Lugat.Domain.Reminders;

namespace Lugat.Domain.Tests.Reminders;

public class ReminderPlannerTests
{
    private static readonly TimeZoneInfo Seoul = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
    private static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static DateTimeOffset Kst(int day, int hour, int minute) => new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(9));

    private static ReminderOccurrence? Due(DateTimeOffset now, ReminderPreferences? prefs = null) =>
        ReminderPlanner.Due(User, now.ToUniversalTime(), Seoul, prefs ?? ReminderPreferences.Default);

    [Fact]
    public void Weekday_reminder_is_due_at_0930_kst()
    {
        // 5 October 2026 is a Monday.
        Assert.Null(Due(Kst(5, 9, 29)));

        var due = Due(Kst(5, 9, 30));

        Assert.NotNull(due);
        Assert.Equal(ReminderSlot.Weekday, due.Slot);
        Assert.Equal(Kst(5, 9, 30), due.ScheduledAt);
        Assert.Equal($"{User:N}:2026-10-05:Weekday", due.Key);
    }

    [Fact]
    public void Reminder_later_than_30_minutes_is_not_sent()
    {
        Assert.NotNull(Due(Kst(5, 10, 0)));
        Assert.Null(Due(Kst(5, 10, 1)));
    }

    [Fact]
    public void Weekend_has_no_reminder()
    {
        Assert.Null(Due(Kst(4, 9, 30))); // Sunday
        Assert.Null(Due(Kst(3, 9, 30))); // Saturday: the lesson evening slot went with the lessons (spec §3.1)
        Assert.Null(Due(Kst(3, 22, 0)));
    }

    [Fact]
    public void Weekdays_get_no_evening_reminder() => Assert.Null(Due(Kst(6, 22, 0)));

    [Fact]
    public void Disabled_reminders_are_never_due() =>
        Assert.Null(Due(Kst(5, 9, 30), prefs: ReminderPreferences.Default with { Enabled = false }));

    [Fact]
    public void Custom_time_is_respected()
    {
        var prefs = ReminderPreferences.Default with { WeekdayTime = new TimeOnly(23, 50) };

        Assert.NotNull(Due(Kst(5, 23, 55), prefs: prefs));
        Assert.NotNull(Due(Kst(6, 0, 10), prefs: prefs)); // just after midnight, still yesterday's reminder
        Assert.Null(Due(Kst(6, 0, 21), prefs: prefs));
    }

    [Fact]
    public void Same_occurrence_has_the_same_key_within_its_window() =>
        Assert.Equal(Due(Kst(5, 9, 31))!.Key, Due(Kst(5, 9, 59))!.Key);
}
