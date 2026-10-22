namespace Lugat.Domain.Reminders;

public enum ReminderSlot
{
    /// <summary>Monday–Friday morning practice reminder.</summary>
    Weekday,

    /// <summary>Saturday evening, only when a lesson is due that Saturday.</summary>
    SaturdayLesson,
}

/// <summary>Per-user reminder settings (spec NOT01, NOT04: the learner controls schedule and can switch them off).</summary>
public sealed record ReminderPreferences(bool Enabled, TimeOnly WeekdayTime, TimeOnly SaturdayTime)
{
    /// <summary>Spec NOT01 defaults: Mon–Fri 09:30, Saturday 22:00, nothing on Sunday.</summary>
    public static readonly ReminderPreferences Default = new(true, new TimeOnly(9, 30), new TimeOnly(22, 0));
}

/// <param name="Key">Unique per user and occurrence, so one reminder is never sent twice (spec NOT03, AC10).</param>
public sealed record ReminderOccurrence(ReminderSlot Slot, DateOnly LocalDate, DateTimeOffset ScheduledAt, string Key);

/// <summary>
/// Decides which reminder, if any, is due right now (spec NOT01–NOT03).
/// Only a reminder whose time has come and is at most <see cref="MaxDelay"/> late is returned,
/// so after downtime old reminders are not sent in a burst.
/// </summary>
public static class ReminderPlanner
{
    /// <summary>Spec NOT03 technical baseline: a reminder more than 30 minutes late is skipped.</summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(30);

    /// <param name="hasLessonOn">Whether the learner has a planned lesson on the given local date.</param>
    public static ReminderOccurrence? Due(
        Guid userId,
        DateTimeOffset now,
        TimeZoneInfo zone,
        ReminderPreferences preferences,
        Func<DateOnly, bool> hasLessonOn)
    {
        if (!preferences.Enabled)
        {
            return null;
        }

        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var today = DateOnly.FromDateTime(localNow.DateTime);

        // Yesterday matters only for a slot just before midnight that is still within the window after it.
        foreach (var date in new[] { today, today.AddDays(-1) })
        {
            var occurrence = Slot(userId, date, zone, preferences, hasLessonOn);
            if (occurrence is not null && now >= occurrence.ScheduledAt && now - occurrence.ScheduledAt <= MaxDelay)
            {
                return occurrence;
            }
        }

        return null;
    }

    private static ReminderOccurrence? Slot(Guid userId, DateOnly date, TimeZoneInfo zone, ReminderPreferences p, Func<DateOnly, bool> hasLessonOn)
    {
        ReminderSlot slot;
        TimeOnly time;
        if (date.DayOfWeek == DayOfWeek.Sunday)
        {
            return null;
        }

        if (date.DayOfWeek == DayOfWeek.Saturday)
        {
            if (!hasLessonOn(date))
            {
                return null;
            }

            (slot, time) = (ReminderSlot.SaturdayLesson, p.SaturdayTime);
        }
        else
        {
            (slot, time) = (ReminderSlot.Weekday, p.WeekdayTime);
        }

        var local = date.ToDateTime(time);
        var at = new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
        return new ReminderOccurrence(slot, date, at, $"{userId:N}:{date:yyyy-MM-dd}:{slot}");
    }
}
