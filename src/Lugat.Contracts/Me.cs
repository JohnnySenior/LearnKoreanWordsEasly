namespace Lugat.Contracts;

/// <param name="RemindersAvailable">False when the server has no bot configured.</param>
/// <param name="BotBlocked">Telegram refused the last message: the learner should press Start in the bot (spec NOT02).</param>
public sealed record PreferencesDto(
    string TimeZone,
    bool RemindersEnabled,
    TimeOnly WeekdayReminderTime,
    bool RemindersAvailable,
    bool BotBlocked);

/// <summary>Null fields are left unchanged.</summary>
public sealed record UpdatePreferencesRequest(bool? RemindersEnabled, TimeOnly? WeekdayReminderTime);

public sealed record TestReminderResult(bool Delivered, string Message);

/// <summary>Plain progress numbers for one learner; never compared with anyone else (spec UX08).</summary>
public sealed record ProgressDto(
    int Words,
    int Seen,
    int RecalledKoreanToUzbek,
    int RecalledUzbekToKorean,
    int Learned,
    int DueNow,
    int ReviewsLast7Days,
    IReadOnlyList<HardWordDto> Hardest);

public sealed record HardWordDto(string Korean, string Uzbek, int ForgotKoreanToUzbek, int ForgotUzbekToKorean);

public sealed record BackupRunDto(DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, long SizeBytes, bool Verified, string? Error);

/// <param name="LastVerifiedAt">The newest backup whose restore check passed (spec REL04).</param>
/// <param name="TelegramReady">The weekly copy outside Railway can be sent (bot and recipient configured, D-37).</param>
/// <param name="TelegramRecipient">Masked Telegram id of the recipient, e.g. "…4321".</param>
public sealed record BackupStatusDto(
    bool Enabled,
    BackupRunDto? Last,
    DateTimeOffset? LastVerifiedAt,
    int FilesKept,
    bool TelegramReady = false,
    string? TelegramRecipient = null,
    DateTimeOffset? LastSentToTelegramAt = null,
    string? TelegramError = null);

public sealed record BackupSendResult(bool Sent, string Message);
