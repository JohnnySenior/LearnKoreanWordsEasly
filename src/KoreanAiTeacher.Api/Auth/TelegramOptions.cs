namespace KoreanAiTeacher.Api.Auth;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>Bot token from BotFather. Secret: set via environment/hosting secrets, never in the repo.</summary>
    public string BotToken { get; set; } = "";

    /// <summary>Telegram user IDs allowed to use the app in v1 (spec SEC02).</summary>
    public long[] AllowedUserIds { get; set; } = [];

    /// <summary>How old a Mini App initData may be when exchanged for a session (spec SEC01: 5 minutes).</summary>
    public TimeSpan InitDataMaxAge { get; set; } = TimeSpan.FromMinutes(5);

    public bool IsAllowed(long userId) => AllowedUserIds.Contains(userId);
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Development only: lets a browser outside Telegram sign in as this (allow-listed) user.
    /// The endpoint is not even mapped outside the Development environment.
    /// </summary>
    public long? DevLoginUserId { get; set; }
}
