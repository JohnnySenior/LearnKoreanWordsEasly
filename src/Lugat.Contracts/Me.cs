namespace Lugat.Contracts;

/// <param name="RemindersAvailable">False when the server has neither the bot nor Web Push configured.</param>
/// <param name="BotBlocked">Telegram refused the last message: the learner should press Start in the bot (spec NOT02).</param>
/// <param name="HasTelegram">The learner signs in with Telegram, so the bot can write to them (spec §8).</param>
/// <param name="PushAvailable">The server has VAPID keys: a device can get reminders by Web Push (L-63).</param>
/// <param name="PushDevices">How many of the learner's devices are subscribed.</param>
/// <param name="ReminderDays">Days the reminder may come, Monday first.</param>
public sealed record PreferencesDto(
    string TimeZone,
    bool RemindersEnabled,
    TimeOnly ReminderTime,
    IReadOnlyList<DayOfWeek> ReminderDays,
    bool RemindersAvailable,
    bool BotBlocked,
    bool HasTelegram,
    bool PushAvailable = false,
    int PushDevices = 0);

/// <summary>The VAPID public key a browser subscribes with (L-63).</summary>
public sealed record PushKeyDto(string PublicKey);

/// <summary>A device's push subscription, as PushSubscription.toJSON() gives it: the endpoint and its two keys.</summary>
public sealed record PushSubscribeRequest(string Endpoint, string P256dh, string Auth);

public sealed record PushUnsubscribeRequest(string Endpoint);

/// <summary>Null fields are left unchanged. <paramref name="TimeZone"/>: "Asia/Seoul" or "Asia/Tashkent".</summary>
public sealed record UpdatePreferencesRequest(bool? RemindersEnabled, TimeOnly? ReminderTime, IReadOnlyList<DayOfWeek>? ReminderDays = null, string? TimeZone = null);

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
/// <param name="TelegramOverdue">Copies in the Telegram chat past the privacy policy's month, still to be deleted by hand.</param>
public sealed record BackupStatusDto(
    bool Enabled,
    BackupRunDto? Last,
    DateTimeOffset? LastVerifiedAt,
    int FilesKept,
    bool TelegramReady = false,
    string? TelegramRecipient = null,
    DateTimeOffset? LastSentToTelegramAt = null,
    string? TelegramError = null,
    IReadOnlyList<DateTimeOffset>? TelegramOverdue = null);

public sealed record BackupSendResult(bool Sent, string Message);
