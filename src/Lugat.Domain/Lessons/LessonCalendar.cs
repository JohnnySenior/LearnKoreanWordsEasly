namespace Lugat.Domain.Lessons;

/// <summary>
/// Vocabulary hand-in rhythm: lesson days at 23:00 in the learner's time zone (Asia/Seoul by default).
/// The default is the teacher's Tuesday, Thursday and Saturday with two tables each (spec PLAN02); every learner
/// can choose their own days and tables per lesson in settings (D-43). Only suggests dates; the learner confirms them.
/// </summary>
public static class LessonCalendar
{
    public static readonly DayOfWeek[] DefaultDays = [DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday];

    public const int DefaultTablesPerLesson = 2;

    public const int MaxTablesPerLesson = 6;

    /// <summary>Words in one book table (spec §1); only used to show the weekly amount.</summary>
    public const int WordsPerTable = 30;

    public static readonly TimeOnly LessonTime = new(23, 0);

    /// <summary>Distinct valid weekdays, Monday first; the defaults when nothing valid is left.</summary>
    public static DayOfWeek[] Normalize(IEnumerable<DayOfWeek> days)
    {
        var clean = days.Where(d => Enum.IsDefined(d)).Distinct().OrderBy(d => ((int)d + 6) % 7).ToArray();
        return clean.Length == 0 ? DefaultDays : clean;
    }

    /// <summary>The first default (Tue/Thu/Sat) slot strictly after <paramref name="after"/>.</summary>
    public static DateTimeOffset NextSlot(DateTimeOffset after, TimeZoneInfo zone) => NextSlot(after, zone, DefaultDays);

    /// <summary>The first slot on one of <paramref name="days"/> strictly after <paramref name="after"/>.</summary>
    public static DateTimeOffset NextSlot(DateTimeOffset after, TimeZoneInfo zone, IReadOnlyCollection<DayOfWeek> days)
    {
        var lessonDays = Normalize(days);
        var local = TimeZoneInfo.ConvertTime(after, zone);
        var day = DateOnly.FromDateTime(local.DateTime);
        for (var i = 0; i < 8; i++)
        {
            var candidateDay = day.AddDays(i);
            if (!lessonDays.Contains(candidateDay.DayOfWeek))
            {
                continue;
            }

            var localTime = candidateDay.ToDateTime(LessonTime);
            var slot = new DateTimeOffset(localTime, zone.GetUtcOffset(localTime));
            if (slot > after)
            {
                return slot.ToUniversalTime();
            }
        }

        throw new InvalidOperationException("No lesson slot within a week.");
    }
}
