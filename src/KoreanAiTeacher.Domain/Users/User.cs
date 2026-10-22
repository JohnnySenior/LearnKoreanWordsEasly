namespace KoreanAiTeacher.Domain.Users;

/// <summary>An app user, identified by Telegram (spec F01, SEC02). All personal data hangs off <see cref="Id"/>.</summary>
public sealed class User
{
    public Guid Id { get; set; }

    public long TelegramUserId { get; set; }

    public string? FirstName { get; set; }

    public string? Username { get; set; }

    /// <summary>IANA time zone. Lessons and reminders use Korea time by default (spec PLAN02).</summary>
    public string TimeZone { get; set; } = "Asia/Seoul";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public bool RemindersEnabled { get; set; } = true;

    public TimeOnly WeekdayReminderTime { get; set; } = new(9, 30);

    public TimeOnly SaturdayReminderTime { get; set; } = new(22, 0);

    /// <summary>Set when Telegram refused a message because the bot was not started or was blocked (spec NOT02).</summary>
    public DateTimeOffset? BotBlockedAt { get; set; }

    /// <summary>AI service chosen in settings; null = the server default (D-21).</summary>
    public Ai.AiProviderKind? AiProvider { get; set; }

    /// <summary>TOPIK date given by the learner (D-34). Not checked against the official schedule.</summary>
    public DateOnly? ExamDate { get; set; }

    /// <summary>Target TOPIK II level, 3–6; optional.</summary>
    public int? TargetLevel { get; set; }

    /// <summary>Vocabulary tables handed in per lesson, 1–6 (D-43). The teacher's default is two (60 words).</summary>
    public int TablesPerLesson { get; set; } = Lessons.LessonCalendar.DefaultTablesPerLesson;

    /// <summary>Lesson weekdays as <see cref="DayOfWeek"/> numbers (D-43). Default Tue/Thu/Sat (spec PLAN02).</summary>
    public int[] LessonDays { get; set; } = [.. Lessons.LessonCalendar.DefaultDays.Select(d => (int)d)];

    public DayOfWeek[] LessonWeekdays() => Lessons.LessonCalendar.Normalize(LessonDays.Select(d => (DayOfWeek)d));

    public TimeZoneInfo Zone() =>
        TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out var zone) ? zone : TimeZoneInfo.Utc;

    public Reminders.ReminderPreferences ReminderPreferences() =>
        new(RemindersEnabled, WeekdayReminderTime, SaturdayReminderTime);
}
