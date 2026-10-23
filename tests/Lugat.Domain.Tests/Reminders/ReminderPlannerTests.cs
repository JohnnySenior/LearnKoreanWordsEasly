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
    public void The_reminder_is_due_every_day_at_2000_by_default()
    {
        // 5 October 2026 is a Monday, 3 October a Saturday, 4 October a Sunday.
        Assert.Null(Due(Kst(5, 19, 59)));
        foreach (var day in new[] { 3, 4, 5 })
        {
            var due = Due(Kst(day, 20, 0));
            Assert.NotNull(due);
            Assert.Equal(ReminderSlot.Daily, due.Slot);
            Assert.Equal(Kst(day, 20, 0), due.ScheduledAt);
            Assert.Equal($"{User:N}:2026-10-0{day}:Daily", due.Key);
        }
    }

    [Fact]
    public void Reminder_later_than_30_minutes_is_not_sent()
    {
        Assert.NotNull(Due(Kst(5, 20, 30)));
        Assert.Null(Due(Kst(5, 20, 31)));
    }

    [Fact]
    public void Only_the_chosen_days_get_a_reminder()
    {
        var weekdays = ReminderPreferences.Default with { Days = ReminderDays.Every & ~(ReminderDays.Saturday | ReminderDays.Sunday) };

        Assert.NotNull(Due(Kst(5, 20, 0), weekdays));
        Assert.Null(Due(Kst(3, 20, 0), weekdays));
        Assert.Null(Due(Kst(4, 20, 0), weekdays));
        Assert.Null(Due(Kst(5, 20, 0), ReminderPreferences.Default with { Days = ReminderDays.None }));
    }

    [Fact]
    public void Days_go_to_and_from_a_list_monday_first()
    {
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Saturday, DayOfWeek.Sunday], (ReminderDays.Sunday | ReminderDays.Monday | ReminderDays.Saturday).ToList());
        Assert.Equal(ReminderDays.Every, ReminderDaysExtensions.FromList(Enum.GetValues<DayOfWeek>()));
    }

    [Fact]
    public void Disabled_reminders_are_never_due() =>
        Assert.Null(Due(Kst(5, 20, 0), prefs: ReminderPreferences.Default with { Enabled = false }));

    [Fact]
    public void Custom_time_is_respected()
    {
        var prefs = ReminderPreferences.Default with { Time = new TimeOnly(23, 50) };

        Assert.NotNull(Due(Kst(5, 23, 55), prefs: prefs));
        Assert.NotNull(Due(Kst(6, 0, 10), prefs: prefs)); // just after midnight, still yesterday's reminder
        Assert.Null(Due(Kst(6, 0, 21), prefs: prefs));
    }

    [Fact]
    public void Same_occurrence_has_the_same_key_within_its_window() =>
        Assert.Equal(Due(Kst(5, 20, 1))!.Key, Due(Kst(5, 20, 29))!.Key);
}
