namespace Lugat.Domain.Users;

/// <summary>An app user. All personal data hangs off <see cref="Id"/>. Sign-in logins move to their own table in stage 1 (spec §5.1).</summary>
public sealed class User
{
    public Guid Id { get; set; }

    public long TelegramUserId { get; set; }

    public string? FirstName { get; set; }

    public string? Username { get; set; }

    /// <summary>IANA time zone. Reminders and local days use Korea time by default (spec §5.1).</summary>
    public string TimeZone { get; set; } = "Asia/Seoul";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public bool RemindersEnabled { get; set; } = true;

    public TimeOnly WeekdayReminderTime { get; set; } = new(9, 30);

    /// <summary>Set when Telegram refused a message because the bot was not started or was blocked (spec NOT02).</summary>
    public DateTimeOffset? BotBlockedAt { get; set; }

    /// <summary>AI service chosen in settings; null = the server default (D-21).</summary>
    public Ai.AiProviderKind? AiProvider { get; set; }

    public TimeZoneInfo Zone() =>
        TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out var zone) ? zone : TimeZoneInfo.Utc;

    public Reminders.ReminderPreferences ReminderPreferences() =>
        new(RemindersEnabled, WeekdayReminderTime);
}
