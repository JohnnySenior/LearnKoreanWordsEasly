namespace Lugat.Domain.Reminders;

public enum ReminderSlot
{
    /// <summary>The Monday–Friday morning reminder of KoreanAiTeacher (D-16); only in old outbox rows.</summary>
    Weekday,

    /// <summary>The daily reminder at the learner's time, on the days they chose (spec §8).</summary>
    Daily,

    /// <summary>L-64: once, about a day before a Premium trial ends.</summary>
    TrialEnd,
}

/// <summary>Days a reminder may come. Stored as flags; every day by default (spec §8).</summary>
[Flags]
public enum ReminderDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Every = 127,
}

public static class ReminderDaysExtensions
{
    public static ReminderDays Flag(this DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => ReminderDays.Monday,
        DayOfWeek.Tuesday => ReminderDays.Tuesday,
        DayOfWeek.Wednesday => ReminderDays.Wednesday,
        DayOfWeek.Thursday => ReminderDays.Thursday,
        DayOfWeek.Friday => ReminderDays.Friday,
        DayOfWeek.Saturday => ReminderDays.Saturday,
        _ => ReminderDays.Sunday,
    };

    public static bool Includes(this ReminderDays days, DayOfWeek day) => (days & day.Flag()) != 0;

    /// <summary>Monday first, as Uzbek calendars show the week.</summary>
    public static IReadOnlyList<DayOfWeek> ToList(this ReminderDays days) =>
        [.. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Where(d => days.Includes(d))];

    public static ReminderDays FromList(IEnumerable<DayOfWeek> days) =>
        days.Aggregate(ReminderDays.None, (all, d) => all | d.Flag());
}

/// <summary>Per-user reminder settings (spec §8, NOT04: the learner controls time and days and can switch them off).</summary>
public sealed record ReminderPreferences(bool Enabled, TimeOnly Time, ReminderDays Days)
{
    /// <summary>Spec §8: every day at 20:00 in the learner's time.</summary>
    public static readonly ReminderPreferences Default = new(true, new TimeOnly(20, 0), ReminderDays.Every);
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

    public static ReminderOccurrence? Due(Guid userId, DateTimeOffset now, TimeZoneInfo zone, ReminderPreferences preferences)
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
            var occurrence = Slot(userId, date, zone, preferences);
            if (occurrence is not null && now >= occurrence.ScheduledAt && now - occurrence.ScheduledAt <= MaxDelay)
            {
                return occurrence;
            }
        }

        return null;
    }

    private static ReminderOccurrence? Slot(Guid userId, DateOnly date, TimeZoneInfo zone, ReminderPreferences p)
    {
        if (!p.Days.Includes(date.DayOfWeek))
        {
            return null;
        }

        const ReminderSlot slot = ReminderSlot.Daily;
        var local = date.ToDateTime(p.Time);
        var at = new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
        return new ReminderOccurrence(slot, date, at, $"{userId:N}:{date:yyyy-MM-dd}:{slot}");
    }
}
