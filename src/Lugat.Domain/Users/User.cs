namespace Lugat.Domain.Users;

/// <summary>What a user may do besides learning (spec §5.1). A user can hold several roles.</summary>
[Flags]
public enum UserRoles
{
    None = 0,
    Learner = 1,
    Teacher = 2,
    Editor = 4,
    Admin = 8,
}

/// <summary>Colour theme chosen in settings; <see cref="Device"/> follows the phone (spec §4.7).</summary>
public enum ThemeChoice
{
    Device,
    Light,
    Dark,
}

/// <summary>
/// An app user (spec §5.1). All personal data hangs off <see cref="Id"/>. How the user signs in lives in
/// <see cref="UserLogin"/>: one user may have a Telegram and a Google login.
/// </summary>
public sealed class User
{
    public Guid Id { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>IANA time zone. Reminders and local days use Korea time by default (spec §5.1).</summary>
    public string TimeZone { get; set; } = "Asia/Seoul";

    public UserRoles Roles { get; set; } = UserRoles.Learner;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Show the Latin reading of Korean words; on by default for beginners (spec §2.3).</summary>
    public bool ShowRomanization { get; set; } = true;

    public ThemeChoice Theme { get; set; }

    public bool RemindersEnabled { get; set; } = true;

    /// <summary>When the daily reminder comes, in the user's time zone (spec §8: 20:00 by default).</summary>
    public TimeOnly ReminderTime { get; set; } = new(20, 0);

    public Reminders.ReminderDays ReminderDays { get; set; } = Reminders.ReminderDays.Every;

    /// <summary>Set when Telegram refused a message because the bot was not started or was blocked (spec NOT02).</summary>
    public DateTimeOffset? BotBlockedAt { get; set; }

    /// <summary>AI service chosen in settings; null = the server default (D-21).</summary>
    public Ai.AiProviderKind? AiProvider { get; set; }

    /// <summary>
    /// Raised on logout and account deletion. Every token carries the value it was issued with, so older tokens
    /// stop working at once (L-13).
    /// </summary>
    public int SessionVersion { get; set; }

    /// <summary>
    /// Short code for the bank transfer note, e.g. "L-7K3Q" (spec §7.3), so the admin finds the payer. Made when the
    /// learner first opens the Premium page.
    /// </summary>
    public string? PaymentCode { get; set; }

    /// <summary>
    /// A teacher's promo code the learner came with (an invite link or <c>/?ref=CODE</c>, spec §9.1): the Premium page
    /// fills it in.
    /// </summary>
    public Guid? PromoCodeId { get; set; }

    /// <summary>The ad channel the user first came from (<c>/?src=tg</c>, L-40); null for a plain visit.</summary>
    public string? SignupSource { get; set; }

    /// <summary>The account was deleted: personal data is gone, the row stays only for anonymous records (spec §5.3).</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    public uint Version { get; set; }

    public TimeZoneInfo Zone() =>
        TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out var zone) ? zone : TimeZoneInfo.Utc;

    public Reminders.ReminderPreferences ReminderPreferences() =>
        new(RemindersEnabled, ReminderTime, ReminderDays);
}

public enum LoginProvider
{
    Telegram,
    Google,
}

/// <summary>
/// One way to sign in (spec §5.1). <see cref="ProviderUserId"/> is unique per provider, so one Telegram or Google
/// account never makes two users.
/// </summary>
public sealed class UserLogin
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public LoginProvider Provider { get; set; }

    /// <summary>Telegram user id, or Google's <c>sub</c>.</summary>
    public string ProviderUserId { get; set; } = "";

    /// <summary>Telegram username or Google email; only for showing which account is linked.</summary>
    public string? Display { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastUsedAt { get; set; }
}
